using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Staybnb.Data;
using Staybnb.Models.Constants;
using Staybnb.Models.Entities;
using Staybnb.Models.Enums;
using Staybnb.Models.Settings;
using Staybnb.Models.ViewModels.Host;
using Staybnb.Services;
using Staybnb.Services.Interfaces;

namespace Staybnb.Controllers
{
    /// <summary>
    /// Host dashboard: properties, booking requests, check-in configuration,
    /// guest document verification, earnings and reviews.
    /// Only users in the Host role can open these pages (Guests get Access Denied).
    /// </summary>
    [Authorize(Roles = Roles.Host)]
    public class HostController : Controller
    {
        private const long MaxUploadBytes = 60L * 1024 * 1024;

        /// <summary>Booking statuses where the guest has paid and the Host earns money.</summary>
        private static readonly BookingStatus[] EarningStatuses =
            { BookingStatus.Approved, BookingStatus.CheckedIn, BookingStatus.Completed };

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBookingService _bookings;
        private readonly ICheckInService _checkIns;
        private readonly IFileStorageService _files;
        private readonly IActivityLogService _activityLog;
        private readonly StaybnbSettings _settings;

        public HostController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IBookingService bookings,
            ICheckInService checkIns,
            IFileStorageService files,
            IActivityLogService activityLog,
            IOptions<StaybnbSettings> settings)
        {
            _context = context;
            _userManager = userManager;
            _bookings = bookings;
            _checkIns = checkIns;
            _files = files;
            _activityLog = activityLog;
            _settings = settings.Value;
        }

        private string HostId => _userManager.GetUserId(User)!;

        /// <summary>Host's earnings for a booking = what the guest paid minus Staybnb's service fee.</summary>
        private static decimal HostPayout(Booking b) => b.TotalPrice - b.ServiceFee;

        // Dashboard

        [HttpGet]
        public IActionResult Index() => RedirectToAction(nameof(Dashboard));

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var hostId = HostId;
            var user = await _userManager.GetUserAsync(User);
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthEnd = monthStart.AddMonths(1);

            var properties = await _context.HostProperties
                .AsNoTracking()
                .Where(p => p.HostId == hostId)
                .Select(p => new { p.Id, p.IsActive })
                .ToListAsync();

