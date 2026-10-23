using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models.Entities;
using Staybnb.Models.Enums;
using Staybnb.Services.Interfaces;

namespace Staybnb.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // ---------------------------------------------------------------------
        // CREATE
        // ---------------------------------------------------------------------

        public async Task CreateAsync(string userId, string title, string message, NotificationType type, string? linkUrl = null)
        {
            if (string.IsNullOrWhiteSpace(userId)) return;

            var notification = Build(userId, title, message, type, linkUrl);
            await SaveSafelyAsync(new[] { notification });
        }

        public async Task NotifyRoleAsync(string roleName, string title, string message, NotificationType type, string? linkUrl = null)
        {
            var users = await _userManager.GetUsersInRoleAsync(roleName);
            if (users.Count == 0) return;

            var notifications = users.Select(u => Build(u.Id, title, message, type, linkUrl)).ToList();
            await SaveSafelyAsync(notifications);
        }

        // ---------------------------------------------------------------------
        // READ
        // ---------------------------------------------------------------------

        public Task<int> GetUnreadCountAsync(string userId) =>
            _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

        public Task<List<Notification>> GetForUserAsync(string userId, bool unreadOnly = false, int take = 100)
        {
            var query = _context.Notifications.AsNoTracking().Where(n => n.UserId == userId);

            if (unreadOnly)
                query = query.Where(n => !n.IsRead);

            return query.OrderByDescending(n => n.CreatedAt).Take(take).ToListAsync();
        }

        public Task<Notification?> GetAsync(int id, string userId) =>
            _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        // ---------------------------------------------------------------------
        // UPDATE (Read / Unread is marked in the rubric - these write to the database)
        // ---------------------------------------------------------------------

        public async Task<bool> MarkAsReadAsync(int id, string userId)
        {
            var notification = await GetAsync(id, userId);
            if (notification == null) return false;

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            return true;
        }

        public async Task<bool> MarkAsUnreadAsync(int id, string userId)
        {
            var notification = await GetAsync(id, userId);
            if (notification == null) return false;

            notification.IsRead = false;
            notification.ReadAt = null;
            await _context.SaveChangesAsync();
            return true;
        }

        public Task MarkReadByLinkAsync(string userId, string linkUrl)
        {
            var now = DateTime.UtcNow;
            return _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead && n.LinkUrl == linkUrl)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, now));
        }

        public Task<int> MarkAllAsReadAsync(string userId)
        {
            // ExecuteUpdateAsync runs a single UPDATE statement in SQL (EF Core 7+)
            var now = DateTime.UtcNow;
            return _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, now));
        }

        // ---------------------------------------------------------------------
        // DELETE
        // ---------------------------------------------------------------------

        public async Task<bool> DeleteAsync(int id, string userId)
        {
            var notification = await GetAsync(id, userId);
            if (notification == null) return false;

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();
            return true;
        }

        // ---------------------------------------------------------------------
        // HELPERS
        // ---------------------------------------------------------------------

        private static Notification Build(string userId, string title, string message, NotificationType type, string? linkUrl) => new()
        {
            UserId = userId,
            Title = Truncate(title, 100),
            Message = Truncate(message, 500),
            Type = type,
            LinkUrl = linkUrl == null ? null : Truncate(linkUrl, 300),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        /// <summary>
        /// A failed notification must not undo the main action (e.g. a booking approval),
        /// so errors are logged and the failed rows are detached instead of thrown.
        /// </summary>
        private async Task SaveSafelyAsync(IEnumerable<Notification> notifications)
        {
            var list = notifications.ToList();
            try
            {
                _context.Notifications.AddRange(list);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                foreach (var n in list)
                    _context.Entry(n).State = EntityState.Detached;

                _logger.LogError(ex, "Failed to save {Count} notification(s)", list.Count);
            }
        }

        private static string Truncate(string value, int maxLength) =>
            value.Length <= maxLength ? value : value[..maxLength];
    }
}
