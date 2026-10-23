using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Staybnb.Data;
using Staybnb.Models.Common;
using Staybnb.Models.Constants;
using Staybnb.Models.Entities;
using Staybnb.Models.Enums;
using Staybnb.Models.Settings;
using Staybnb.Models.ViewModels.Host;
using Staybnb.Services.Interfaces;

namespace Staybnb.Services
{
    public class HostApplicationService : IHostApplicationService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IFileStorageService _files;
        private readonly IActivityLogService _activityLog;
        private readonly INotificationService _notifications;
        private readonly StaybnbSettings _settings;
        private readonly ILogger<HostApplicationService> _logger;

        public HostApplicationService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IFileStorageService files,
            IActivityLogService activityLog,
            INotificationService notifications,
            IOptions<StaybnbSettings> settings,
            ILogger<HostApplicationService> logger)
        {
            _context = context;
            _userManager = userManager;
            _files = files;
            _activityLog = activityLog;
            _notifications = notifications;
            _settings = settings.Value;
            _logger = logger;
        }

        // Queries

        public Task<HostApplication?> GetPendingForGuestAsync(string userId) =>
            _context.HostApplications
                .Include(a => a.Property)
                .FirstOrDefaultAsync(a => a.ApplicationUserId == userId
                                          && a.Status == ApplicationStatus.Pending
                                          && !a.IsAdditionalProperty);

        public Task<List<HostApplication>> GetForUserAsync(string userId) =>
            _context.HostApplications
                .AsNoTracking()
                .Include(a => a.Property).ThenInclude(p => p.Images)
                .Where(a => a.ApplicationUserId == userId)
                .OrderByDescending(a => a.AppliedAt)
                .ToListAsync();

        // Submit

        public async Task<ServiceResult<HostApplication>> SubmitAsync(string userId, HostApplicationViewModel model)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ServiceResult<HostApplication>.Fail("User not found.");

            var isHost = await _userManager.IsInRoleAsync(user, Roles.Host);
            var isGuest = await _userManager.IsInRoleAsync(user, Roles.Guest);
            if (!isHost && !isGuest)
                return ServiceResult<HostApplication>.Fail("Only Guests and Hosts can submit property applications.");

            if (isGuest && await GetPendingForGuestAsync(userId) != null)
                return ServiceResult<HostApplication>.Fail("You already have an application waiting for review.");

            // Need at least one photo
            var images = (model.Images ?? new List<IFormFile>()).Where(f => f != null && f.Length > 0).ToList();
            if (images.Count == 0)
                return ServiceResult<HostApplication>.Fail("Your application is incomplete: please add at least one photo of your place.");

            if (images.Count > _settings.MaxImagesPerProperty)
                return ServiceResult<HostApplication>.Fail($"You can upload a maximum of {_settings.MaxImagesPerProperty} photos.");

            foreach (var image in images)
            {
                var validation = _files.ValidateImage(image);
                if (!validation.IsValid)
                    return ServiceResult<HostApplication>.Fail(validation.Error!);
            }

            // Save the photos first; if anything fails later, delete them again
            var savedUrls = new List<string>();
            try
            {
                foreach (var image in images)
                    savedUrls.Add(await _files.SaveImageAsync(image, ImageFolders.Properties));

                var amenityIds = model.SelectedAmenityIds?.Distinct().ToList() ?? new List<int>();
                var amenities = await _context.Amenities.Where(a => amenityIds.Contains(a.Id)).ToListAsync();

                var property = new HostProperty
                {
                    Title = model.Title.Trim(),
                    Description = model.Description.Trim(),
                    PropertyType = model.PropertyType,
                    Address = model.Address.Trim(),
                    City = NormaliseCity(model.City),
                    PricePerNight = model.PricePerNight,
                    CleaningFee = model.CleaningFee,
                    MaxGuests = model.MaxGuests,
                    Bedrooms = model.Bedrooms,
                    Beds = model.Beds,
                    Bathrooms = model.Bathrooms,
                    IsActive = false,          // only goes live once an Admin approves it
                    HostId = userId,
                    CreatedAt = DateTime.UtcNow,
                    Amenities = amenities,
                    Images = savedUrls.Select((url, index) => new PropertyImage
                    {
                        ImageUrl = url,
                        IsCover = index == 0,
                        UploadedAt = DateTime.UtcNow
                    }).ToList()
                };

                // Every property gets a default check-in process the Host can edit later
                var checkIn = new CheckInProcess
                {
                    Title = $"Check-in for {property.Title}",
                    CreatedAt = DateTime.UtcNow,
                    Property = property
                };
                checkIn.SetSteps(CheckInProcess.DefaultSteps());

                var application = new HostApplication
                {
                    ApplicationUserId = userId,
                    Property = property,
                    Status = ApplicationStatus.Pending,
                    AppliedAt = DateTime.UtcNow,
                    IsAdditionalProperty = isHost
                };

                _context.HostProperties.Add(property);
                _context.CheckInProcesses.Add(checkIn);
                _context.HostApplications.Add(application);
                await _context.SaveChangesAsync();

                // Log + notify
                var action = isHost ? ActivityActions.AdditionalPropertyApplicationSubmitted : ActivityActions.HostApplicationSubmitted;
                await _activityLog.LogAsync(userId, action, ActivityType.HostApplication,
                    $"{user.FullName} submitted application #{application.Id} for '{property.Title}' in {property.City} with {savedUrls.Count} photo(s).");

                await _notifications.CreateAsync(userId,
                    isHost ? "Additional Property Application Submitted" : "Host Application Submitted",
                    $"Your application for '{property.Title}' has been received and is under review.",
                    NotificationType.ApplicationSubmitted,
                    "/HostApplication/Status");

                var adminMessage = $"{user.FullName} submitted '{property.Title}' ({property.City}) for review.";
                var adminLink = $"/Admin/ReviewApplication/{application.Id}";
                await _notifications.NotifyRoleAsync(Roles.Admin, "New Host Application", adminMessage, NotificationType.ApplicationSubmitted, adminLink);
                await _notifications.NotifyRoleAsync(Roles.SuperAdmin, "New Host Application", adminMessage, NotificationType.ApplicationSubmitted, adminLink);

                return ServiceResult<HostApplication>.Ok(application,
                    "Your application has been submitted! An administrator will review it shortly.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Host application submission failed for user {UserId}", userId);
                foreach (var url in savedUrls)
                    _files.DeleteImage(url);

                return ServiceResult<HostApplication>.Fail("Something went wrong while saving your application. Please try again.");
            }
        }

        // Approve  (the Guest -> Host transition)

        public async Task<ServiceResult> ApproveAsync(int applicationId, string adminId, string? adminNotes)
        {
            var application = await _context.HostApplications
                .Include(a => a.Property)
                .Include(a => a.Applicant)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null)
                return ServiceResult.Fail("Application not found.");

            if (application.Status != ApplicationStatus.Pending)
                return ServiceResult.Fail($"This application has already been {application.Status.ToString().ToLower()}.");

            var user = application.Applicant;
            var roleSwapped = false;

            // Everything below succeeds together or not at all.
            // UserManager uses the same DbContext, so its role changes join this transaction.
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Swap roles: add Host, drop Guest
                if (!await _userManager.IsInRoleAsync(user, Roles.Host))
                {
                    var addResult = await _userManager.AddToRoleAsync(user, Roles.Host);
                    if (!addResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult.Fail("Could not add the Host role: " + JoinErrors(addResult));
                    }
                    roleSwapped = true;
                }

                if (await _userManager.IsInRoleAsync(user, Roles.Guest))
                {
                    // In Staybnb a user is either a Guest or a Host, never both
                    var removeResult = await _userManager.RemoveFromRoleAsync(user, Roles.Guest);
                    if (!removeResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult.Fail("Could not remove the Guest role: " + JoinErrors(removeResult));
                    }
                    roleSwapped = true;
                }

                // Application + property
                application.Status = ApplicationStatus.Approved;
                application.ReviewedAt = DateTime.UtcNow;
                application.ReviewedById = adminId;
                application.AdminNotes = string.IsNullOrWhiteSpace(adminNotes) ? null : adminNotes.Trim();

                application.Property.IsActive = true;
                application.Property.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Approving host application {Id} failed", applicationId);
                return ServiceResult.Fail("Something went wrong while approving the application. No changes were made.");
            }

            // Log + notify (after the commit)
            await _activityLog.LogAsync(adminId, ActivityActions.HostApplicationApproved, ActivityType.HostApplication,
                $"Application #{application.Id} for '{application.Property.Title}' by {user.FullName} approved. Property is now active.");

            if (roleSwapped)
            {
                await _activityLog.LogAsync(user.Id, ActivityActions.GuestBecameHost, ActivityType.RoleChange,
                    $"{user.FullName} ({user.Email}): role '{Roles.Guest}' removed, role '{Roles.Host}' added by admin {adminId}.");
            }

            await _notifications.CreateAsync(user.Id,
                application.IsAdditionalProperty ? "Property Approved" : "Host Application Approved",
                application.IsAdditionalProperty
                    ? $"Your property '{application.Property.Title}' has been approved and is now live."
                    : $"Congratulations! Your host application for '{application.Property.Title}' has been approved. You are now a Staybnb Host.",
                NotificationType.ApplicationApproved,
                "/Host/Dashboard");

            return ServiceResult.Ok(roleSwapped
                ? $"Application approved. {user.FullName} is now a Host and '{application.Property.Title}' is live."
                : $"Application approved. '{application.Property.Title}' is now live.");
        }

        // Reject

        public async Task<ServiceResult> RejectAsync(int applicationId, string adminId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return ServiceResult.Fail("Please give a reason so the applicant knows what to fix.");

            var application = await _context.HostApplications
                .Include(a => a.Property)
                .Include(a => a.Applicant)
                .FirstOrDefaultAsync(a => a.Id == applicationId);

            if (application == null)
                return ServiceResult.Fail("Application not found.");

            if (application.Status != ApplicationStatus.Pending)
                return ServiceResult.Fail($"This application has already been {application.Status.ToString().ToLower()}.");

            application.Status = ApplicationStatus.Rejected;
            application.ReviewedAt = DateTime.UtcNow;
            application.ReviewedById = adminId;
            application.AdminNotes = reason.Trim();
            application.Property.IsActive = false;

            await _context.SaveChangesAsync();

            await _activityLog.LogAsync(adminId, ActivityActions.HostApplicationRejected, ActivityType.HostApplication,
                $"Application #{application.Id} for '{application.Property.Title}' by {application.Applicant.FullName} rejected. Reason: {reason.Trim()}");

            await _notifications.CreateAsync(application.ApplicationUserId,
                "Host Application Not Approved",
                $"Your application for '{application.Property.Title}' was not approved. Reason: {reason.Trim()} You are welcome to apply again.",
                NotificationType.ApplicationRejected,
                "/HostApplication/Status");

            return ServiceResult.Ok("Application rejected and the applicant has been notified.");
        }

        // Helpers

        /// <summary>"  cape town " -> "Cape Town", so the city filter groups listings correctly.</summary>
        private static string NormaliseCity(string city)
        {
            var trimmed = string.Join(' ', city.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(trimmed.ToLowerInvariant());
        }

        private static string JoinErrors(IdentityResult result) =>
            string.Join("; ", result.Errors.Select(e => e.Description));
    }
}
