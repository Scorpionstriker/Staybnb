using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models.Constants;
using Staybnb.Models.Entities;
using Staybnb.Models.Enums;
using Staybnb.Models.ViewModels.Admin;
using Staybnb.Services.Interfaces;

namespace Staybnb.Controllers
{
    /// <summary>
    /// Admin "command centre": dashboard, application review, bookings and properties
    /// overview, and the System Audit (ActivityLog). Admin and SuperAdmin only.
    /// User management (promoting Guests to Admin) is in UserManagementController - SuperAdmin only.
    /// </summary>
    [Authorize(Roles = Roles.AdminOrSuperAdmin)]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHostApplicationService _applications;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogService _activityLog;
        private readonly INotificationService _notifications;

        private const int LogsPageSize = 25;

        public AdminController(
            ApplicationDbContext context,
            IHostApplicationService applications,
            UserManager<ApplicationUser> userManager,
            IActivityLogService activityLog,
            INotificationService notifications)
        {
            _context = context;
            _applications = applications;
            _userManager = userManager;
            _activityLog = activityLog;
            _notifications = notifications;
        }

        // GET: /Admin
        [HttpGet]
        public IActionResult Index() => RedirectToAction(nameof(Dashboard));

        // Dashboard

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var monthStartUtc = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).ToUniversalTime();

            var paidBookings = await _context.Bookings
                .Where(b => b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Completed)
                .Select(b => new { b.TotalPrice, b.ServiceFee })
                .ToListAsync();

            var model = new AdminDashboardViewModel
            {
                TotalUsers = await _context.Users.CountAsync(),
                NewUsersThisMonth = await _context.Users.CountAsync(u => u.CreatedAt >= monthStartUtc),
                PendingHostApplications = await _context.HostApplications.CountAsync(a => a.Status == ApplicationStatus.Pending),
                ActiveProperties = await _context.HostProperties.CountAsync(p => p.IsActive),
                TotalProperties = await _context.HostProperties.CountAsync(),
                PlatformRevenue = paidBookings.Sum(b => b.ServiceFee),
                GrossBookingValue = paidBookings.Sum(b => b.TotalPrice),
                TotalBookings = await _context.Bookings.CountAsync(),
                PendingBookings = await _context.Bookings.CountAsync(b => b.Status == BookingStatus.Pending),
                ApprovedHosts = (await _userManager.GetUsersInRoleAsync(Roles.Host)).Count,
                GuestCount = (await _userManager.GetUsersInRoleAsync(Roles.Guest)).Count,
                AdminCount = (await _userManager.GetUsersInRoleAsync(Roles.Admin)).Count
                             + (await _userManager.GetUsersInRoleAsync(Roles.SuperAdmin)).Count,

                RecentActivity = await _context.ActivityLogs.AsNoTracking()
                    .Include(l => l.User)
                    .OrderByDescending(l => l.CreatedAt).Take(8).ToListAsync(),
                RecentApplications = await _context.HostApplications.AsNoTracking()
                    .Include(a => a.Applicant).Include(a => a.Property)
                    .OrderByDescending(a => a.AppliedAt).Take(5).ToListAsync(),
                RecentBookings = await _context.Bookings.AsNoTracking()
                    .Include(b => b.Guest).Include(b => b.Property).Include(b => b.Payment)
                    .OrderByDescending(b => b.CreatedAt).Take(5).ToListAsync()
            };

            return View(model);
        }

        // Application review

        // GET: /Admin/HostRequests?filter=pending&search=cape
        //      filter = pending (default) | approved | rejected | all
        [HttpGet]
        public async Task<IActionResult> HostRequests(string? filter = "pending", string? search = null)
        {
            ApplicationStatus? status = filter?.ToLowerInvariant() switch
            {
                "all" => null,
                "approved" => ApplicationStatus.Approved,
                "rejected" => ApplicationStatus.Rejected,
                _ => ApplicationStatus.Pending
            };

            var query = _context.HostApplications
                .AsNoTracking()
                .Include(a => a.Applicant)
                .Include(a => a.Property).ThenInclude(p => p.Images)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(a => a.Status == status.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(a =>
                    a.Property.Title.Contains(term) ||
                    a.Property.City.Contains(term) ||
                    a.Applicant.FirstName.Contains(term) ||
                    a.Applicant.LastName.Contains(term) ||
                    a.Applicant.Email!.Contains(term));
            }

            var counts = await _context.HostApplications
                .GroupBy(a => a.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var model = new HostRequestsViewModel
            {
                StatusFilter = status,
                Search = search,
                // Oldest pending first (first come, first served); otherwise newest first
                Applications = status == ApplicationStatus.Pending
                    ? await query.OrderBy(a => a.AppliedAt).ToListAsync()
                    : await query.OrderByDescending(a => a.AppliedAt).ToListAsync(),
                PendingCount = counts.FirstOrDefault(c => c.Status == ApplicationStatus.Pending)?.Count ?? 0,
                ApprovedCount = counts.FirstOrDefault(c => c.Status == ApplicationStatus.Approved)?.Count ?? 0,
                RejectedCount = counts.FirstOrDefault(c => c.Status == ApplicationStatus.Rejected)?.Count ?? 0
            };

            return View(model);
        }

        // GET: /Admin/ReviewApplication/5
        [HttpGet]
        public async Task<IActionResult> ReviewApplication(int id)
        {
            var application = await _context.HostApplications
                .AsNoTracking()
                .Include(a => a.Applicant)
                .Include(a => a.ReviewedBy)
                .Include(a => a.Property).ThenInclude(p => p.Images)
                .Include(a => a.Property).ThenInclude(p => p.Amenities)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (application == null)
                return NotFound();

            var model = new ReviewApplicationViewModel
            {
                Application = application,
                ApplicantRoles = await _userManager.GetRolesAsync(application.Applicant),
                ApplicantActiveListings = await _context.HostProperties
                    .CountAsync(p => p.HostId == application.ApplicationUserId && p.IsActive)
            };

            return View(model);
        }

        // POST: /Admin/ApproveApplication/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveApplication(int id, string? adminNotes)
        {
            var adminId = _userManager.GetUserId(User)!;
            var result = await _applications.ApproveAsync(id, adminId, adminNotes);

            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return result.Succeeded
                ? RedirectToAction(nameof(HostRequests))
                : RedirectToAction(nameof(ReviewApplication), new { id });
        }

        // POST: /Admin/RejectApplication/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectApplication(int id, string reason)
        {
            var adminId = _userManager.GetUserId(User)!;
            var result = await _applications.RejectAsync(id, adminId, reason);

            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return result.Succeeded
                ? RedirectToAction(nameof(HostRequests))
                : RedirectToAction(nameof(ReviewApplication), new { id });
        }
            // Bookings overview (read-only)

        // GET: /Admin/Bookings?status=Pending&search=durban
        [HttpGet]
        public async Task<IActionResult> Bookings(BookingStatus? status, string? search)
        {
            var query = _context.Bookings.AsNoTracking()
                .Include(b => b.Guest)
                .Include(b => b.Payment)
                .Include(b => b.Property).ThenInclude(p => p.Host)
                .AsQueryable();

            if (status.HasValue)
                query = query.Where(b => b.Status == status.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(b =>
                    b.Property.Title.Contains(term) || b.Property.City.Contains(term) ||
                    b.Guest.FirstName.Contains(term) || b.Guest.LastName.Contains(term) || b.Guest.Email!.Contains(term) ||
                    b.Property.Host.FirstName.Contains(term) || b.Property.Host.LastName.Contains(term));
            }

            var counts = await _context.Bookings
                .GroupBy(b => b.Status)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync();

            return View(new AdminBookingsViewModel
            {
                Status = status,
                Search = search,
                Bookings = await query.OrderByDescending(b => b.CreatedAt).Take(200).ToListAsync(),
                Counts = counts.ToDictionary(c => c.Key, c => c.Count)
            });
        }

        // Properties overview + quality control (suspend / reactivate)

        // GET: /Admin/Properties?filter=active&search=cabin
        [HttpGet]
        public async Task<IActionResult> Properties(string filter = "all", string? search = null)
        {
            filter = (filter ?? "all").ToLowerInvariant();
            var query = _context.HostProperties.AsNoTracking().AsQueryable();

            query = filter switch
            {
                "active" => query.Where(p => p.IsActive),
                "inactive" => query.Where(p => !p.IsActive),
                "pending" => query.Where(p => p.HostApplications.Any(a => a.Status == ApplicationStatus.Pending)),
                _ => query
            };

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(p => p.Title.Contains(term) || p.City.Contains(term)
                                         || p.Host.FirstName.Contains(term) || p.Host.LastName.Contains(term) || p.Host.Email!.Contains(term));
            }

            var rows = await query
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new AdminPropertyRow
                {
                    Id = p.Id,
                    Title = p.Title,
                    City = p.City,
                    PropertyType = p.PropertyType,
                    PricePerNight = p.PricePerNight,
                    IsActive = p.IsActive,
                    HostName = p.Host.FirstName + " " + p.Host.LastName,
                    HostEmail = p.Host.Email ?? "",
                    CoverImageUrl = p.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault(),
                    BookingCount = p.Bookings.Count,
                    ApplicationStatus = p.HostApplications.OrderByDescending(a => a.AppliedAt).Select(a => (ApplicationStatus?)a.Status).FirstOrDefault(),
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            return View(new AdminPropertiesViewModel { Filter = filter, Search = search, Properties = rows });
        }

        // POST: /Admin/ToggleProperty/5  (suspend a listing that breaks the rules, or reactivate it)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleProperty(int id, string? reason, string? returnFilter)
        {
            var property = await _context.HostProperties
                .Include(p => p.HostApplications)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (property == null) return NotFound();

            var latest = property.HostApplications.OrderByDescending(a => a.AppliedAt).FirstOrDefault();
            if (!property.IsActive && latest != null && latest.Status != ApplicationStatus.Approved)
            {
                TempData["Error"] = "Approve the host application first - that activates the listing.";
                return RedirectToAction(nameof(Properties), new { filter = returnFilter });
            }

            property.IsActive = !property.IsActive;
            property.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var adminId = _userManager.GetUserId(User)!;
            var reasonText = string.IsNullOrWhiteSpace(reason) ? "" : $" Reason: {reason.Trim()}";
            await _activityLog.LogAsync(adminId,
                property.IsActive ? ActivityActions.PropertyActivated : ActivityActions.PropertyDeactivated,
                ActivityType.PropertyUpdate,
                $"Admin {(property.IsActive ? "reactivated" : "suspended")} '{property.Title}' (#{property.Id}).{reasonText}");

            await _notifications.CreateAsync(property.HostId,
                property.IsActive ? "Listing Reactivated" : "Listing Suspended",
                property.IsActive
                    ? $"An administrator reactivated '{property.Title}'. It is visible to guests again."
                    : $"An administrator suspended '{property.Title}', so it is hidden from guests.{reasonText}",
                NotificationType.General,
                "/Host/Properties");

            TempData["Success"] = $"'{property.Title}' is now {(property.IsActive ? "active" : "suspended")}.";
            return RedirectToAction(nameof(Properties), new { filter = returnFilter });
        }

        // System audit (ActivityLog)

        // GET: /Admin/ActivityLogs?type=Login&search=elsie&from=2026-01-01&to=2026-01-31&page=2
        [HttpGet]
        public async Task<IActionResult> ActivityLogs(ActivityType? type, string? search, DateTime? from, DateTime? to, int page = 1)
        {
            var query = FilterLogs(type, search, from, to);

            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)LogsPageSize));
            page = Math.Clamp(page, 1, totalPages);

            return View(new ActivityLogsViewModel
            {
                Type = type,
                Search = search,
                From = from,
                To = to,
                Page = page,
                TotalPages = totalPages,
                TotalCount = total,
                Logs = await query.OrderByDescending(l => l.CreatedAt)
                    .Skip((page - 1) * LogsPageSize).Take(LogsPageSize).ToListAsync()
            });
        }

        // GET: /Admin/ExportActivityLogs?...same filters...  -> CSV download
        [HttpGet]
        public async Task<IActionResult> ExportActivityLogs(ActivityType? type, string? search, DateTime? from, DateTime? to)
        {
            var logs = await FilterLogs(type, search, from, to)
                .OrderByDescending(l => l.CreatedAt)
                .Take(10_000)
                .ToListAsync();

            static string Csv(string? value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";

            var csv = new StringBuilder();
            csv.AppendLine("Date,User,Email,Type,Action,Details,IP Address");
            foreach (var l in logs)
            {
                csv.AppendLine(string.Join(",",
                    Csv(l.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
                    Csv(l.User?.FullName ?? "System"),
                    Csv(l.User?.Email),
                    Csv(l.ActivityType.ToString()),
                    Csv(l.Action),
                    Csv(l.Details),
                    Csv(l.IpAddress)));
            }

            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(bytes, "text/csv", $"staybnb-activity-log-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        }

        private IQueryable<ActivityLog> FilterLogs(ActivityType? type, string? search, DateTime? from, DateTime? to)
        {
            var query = _context.ActivityLogs.AsNoTracking().Include(l => l.User).AsQueryable();

            if (type.HasValue)
                query = query.Where(l => l.ActivityType == type.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(l => l.Action.Contains(term)
                                         || (l.Details != null && l.Details.Contains(term))
                                         || (l.User != null && (l.User.FirstName.Contains(term) || l.User.LastName.Contains(term) || l.User.Email!.Contains(term))));
            }

            if (from.HasValue)
                query = query.Where(l => l.CreatedAt >= from.Value.Date.ToUniversalTime());

            if (to.HasValue)
                query = query.Where(l => l.CreatedAt < to.Value.Date.AddDays(1).ToUniversalTime());

            return query;
        }
    }
}
