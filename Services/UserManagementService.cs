using Microsoft.AspNetCore.Identity;
using Staybnb.Data;
using Staybnb.Models.Common;
using Staybnb.Models.Constants;
using Staybnb.Models.Entities;
using Staybnb.Models.Enums;
using Staybnb.Services.Interfaces;

namespace Staybnb.Services
{
    public class UserManagementService : IUserManagementService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogService _activityLog;
        private readonly INotificationService _notifications;
        private readonly ILogger<UserManagementService> _logger;

        public UserManagementService(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IActivityLogService activityLog,
            INotificationService notifications,
            ILogger<UserManagementService> logger)
        {
            _context = context;
            _userManager = userManager;
            _activityLog = activityLog;
            _notifications = notifications;
            _logger = logger;
        }

        // Promote: Guest -> Admin
        public async Task<ServiceResult> PromoteToAdminAsync(string userId, string superAdminId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ServiceResult.Fail("User not found.");

            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains(Roles.SuperAdmin) || roles.Contains(Roles.Admin))
                return ServiceResult.Fail($"{user.FullName} is already an administrator.");

            // Only Guests can be promoted
            if (!roles.Contains(Roles.Guest))
                return ServiceResult.Fail($"Only Guests can be promoted. {user.FullName} is a {string.Join("/", roles)}.");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var add = await _userManager.AddToRoleAsync(user, Roles.Admin);
                if (!add.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult.Fail("Could not add the Admin role: " + JoinErrors(add));
                }

                // An administrator manages the marketplace and does not book stays
                var remove = await _userManager.RemoveFromRoleAsync(user, Roles.Guest);
                if (!remove.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult.Fail("Could not remove the Guest role: " + JoinErrors(remove));
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Promoting {UserId} to Admin failed", userId);
                return ServiceResult.Fail("Something went wrong. No changes were made.");
            }

            await _activityLog.LogAsync(superAdminId, ActivityActions.PromotedToAdmin, ActivityType.RoleChange,
                $"{user.FullName} ({user.Email}) promoted from '{Roles.Guest}' to '{Roles.Admin}'.");

            await _notifications.CreateAsync(user.Id,
                "You're now a Staybnb Admin",
                "A Super Administrator has given you the Admin role. You can now review host applications and monitor the system.",
                NotificationType.RoleChanged,
                "/Admin/Dashboard");

            return ServiceResult.Ok($"{user.FullName} is now an Admin.");
        }

        // Revoke: Admin -> Guest
        public async Task<ServiceResult> RevokeAdminAsync(string userId, string superAdminId)
        {
            if (userId == superAdminId)
                return ServiceResult.Fail("You can't change your own role.");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return ServiceResult.Fail("User not found.");

            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains(Roles.SuperAdmin))
                return ServiceResult.Fail("The SuperAdmin account can't be changed.");

            if (!roles.Contains(Roles.Admin))
                return ServiceResult.Fail($"{user.FullName} is not an Admin.");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var remove = await _userManager.RemoveFromRoleAsync(user, Roles.Admin);
                var add = remove.Succeeded ? await _userManager.AddToRoleAsync(user, Roles.Guest) : remove;
                if (!remove.Succeeded || !add.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult.Fail("Could not change roles: " + JoinErrors(remove.Succeeded ? add : remove));
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Revoking Admin from {UserId} failed", userId);
                return ServiceResult.Fail("Something went wrong. No changes were made.");
            }

            await _activityLog.LogAsync(superAdminId, ActivityActions.RoleRemoved, ActivityType.RoleChange,
                $"{user.FullName} ({user.Email}): role '{Roles.Admin}' removed, back to '{Roles.Guest}'.");

            await _notifications.CreateAsync(user.Id,
                "Admin Access Removed",
                "Your Admin role has been removed. Your account is a Guest account again.",
                NotificationType.RoleChanged,
                "/Properties");

            return ServiceResult.Ok($"{user.FullName} is a Guest again.");
        }

        private static string JoinErrors(IdentityResult result) =>
            string.Join("; ", result.Errors.Select(e => e.Description));
    }
}
