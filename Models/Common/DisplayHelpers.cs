using Staybnb.Models.Entities;
using Staybnb.Models.Enums;

namespace Staybnb.Models.Common
{
    /// <summary>Small formatting helpers used by several views.</summary>
    public static class DisplayHelpers
    {
        /// <summary>"just now", "5m ago", "3h ago", "2d ago", or a date for older items. Input is UTC.</summary>
        public static string TimeAgo(DateTime utc)
        {
            var span = DateTime.UtcNow - utc;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return utc.ToLocalTime().ToString("dd MMM yyyy");
        }

        public static string Initials(ApplicationUser? user) =>
            user == null ? "?" : $"{user.FirstName.FirstOrDefault()}{user.LastName.FirstOrDefault()}".ToUpper();

        /// <summary>Bootstrap Icons class + colour for each notification type.</summary>
        public static (string Icon, string Css) NotificationIcon(NotificationType type) => type switch
        {
            NotificationType.ApplicationApproved or NotificationType.BookingApproved or NotificationType.DocumentVerified
                => ("bi-check-circle-fill", "text-success"),
            NotificationType.ApplicationRejected or NotificationType.BookingRejected or NotificationType.DocumentRejected or NotificationType.BookingCancelled
                => ("bi-x-circle-fill", "text-danger"),
            NotificationType.ApplicationSubmitted => ("bi-clipboard-plus", "text-primary"),
            NotificationType.BookingRequested => ("bi-calendar-plus", "text-primary"),
            NotificationType.CheckInSubmitted => ("bi-person-vcard", "text-primary"),
            NotificationType.NewMessage => ("bi-chat-dots-fill", "text-primary"),
            NotificationType.RoleChanged => ("bi-shield-check", "text-success"),
            _ => ("bi-bell-fill", "text-secondary")
        };
    }
}
