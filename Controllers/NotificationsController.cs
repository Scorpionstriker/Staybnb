using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Models.Entities;
using Staybnb.Models.ViewModels.Shared;
using Staybnb.Services.Interfaces;

namespace Staybnb.Controllers
{
    /// <summary>
    /// The bell-icon notification centre. Opening a notification marks it READ in the database;
    /// users can also mark it unread again, mark all as read, or delete it.
    /// </summary>
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notifications;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsController(INotificationService notifications, UserManager<ApplicationUser> userManager)
        {
            _notifications = notifications;
            _userManager = userManager;
        }

        private string UserId => _userManager.GetUserId(User)!;

        // GET: /Notifications?filter=unread&selectedId=61
        [HttpGet]
        public async Task<IActionResult> Index(string filter = "all", int? selectedId = null)
        {
            var userId = UserId;
            filter = (filter ?? "all").ToLowerInvariant();

            // Opening a notification marks it as read
            if (selectedId.HasValue)
                await _notifications.MarkAsReadAsync(selectedId.Value, userId);

            var all = await _notifications.GetForUserAsync(userId, take: 200);
            var todayUtc = DateTime.Today.ToUniversalTime();

            var filtered = filter switch
            {
                "unread" => all.Where(n => !n.IsRead),
                "read" => all.Where(n => n.IsRead),
                "today" => all.Where(n => n.CreatedAt >= todayUtc),
                _ => all
            };

            var model = new NotificationCenterViewModel
            {
                Filter = filter,
                Notifications = filtered.ToList(),
                Selected = selectedId.HasValue ? all.FirstOrDefault(n => n.Id == selectedId.Value) : null,
                AllCount = all.Count,
                UnreadCount = all.Count(n => !n.IsRead),
                ReadCount = all.Count(n => n.IsRead),
                TodayCount = all.Count(n => n.CreatedAt >= todayUtc)
            };

            return View(model);
        }

        // GET: /Notifications/Open/61 -> marks read and follows the link ("Take Action")
        [HttpGet]
        public async Task<IActionResult> Open(int id)
        {
            var notification = await _notifications.GetAsync(id, UserId);
            if (notification == null) return NotFound();

            await _notifications.MarkAsReadAsync(id, UserId);

            if (!string.IsNullOrEmpty(notification.LinkUrl) && Url.IsLocalUrl(notification.LinkUrl))
                return LocalRedirect(notification.LinkUrl);

            return RedirectToAction(nameof(Index), new { selectedId = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id, string? filter)
        {
            await _notifications.MarkAsReadAsync(id, UserId);
            return RedirectToAction(nameof(Index), new { filter, selectedId = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkUnread(int id, string? filter)
        {
            await _notifications.MarkAsUnreadAsync(id, UserId);
            TempData["Info"] = "Marked as unread.";
            return RedirectToAction(nameof(Index), new { filter });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead(string? filter)
        {
            var count = await _notifications.MarkAllAsReadAsync(UserId);
            TempData["Success"] = count == 0 ? "You're all caught up." : $"{count} notification(s) marked as read.";
            return RedirectToAction(nameof(Index), new { filter });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string? filter)
        {
            var deleted = await _notifications.DeleteAsync(id, UserId);
            TempData[deleted ? "Success" : "Error"] = deleted ? "Notification deleted." : "Notification not found.";
            return RedirectToAction(nameof(Index), new { filter });
        }
    }
}
