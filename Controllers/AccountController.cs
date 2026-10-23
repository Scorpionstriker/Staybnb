using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Models.Constants;
using Staybnb.Models.Entities;
using Staybnb.Models.Enums;
using Staybnb.Models.ViewModels.Account;
using Staybnb.Services.Interfaces;

namespace Staybnb.Controllers
{
    /// <summary>
    /// Registration, login and logout.
    /// Every new registrant is automatically assigned the Guest role.
    /// Every login (successful or failed) and logout is written to the ActivityLog.
    /// </summary>
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IActivityLogService _activityLog;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IActivityLogService activityLog,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _activityLog = activityLog;
            _logger = logger;
        }

        // Register

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (_signInManager.IsSignedIn(User))
                return RedirectToAction("Index", "Home");

            return View(new RegisterViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email.Trim(),
                Email = model.Email.Trim(),
                EmailConfirmed = true, // no email server in this project
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    // Show "email already taken" next to the Email field, everything else at the top
                    var key = error.Code is "DuplicateUserName" or "DuplicateEmail" ? nameof(model.Email) : string.Empty;
                    var message = error.Code == "DuplicateUserName"
                        ? "An account with this email address already exists."
                        : error.Description;
                    ModelState.AddModelError(key, message);
                }
                return View(model);
            }

            // Everyone starts out as a Guest
            var roleResult = await _userManager.AddToRoleAsync(user, Roles.Guest);
            if (!roleResult.Succeeded)
            {
                _logger.LogError("Could not assign Guest role to {Email}", user.Email);
            }

            await _activityLog.LogAsync(user.Id, ActivityActions.UserRegistered, ActivityType.Registration,
                $"{user.FullName} ({user.Email}) registered and was assigned the '{Roles.Guest}' role.");

            // Sign the new user straight in (after the role is added, so the cookie contains it)
            await _signInManager.SignInAsync(user, isPersistent: false);
            await _activityLog.LogAsync(user.Id, ActivityActions.UserLoggedIn, ActivityType.Login,
                "Signed in automatically after registration.");

            TempData["Success"] = $"Welcome to Staybnb, {user.FirstName}!";
            return RedirectToAction("Index", "Home");
        }

        // Login

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (_signInManager.IsSignedIn(User))
                return RedirectToAction("Index", "Home");

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email.Trim());
            if (user == null)
            {
                await _activityLog.LogAsync(null, ActivityActions.LoginFailed, ActivityType.FailedLogin,
                    $"Login attempt for unknown email '{model.Email}'.");
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                await _activityLog.LogAsync(user.Id, ActivityActions.UserLoggedIn, ActivityType.Login,
                    $"{user.FullName} logged in.");

                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    return LocalRedirect(model.ReturnUrl);

                return await RedirectToRoleHomeAsync(user);
            }

            if (result.IsLockedOut)
            {
                await _activityLog.LogAsync(user.Id, ActivityActions.AccountLockedOut, ActivityType.FailedLogin,
                    $"Account locked after too many failed login attempts.");
                ModelState.AddModelError(string.Empty,
                    "This account is temporarily locked because of too many failed attempts. Try again in 5 minutes.");
                return View(model);
            }

            await _activityLog.LogAsync(user.Id, ActivityActions.LoginFailed, ActivityType.FailedLogin,
                "Incorrect password entered.");
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        // Logout

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var userId = _userManager.GetUserId(User);
            await _signInManager.SignOutAsync();

            await _activityLog.LogAsync(userId, ActivityActions.UserLoggedOut, ActivityType.Logout, "User logged out.");

            return RedirectToAction("Index", "Home");
        }

        // Access denied (shown when a role tries to open another role's page)

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // Helpers

        /// <summary>Sends each role to its own start page after login.</summary>
        private async Task<IActionResult> RedirectToRoleHomeAsync(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains(Roles.SuperAdmin) || roles.Contains(Roles.Admin))
                return RedirectToAction("Dashboard", "Admin");

            if (roles.Contains(Roles.Host))
                return RedirectToAction("Dashboard", "Host");

            return RedirectToAction("Index", "Home");        // guests land on the property gallery
        }
    }
}