            var bookings = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.Guest)
                .Include(b => b.Payment)
                .Include(b => b.Property).ThenInclude(p => p.Images)
                .Where(b => b.Property.HostId == hostId)
                .ToListAsync();

            var reviews = await _context.Reviews
                .AsNoTracking()
                .Where(r => r.Property.HostId == hostId)
                .Select(r => r.Rating)
                .ToListAsync();

            var earning = bookings.Where(b => EarningStatuses.Contains(b.Status)).ToList();
            var decided = bookings.Count(b => b.Status != BookingStatus.Pending && b.Status != BookingStatus.Cancelled);
            var activeCount = properties.Count(p => p.IsActive);

            // Occupancy: booked nights this month / (active properties x days in month)
            var bookedNightsThisMonth = earning.Sum(b =>
            {
                var start = b.CheckInDate < monthStart ? monthStart : b.CheckInDate;
                var end = b.CheckOutDate > monthEnd ? monthEnd : b.CheckOutDate;
                return Math.Max(0, (end - start).Days);
            });
            var availableNights = activeCount * DateTime.DaysInMonth(today.Year, today.Month);

            var model = new HostDashboardViewModel
            {
                HostFirstName = user?.FirstName ?? "Host",
                TotalProperties = properties.Count,
                ActiveProperties = activeCount,
                PendingApplications = await _context.HostApplications
                    .CountAsync(a => a.ApplicationUserId == hostId && a.Status == ApplicationStatus.Pending),

                TotalBookings = bookings.Count,
                BookingsThisMonth = bookings.Count(b => b.CreatedAt >= monthStart.ToUniversalTime()),
                PendingRequests = bookings.Count(b => b.Status == BookingStatus.Pending),
                CheckInsToReview = await _context.GuestCheckIns
                    .CountAsync(c => c.Booking.Property.HostId == hostId && c.Status == CheckInStatus.Submitted),

                TotalEarnings = earning.Sum(HostPayout),
                EarningsThisMonth = earning.Where(b => b.Payment?.PaidAt >= monthStart.ToUniversalTime()).Sum(HostPayout),
                AverageBookingValue = earning.Any() ? Math.Round(earning.Average(HostPayout), 2) : 0,

                ReviewCount = reviews.Count,
                AverageRating = reviews.Any() ? Math.Round(reviews.Average(), 1) : 0,
                AcceptanceRate = decided > 0 ? Math.Round(100.0 * earning.Count / decided, 1) : 0,
                OccupancyRateThisMonth = availableNights > 0 ? Math.Round(100.0 * bookedNightsThisMonth / availableNights, 1) : 0,

                PendingBookingRequests = bookings
                    .Where(b => b.Status == BookingStatus.Pending)
                    .OrderBy(b => b.CheckInDate).Take(5).ToList(),
                UpcomingStays = bookings
                    .Where(b => (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn) && b.CheckOutDate >= today)
                    .OrderBy(b => b.CheckInDate).Take(5).ToList()
            };

            return View(model);
        }

        // My properties

        [HttpGet]
        public async Task<IActionResult> Properties()
        {
            var hostId = HostId;

            var properties = await _context.HostProperties
                .AsNoTracking()
                .Where(p => p.HostId == hostId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new HostPropertyListItem
                {
                    Id = p.Id,
                    Title = p.Title,
                    City = p.City,
                    PropertyType = p.PropertyType,
                    PricePerNight = p.PricePerNight,
                    MaxGuests = p.MaxGuests,
                    Bedrooms = p.Bedrooms,
                    Beds = p.Beds,
                    Bathrooms = p.Bathrooms,
                    IsActive = p.IsActive,
                    CoverImageUrl = p.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault(),
                    ImageCount = p.Images.Count,
                    BookingCount = p.Bookings.Count,
                    ReviewCount = p.Reviews.Count,
                    AverageRating = p.Reviews.Any() ? p.Reviews.Average(r => (double)r.Rating) : 0,
                    ApplicationStatus = p.HostApplications
                        .OrderByDescending(a => a.AppliedAt)
                        .Select(a => (ApplicationStatus?)a.Status)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return View(properties);
        }

        // POST: /Host/ToggleActive/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var property = await _context.HostProperties
                .Include(p => p.HostApplications)
                .FirstOrDefaultAsync(p => p.Id == id && p.HostId == HostId);

            if (property == null) return NotFound();

            var latest = property.HostApplications.OrderByDescending(a => a.AppliedAt).FirstOrDefault();
            if (latest != null && latest.Status != ApplicationStatus.Approved)
            {
                TempData["Error"] = "This property can only be activated once an administrator has approved it.";
                return RedirectToAction(nameof(Properties));
            }

            property.IsActive = !property.IsActive;
            property.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _activityLog.LogAsync(HostId,
                property.IsActive ? ActivityActions.PropertyActivated : ActivityActions.PropertyDeactivated,
                ActivityType.PropertyUpdate,
                $"'{property.Title}' (#{property.Id}) {(property.IsActive ? "activated" : "deactivated")} by the Host.");

            var hasFutureStays = await _context.Bookings.AnyAsync(b => b.PropertyId == id
                && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Pending)
                && b.CheckOutDate >= DateTime.Today);

            TempData["Success"] = property.IsActive
                ? $"'{property.Title}' is now live and visible to guests."
                : $"'{property.Title}' is now hidden from guests." +
                  (hasFutureStays ? " Existing bookings and requests are not affected." : "");

            return RedirectToAction(nameof(Properties));
        }

        // Edit property

        [HttpGet]
        public async Task<IActionResult> EditProperty(int id)
        {
            var property = await _context.HostProperties
                .AsNoTracking()
                .Include(p => p.Images)
                .Include(p => p.Amenities)
                .FirstOrDefaultAsync(p => p.Id == id && p.HostId == HostId);

            if (property == null) return NotFound();

            var model = new PropertyEditViewModel
            {
                Id = property.Id,
                Title = property.Title,
                Description = property.Description,
                PropertyType = property.PropertyType,
                Address = property.Address,
                City = property.City,
                MaxGuests = property.MaxGuests,
                Bedrooms = property.Bedrooms,
                Beds = property.Beds,
                Bathrooms = property.Bathrooms,
                PricePerNight = property.PricePerNight,
                CleaningFee = property.CleaningFee,
                SelectedAmenityIds = property.Amenities.Select(a => a.Id).ToList()
            };

            await PopulateEditFormAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxUploadBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
        public async Task<IActionResult> EditProperty(PropertyEditViewModel model)
        {
            var property = await _context.HostProperties
                .Include(p => p.Images)
                .Include(p => p.Amenities)
                .FirstOrDefaultAsync(p => p.Id == model.Id && p.HostId == HostId);

            if (property == null) return NotFound();

            var newImages = (model.NewImages ?? new List<IFormFile>()).Where(f => f != null && f.Length > 0).ToList();
            if (property.Images.Count + newImages.Count > _settings.MaxImagesPerProperty)
                ModelState.AddModelError(nameof(model.NewImages),
                    $"A property can have at most {_settings.MaxImagesPerProperty} photos (it has {property.Images.Count}).");

            foreach (var file in newImages)
            {
                var validation = _files.ValidateImage(file);
                if (!validation.IsValid)
                    ModelState.AddModelError(nameof(model.NewImages), validation.Error!);
            }

            if (!ModelState.IsValid)
            {
                await PopulateEditFormAsync(model);
                return View(model);
            }

            // Details
            property.Title = model.Title.Trim();
            property.Description = model.Description.Trim();
            property.PropertyType = model.PropertyType;
            property.Address = model.Address.Trim();
            property.City = System.Globalization.CultureInfo.InvariantCulture.TextInfo
                .ToTitleCase(string.Join(' ', model.City.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant());
            property.MaxGuests = model.MaxGuests;
            property.Bedrooms = model.Bedrooms;
            property.Beds = model.Beds;
            property.Bathrooms = model.Bathrooms;
            property.PricePerNight = model.PricePerNight;
            property.CleaningFee = model.CleaningFee;
            property.UpdatedAt = DateTime.UtcNow;

            // Amenities (many-to-many)
            var selectedIds = model.SelectedAmenityIds?.Distinct().ToList() ?? new List<int>();
            property.Amenities.Clear();
            var amenities = await _context.Amenities.Where(a => selectedIds.Contains(a.Id)).ToListAsync();
            foreach (var amenity in amenities)
                property.Amenities.Add(amenity);

            // New photos
            var hasCover = property.Images.Any(i => i.IsCover);
            foreach (var file in newImages)
            {
                var url = await _files.SaveImageAsync(file, ImageFolders.Properties);
                property.Images.Add(new PropertyImage { ImageUrl = url, IsCover = !hasCover, UploadedAt = DateTime.UtcNow });
                hasCover = true;
            }

            await _context.SaveChangesAsync();

            await _activityLog.LogAsync(HostId, ActivityActions.PropertyUpdated, ActivityType.PropertyUpdate,
                $"'{property.Title}' (#{property.Id}) updated" + (newImages.Any() ? $", {newImages.Count} photo(s) added." : "."));

            TempData["Success"] = "Your listing has been updated.";
            return RedirectToAction(nameof(EditProperty), new { id = property.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int imageId)
        {
            var image = await _context.PropertyImages
                .Include(i => i.Property).ThenInclude(p => p.Images)
                .FirstOrDefaultAsync(i => i.Id == imageId && i.Property.HostId == HostId);

            if (image == null) return NotFound();

            if (image.Property.Images.Count <= 1)
            {
                TempData["Error"] = "A listing must keep at least one photo.";
                return RedirectToAction(nameof(EditProperty), new { id = image.PropertyId });
            }

            var wasCover = image.IsCover;
            var propertyId = image.PropertyId;
            _context.PropertyImages.Remove(image);

            if (wasCover)
            {
                var next = image.Property.Images.Where(i => i.Id != imageId).OrderBy(i => i.Id).First();
                next.IsCover = true;
            }

            await _context.SaveChangesAsync();
            _files.DeleteImage(image.ImageUrl);

            TempData["Success"] = "Photo removed.";
            return RedirectToAction(nameof(EditProperty), new { id = propertyId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetCoverImage(int imageId)
        {
            var image = await _context.PropertyImages
                .Include(i => i.Property).ThenInclude(p => p.Images)
                .FirstOrDefaultAsync(i => i.Id == imageId && i.Property.HostId == HostId);

            if (image == null) return NotFound();

            foreach (var other in image.Property.Images)
                other.IsCover = other.Id == imageId;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Cover photo updated.";
            return RedirectToAction(nameof(EditProperty), new { id = image.PropertyId });
        }

        private async Task PopulateEditFormAsync(PropertyEditViewModel model)
        {
            model.AvailableAmenities = await _context.Amenities.AsNoTracking().OrderBy(a => a.Name)
                .Select(a => new AmenityOption { Id = a.Id, Name = a.Name, IconClass = a.IconClass })
                .ToListAsync();

            model.ExistingImages = await _context.PropertyImages.AsNoTracking()
                .Where(i => i.PropertyId == model.Id)
                .OrderByDescending(i => i.IsCover).ThenBy(i => i.Id)
                .ToListAsync();

            model.IsActive = await _context.HostProperties.Where(p => p.Id == model.Id).Select(p => p.IsActive).FirstOrDefaultAsync();
            model.MaxImages = _settings.MaxImagesPerProperty;
        }

        // Booking requests

        // GET: /Host/BookingRequests?filter=pending&propertyId=3
        [HttpGet]
        public async Task<IActionResult> BookingRequests(string filter = "pending", int? propertyId = null)
        {
            var hostId = HostId;
            filter = (filter ?? "pending").ToLowerInvariant();

            var baseQuery = _context.Bookings.AsNoTracking().Where(b => b.Property.HostId == hostId);
            if (propertyId.HasValue)
                baseQuery = baseQuery.Where(b => b.PropertyId == propertyId.Value);

            var query = filter switch
            {
                "upcoming" => baseQuery.Where(b => b.Status == BookingStatus.Approved),
                "checkedin" => baseQuery.Where(b => b.Status == BookingStatus.CheckedIn),
                "past" => baseQuery.Where(b => b.Status == BookingStatus.Completed
                                               || b.Status == BookingStatus.Rejected
                                               || b.Status == BookingStatus.Cancelled),
                "all" => baseQuery,
                _ => baseQuery.Where(b => b.Status == BookingStatus.Pending)
            };

            var bookings = await query
                .Include(b => b.Guest)
                .Include(b => b.Payment)
                .Include(b => b.GuestCheckIn)
                .Include(b => b.Property).ThenInclude(p => p.Images)
                .ToListAsync();

            // Past/all: most recent first. Everything else: soonest stay first.
            bookings = filter is "past" or "all"
                ? bookings.OrderByDescending(b => b.CheckInDate).ToList()
                : bookings.OrderBy(b => b.CheckInDate).ToList();

            var model = new HostBookingsViewModel
            {
                Filter = filter,
                PropertyId = propertyId,
                Bookings = bookings,
                Properties = await _context.HostProperties.AsNoTracking()
                    .Where(p => p.HostId == hostId).OrderBy(p => p.Title)
                    .Select(p => new HostPropertyOption { Id = p.Id, Title = p.Title })
                    .ToListAsync(),
                PendingCount = await baseQuery.CountAsync(b => b.Status == BookingStatus.Pending),
                UpcomingCount = await baseQuery.CountAsync(b => b.Status == BookingStatus.Approved),
                CheckedInCount = await baseQuery.CountAsync(b => b.Status == BookingStatus.CheckedIn)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveBooking(int id, string? returnFilter)
        {
            var result = await _bookings.ApproveAsync(id, HostId);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(BookingRequests), new { filter = returnFilter ?? "pending" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectBooking(int id, string? reason, string? returnFilter)
        {
            var result = await _bookings.RejectAsync(id, HostId, reason);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(BookingRequests), new { filter = returnFilter ?? "pending" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteBooking(int id, string? returnFilter)
        {
            var result = await _bookings.CompleteAsync(id, HostId);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(BookingRequests), new { filter = returnFilter ?? "checkedin" });
        }

        // Check-in process (rules + required documents per property)

        [HttpGet]
        public async Task<IActionResult> ConfigureCheckIn(int propertyId)
        {
            var property = await _context.HostProperties
                .AsNoTracking()
                .Include(p => p.CheckInProcess)
                .FirstOrDefaultAsync(p => p.Id == propertyId && p.HostId == HostId);

            if (property == null) return NotFound();

            var steps = property.CheckInProcess?.GetSteps() ?? CheckInProcess.DefaultSteps();

            var model = new CheckInProcessViewModel
            {
                PropertyId = property.Id,
                PropertyTitle = property.Title,
                Title = property.CheckInProcess?.Title ?? $"Check-in for {property.Title}",
                Steps = steps.Select(s => new CheckInStepInput
                {
                    Title = s.Title,
                    Instructions = s.Instructions,
                    RequiresDocument = s.RequiresDocument,
                    DocumentType = s.DocumentType
                }).ToList()
            };

            ViewBag.Properties = await GetPropertyOptionsAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfigureCheckIn(CheckInProcessViewModel model)
        {
            var property = await _context.HostProperties
                .Include(p => p.CheckInProcess)
                .FirstOrDefaultAsync(p => p.Id == model.PropertyId && p.HostId == HostId);

            if (property == null) return NotFound();

            // Ignore completely empty rows, then re-check the rest ourselves
            // (the list is renumbered, so the original Steps[i] ModelState entries no longer match)
            model.Steps ??= new List<CheckInStepInput>();
            model.Steps = model.Steps.Where(s => !string.IsNullOrWhiteSpace(s.Title)).ToList();
            foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Steps", StringComparison.OrdinalIgnoreCase)).ToList())
                ModelState.Remove(key);

            if (model.Steps.Any(s => s.Title.Trim().Length > 100 || (s.Instructions?.Length ?? 0) > 1000))
                ModelState.AddModelError(string.Empty, "Step titles are limited to 100 characters and instructions to 1000.");

            if (model.Steps.Count == 0)
                ModelState.AddModelError(string.Empty, "Add at least one check-in step.");

            if (model.Steps.Count > 10)
                ModelState.AddModelError(string.Empty, "A check-in process can have at most 10 steps.");

            if (!model.Steps.Any(s => s.RequiresDocument))
                ModelState.AddModelError(string.Empty,
                    "At least one step must ask the guest to upload an ID or passport so you can verify who is staying.");

            if (!ModelState.IsValid)
            {
                model.PropertyTitle = property.Title;
                ViewBag.Properties = await GetPropertyOptionsAsync();
                return View(model);
            }

            var steps = model.Steps.Select((s, index) => new CheckInStep
            {
                StepNumber = index + 1,
                Title = s.Title.Trim(),
                Instructions = s.Instructions?.Trim() ?? string.Empty,
                RequiresDocument = s.RequiresDocument,
                DocumentType = s.RequiresDocument
                    ? (string.IsNullOrWhiteSpace(s.DocumentType) ? "ID or Passport" : s.DocumentType.Trim())
                    : null
            }).ToList();

            if (property.CheckInProcess == null)
            {
                property.CheckInProcess = new CheckInProcess { PropertyId = property.Id, CreatedAt = DateTime.UtcNow };
            }

            property.CheckInProcess.Title = model.Title.Trim();
            property.CheckInProcess.SetSteps(steps);
            property.CheckInProcess.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _activityLog.LogAsync(HostId, ActivityActions.CheckInProcessUpdated, ActivityType.PropertyUpdate,
                $"Check-in process for '{property.Title}' updated ({steps.Count} step(s)).");

            TempData["Success"] = "Check-in process saved. New check-ins will follow these steps.";
            return RedirectToAction(nameof(ConfigureCheckIn), new { propertyId = property.Id });
        }

        private Task<List<HostPropertyOption>> GetPropertyOptionsAsync() =>
            _context.HostProperties.AsNoTracking()
                .Where(p => p.HostId == HostId).OrderBy(p => p.Title)
                .Select(p => new HostPropertyOption { Id = p.Id, Title = p.Title })
                .ToListAsync();

        // Guest check-ins (verify documents)

        [HttpGet]
        public async Task<IActionResult> CheckIns()
        {
            var hostId = HostId;
            var all = await _context.GuestCheckIns
                .AsNoTracking()
                .Include(c => c.Documents)
                .Include(c => c.Booking).ThenInclude(b => b.Guest)
                .Include(c => c.Booking).ThenInclude(b => b.Property)
                .Where(c => c.Booking.Property.HostId == hostId)
                .OrderByDescending(c => c.SubmittedAt ?? c.CreatedAt)
                .ToListAsync();

            var model = new HostCheckInsViewModel
            {
                AwaitingReview = all.Where(c => c.Status == CheckInStatus.Submitted).ToList(),
                InProgress = all.Where(c => c.Status is CheckInStatus.NotStarted or CheckInStatus.InProgress or CheckInStatus.Rejected).ToList(),
                Completed = all.Where(c => c.Status == CheckInStatus.Verified).Take(20).ToList()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ReviewCheckIn(int id)
        {
            var checkIn = await _checkIns.GetForHostAsync(id, HostId);
            if (checkIn == null) return NotFound();

            var steps = checkIn.CheckInProcess.GetSteps();
            var requiredTypes = steps.Where(s => s.RequiresDocument).Select(CheckInService.DocumentTypeFor).Distinct().ToList();

            var model = new CheckInReviewViewModel
            {
                CheckIn = checkIn,
                Steps = steps,
                AllDocumentsVerified = requiredTypes.All(t =>
                    checkIn.Documents.Any(d => d.DocumentType == t && d.Status == DocumentStatus.Verified))
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyDocument(int documentId, int checkInId)
        {
            var result = await _checkIns.VerifyDocumentAsync(documentId, HostId);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(ReviewCheckIn), new { id = checkInId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectDocument(int documentId, int checkInId, string reason)
        {
            var result = await _checkIns.RejectDocumentAsync(documentId, HostId, reason);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(ReviewCheckIn), new { id = checkInId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveCheckIn(int id)
        {
            var result = await _checkIns.ApproveCheckInAsync(id, HostId);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return result.Succeeded
                ? RedirectToAction(nameof(CheckIns))
                : RedirectToAction(nameof(ReviewCheckIn), new { id });
        }

        // Earnings

        [HttpGet]
        public async Task<IActionResult> Earnings()
        {
            var hostId = HostId;
            var today = DateTime.Today;
            var monthStartUtc = new DateTime(today.Year, today.Month, 1).ToUniversalTime();

            var paid = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.Guest)
                .Include(b => b.Payment)
                .Include(b => b.Property)
                .Where(b => b.Property.HostId == hostId
                            && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Completed))
                .OrderByDescending(b => b.CheckInDate)
                .ToListAsync();

            var model = new HostEarningsViewModel
            {
                TotalEarnings = paid.Sum(HostPayout),
                EarningsThisMonth = paid.Where(b => b.Payment?.PaidAt >= monthStartUtc).Sum(HostPayout),
                ExpectedUpcoming = paid.Where(b => b.Status == BookingStatus.Approved && b.CheckInDate >= today).Sum(HostPayout),
                PaidBookings = paid,
                Monthly = paid
                    .GroupBy(b => new { b.CheckInDate.Year, b.CheckInDate.Month })
                    .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month)
                    .Take(12)
                    .Select(g => new MonthlyEarning
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        Amount = g.Sum(HostPayout),
                        Bookings = g.Count()
                    })
                    .ToList()
            };

            return View(model);
        }

        // Reviews

        [HttpGet]
        public async Task<IActionResult> Reviews()
        {
            var hostId = HostId;
            var reviews = await _context.Reviews
                .AsNoTracking()
                .Include(r => r.Reviewer)
                .Include(r => r.Property)
                .Where(r => r.Property.HostId == hostId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(new HostReviewsViewModel
            {
                Reviews = reviews,
                AverageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 1) : 0
            });
        }
    }
}
