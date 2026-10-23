using Staybnb.Models.Entities;
using Staybnb.Models.Enums;

namespace Staybnb.Services.Interfaces
{
    /// <summary>
    /// Creates and manages the bell-icon notifications ("Booking Approved", "New Message"...).
    /// All read/write methods take the userId so a user can only touch their own notifications.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>Send a notification to one user.</summary>
        Task CreateAsync(string userId, string title, string message, NotificationType type, string? linkUrl = null);

        /// <summary>Send the same notification to every user in a role (e.g. all Admins).</summary>
        Task NotifyRoleAsync(string roleName, string title, string message, NotificationType type, string? linkUrl = null);

        Task<int> GetUnreadCountAsync(string userId);

        /// <summary>Newest first.</summary>
        Task<List<Notification>> GetForUserAsync(string userId, bool unreadOnly = false, int take = 100);

        Task<Notification?> GetAsync(int id, string userId);

        Task<bool> MarkAsReadAsync(int id, string userId);

        Task<bool> MarkAsUnreadAsync(int id, string userId);

        Task<int> MarkAllAsReadAsync(string userId);

        /// <summary>Marks every unread notification that links to <paramref name="linkUrl"/> as read.</summary>
        Task MarkReadByLinkAsync(string userId, string linkUrl);

        Task<bool> DeleteAsync(int id, string userId);
    }
}
