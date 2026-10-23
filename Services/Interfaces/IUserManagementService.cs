using Staybnb.Models.Common;

namespace Staybnb.Services.Interfaces
{
    /// <summary>
    /// SuperAdmin-only role management.
    /// Promote: Guest -> Admin (Guest role removed).  Revoke: Admin -> Guest.
    /// </summary>
    public interface IUserManagementService
    {
        Task<ServiceResult> PromoteToAdminAsync(string userId, string superAdminId);

        Task<ServiceResult> RevokeAdminAsync(string userId, string superAdminId);
    }
}
