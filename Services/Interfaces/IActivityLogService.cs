using Staybnb.Models.Enums;

namespace Staybnb.Services.Interfaces
{
    /// <summary>
    /// Writes entries to the ActivityLog table (the Admin "System Audit").
    /// Inject it into any controller or service that performs a significant action.
    /// </summary>
    public interface IActivityLogService
    {
        /// <param name="userId">The user who performed the action (null for system events).</param>
        /// <param name="action">Use a constant from ActivityActions, e.g. ActivityActions.UserLoggedIn.</param>
        /// <param name="type">Category used for filtering on the audit page.</param>
        /// <param name="details">Optional human-readable description.</param>
        Task LogAsync(string? userId, string action, ActivityType type, string? details = null);
    }
}
