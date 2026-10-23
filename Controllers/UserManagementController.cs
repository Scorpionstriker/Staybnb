using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models.Constants;
using Staybnb.Models.Entities;
using Staybnb.Models.ViewModels.Admin;
using Staybnb.Services.Interfaces;

namespace Staybnb.Controllers
{
    /// <summary>
    /// User Management - SUPERADMIN ONLY.
    /// Find existing Guests and promote them to the Admin role (or revoke it again).
    /// Normal Admins get Access Denied here.
    /// </summary>
    [Authorize(Roles = Roles.SuperAdmin)]
    public class UserManagementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserManagementService _users;

        public UserManagementController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IUserManagementService users)
        {
            _context = context;
            _userManager = userManager;
            _users = users;
        }

        // GET: /UserManagement?role=guest&search=thabo
        [HttpGet]
        public async Task<IActionResult> Index(string role = "guest", string? search = null)
        {
            role = (role ?? "guest").ToLowerInvariant();

            // Every user's role names in one query
            var userRoles = await (from ur in _context.UserRoles
                                   join r in _context.Roles on ur.RoleId equals r.Id
                                   select new { ur.UserId, RoleName = r.Name! })
                                  .ToListAsync();
            var rolesByUser = userRoles
                .GroupBy(x => x.UserId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).OrderBy(n => n).ToList());

            var usersQuery = _context.Users.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                usersQuery = usersQuery.Where(u => u.FirstName.Contains(term) || u.LastName.Contains(term)
                                                   || u.Email!.Contains(term) || (u.PhoneNumber != null && u.PhoneNumber.Contains(term)));
            }

            var now = DateTimeOffset.UtcNow;
            var allRows = (await usersQuery
                    .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                    .Select(u => new UserRow
                    {
                        Id = u.Id,
                        FullName = u.FirstName + " " + u.LastName,
                        Email = u.Email ?? "",
                        PhoneNumber = u.PhoneNumber,
                        CreatedAt = u.CreatedAt,
                        BookingCount = u.Bookings.Count,
                        ListingCount = u.Properties.Count,
                        IsLockedOut = u.LockoutEnd != null && u.LockoutEnd > now
                    })
                    .ToListAsync())
                .Select(r => { r.Roles = rolesByUser.TryGetValue(r.Id, out var list) ? list : new List<string>(); return r; })
                .ToList();

            bool IsAdmin(UserRow r) => r.Roles.Contains(Roles.Admin) || r.Roles.Contains(Roles.SuperAdmin);

            var model = new UserManagementViewModel
            {
                Role = role,
                Search = search,
                AllCount = allRows.Count,
                GuestCount = allRows.Count(r => r.Roles.Contains(Roles.Guest)),
                HostCount = allRows.Count(r => r.Roles.Contains(Roles.Host)),
                AdminCount = allRows.Count(IsAdmin),
                Users = role switch
                {
                    "host" => allRows.Where(r => r.Roles.Contains(Roles.Host)).ToList(),
                    "admin" => allRows.Where(IsAdmin).ToList(),
                    "all" => allRows,
                    _ => allRows.Where(r => r.Roles.Contains(Roles.Guest)).ToList()
                }
            };

            return View(model);
        }

        // POST: /UserManagement/Promote
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Promote(string userId, string? role, string? search)
        {
            var result = await _users.PromoteToAdminAsync(userId, _userManager.GetUserId(User)!);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index), new { role, search });
        }

        // POST: /UserManagement/Revoke
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revoke(string userId, string? role, string? search)
        {
            var result = await _users.RevokeAdminAsync(userId, _userManager.GetUserId(User)!);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Index), new { role, search });
        }
    }
}
