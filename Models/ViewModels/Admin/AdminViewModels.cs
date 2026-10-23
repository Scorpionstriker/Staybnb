using Staybnb.Models.Entities;
using Staybnb.Models.Enums;

namespace Staybnb.Models.ViewModels.Admin
{
    // =====================================================================
    // DASHBOARD
    // =====================================================================
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int NewUsersThisMonth { get; set; }
        public int PendingHostApplications { get; set; }
        public int ActiveProperties { get; set; }
        public int TotalProperties { get; set; }

        /// <summary>Staybnb's income: the service fees of paid bookings.</summary>
        public decimal PlatformRevenue { get; set; }

        /// <summary>Everything guests paid for paid bookings.</summary>
        public decimal GrossBookingValue { get; set; }

        public int TotalBookings { get; set; }
        public int PendingBookings { get; set; }
        public int ApprovedHosts { get; set; }
        public int GuestCount { get; set; }
        public int AdminCount { get; set; }

        public List<ActivityLog> RecentActivity { get; set; } = new();
        public List<HostApplication> RecentApplications { get; set; } = new();
        public List<Booking> RecentBookings { get; set; } = new();
    }

    // =====================================================================
    // BOOKINGS + PROPERTIES OVERVIEW
    // =====================================================================
    public class AdminBookingsViewModel
    {
        public BookingStatus? Status { get; set; }
        public string? Search { get; set; }
        public List<Booking> Bookings { get; set; } = new();
        public Dictionary<BookingStatus, int> Counts { get; set; } = new();
    }

    public class AdminPropertiesViewModel
    {
        /// <summary>all | active | inactive | pending</summary>
        public string Filter { get; set; } = "all";
        public string? Search { get; set; }
        public List<AdminPropertyRow> Properties { get; set; } = new();
    }

    public class AdminPropertyRow
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public PropertyType PropertyType { get; set; }
        public decimal PricePerNight { get; set; }
        public bool IsActive { get; set; }
        public string HostName { get; set; } = string.Empty;
        public string HostEmail { get; set; } = string.Empty;
        public string? CoverImageUrl { get; set; }
        public int BookingCount { get; set; }
        public ApplicationStatus? ApplicationStatus { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // =====================================================================
    // SYSTEM AUDIT (ActivityLog)
    // =====================================================================
    public class ActivityLogsViewModel
    {
        public ActivityType? Type { get; set; }
        public string? Search { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public List<ActivityLog> Logs { get; set; } = new();
    }

    // =====================================================================
    // USER MANAGEMENT (SuperAdmin)
    // =====================================================================
    public class UserManagementViewModel
    {
        /// <summary>all | guest | host | admin</summary>
        public string Role { get; set; } = "guest";
        public string? Search { get; set; }
        public List<UserRow> Users { get; set; } = new();
        public int GuestCount { get; set; }
        public int HostCount { get; set; }
        public int AdminCount { get; set; }
        public int AllCount { get; set; }
    }

    public class UserRow
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> Roles { get; set; } = new();
        public int BookingCount { get; set; }
        public int ListingCount { get; set; }
        public bool IsLockedOut { get; set; }

        public string Initials =>
            string.Concat(FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => p[0])).ToUpper();
    }
}
