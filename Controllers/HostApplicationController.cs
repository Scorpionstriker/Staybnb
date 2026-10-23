using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Staybnb.Data;
using Staybnb.Models.Constants;
using Staybnb.Models.Entities;
using Staybnb.Models.Settings;
using Staybnb.Models.ViewModels.Host;
using Staybnb.Services.Interfaces;

namespace Staybnb.Controllers
{
    /// <summary>
    /// "Become a Host" (for Guests) and "Add another property" (for existing Hosts),
    /// plus the applicant's "Host Application Status" page.
    /// </summary>
    [Authorize(Roles = Roles.Guest + "," + Roles.Host)]
    public class HostApplicationController : Controller
    {
        private const long MaxUploadBytes = 60L * 1024 * 1024;

        private readonly IHostApplicationService _applications;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly StaybnbSettings _settings;

        public HostApplicationController(
            IHostApplicationService applications,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IOptions<StaybnbSettings> settings)
        {
            _applications = applications;
            _context = context;
            _userManager = userManager;
            _settings = settings.Value;
        }

        // GET: /HostApplication/Apply
        [HttpGet]
        public async Task<IActionResult> Apply()
        {
            var userId = _userManager.GetUserId(User)!;

            // A Guest may only have one application waiting at a time
            if (User.IsInRole(Roles.Guest))
            {
                var pending = await _applications.GetPendingForGuestAsync(userId);
                if (pending != null)
                {
                    TempData["Info"] = $"Your application for '{pending.Property.Title}' is still being reviewed.";
                    return RedirectToAction(nameof(Status));
                }
            }

            var model = new HostApplicationViewModel();
            await PopulateFormDataAsync(model);
            return View(model);
        }

        // POST: /HostApplication/Apply
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxUploadBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
        public async Task<IActionResult> Apply(HostApplicationViewModel model)
        {
            // REQUIREMENT: an application without an image is incomplete and cannot be submitted
            if (model.Images == null || !model.Images.Any(f => f != null && f.Length > 0))
            {
                ModelState.AddModelError(nameof(model.Images),
                    "Your application is incomplete: please add at least one photo of your place.");
            }

            if (!model.ConfirmAccuracy)
            {
                ModelState.AddModelError(nameof(model.ConfirmAccuracy),
                    "Please confirm that the information is accurate.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateFormDataAsync(model);
                return View(model);
            }

            var userId = _userManager.GetUserId(User)!;
            var result = await _applications.SubmitAsync(userId, model);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                await PopulateFormDataAsync(model);
                return View(model);
            }

            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(Status));
        }

        // GET: /HostApplication/Status
        [HttpGet]
        public async Task<IActionResult> Status()
        {
            var userId = _userManager.GetUserId(User)!;
            var applications = await _applications.GetForUserAsync(userId);
            return View(applications);
        }

        /// <summary>Fills the parts of the form that are not posted back (amenity list, limits).</summary>
        private async Task PopulateFormDataAsync(HostApplicationViewModel model)
        {
            model.AvailableAmenities = await _context.Amenities
                .AsNoTracking()
                .OrderBy(a => a.Name)
                .Select(a => new AmenityOption { Id = a.Id, Name = a.Name, IconClass = a.IconClass })
                .ToListAsync();

            model.IsAdditionalProperty = User.IsInRole(Roles.Host);
            model.MaxImages = _settings.MaxImagesPerProperty;
            model.MaxImageSizeMb = _settings.MaxImageSizeMb;
        }
    }
}
