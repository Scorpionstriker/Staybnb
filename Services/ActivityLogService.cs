using Staybnb.Data;
using Staybnb.Models.Entities;
using Staybnb.Models.Enums;
using Staybnb.Services.Interfaces;

namespace Staybnb.Services
{
    public class ActivityLogService : IActivityLogService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<ActivityLogService> _logger;

        public ActivityLogService(
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<ActivityLogService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public async Task LogAsync(string? userId, string action, ActivityType type, string? details = null)
        {
            var log = new ActivityLog
            {
                UserId = userId,
                Action = Truncate(action, 100)!,
                ActivityType = type,
                Details = Truncate(details, 500),
                IpAddress = Truncate(_httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(), 45),
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                _context.ActivityLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Logging must never break the user's actual action (e.g. logging in).
                // Detach the failed entry so it is not retried by the next SaveChanges.
                _context.Entry(log).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                _logger.LogError(ex, "Failed to write activity log entry {Action} for user {UserId}", action, userId);
            }
        }

        private static string? Truncate(string? value, int maxLength) =>
            value == null || value.Length <= maxLength ? value : value[..maxLength];
    }
}
