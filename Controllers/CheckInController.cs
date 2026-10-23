using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Staybnb.Models.Constants;
using Staybnb.Models.Entities;
using Staybnb.Models.ViewModels.Guest;
using Staybnb.Services;
using Staybnb.Services.Interfaces;

namespace Staybnb.Controllers
{
    /// <summary>
    /// Guest multi-step online check-in for an APPROVED booking.
    /// URL: /CheckIn/Index/{bookingId}
    /// </summary>
    [Authorize(Roles = Roles.Guest)]
    public class CheckInController : Controller
    {
        private const long MaxUploadBytes = 15L * 1024 * 1024;

        private readonly ICheckInService _checkIns;
        private readonly UserManager<ApplicationUser> _userManager;

        public CheckInController(ICheckInService checkIns, UserManager<ApplicationUser> userManager)
        {
            _checkIns = checkIns;
            _userManager = userManager;
        }

        private string GuestId => _userManager.GetUserId(User)!;

        // GET: /CheckIn/Index/12   (12 = booking id)
        [HttpGet]
        public async Task<IActionResult> Index(int id)
        {
            var checkIn = await _checkIns.GetForGuestAsync(id, GuestId);

            if (checkIn == null)
            {
                // First visit: create the check-in (only allowed for Approved bookings)
                var start = await _checkIns.StartAsync(id, GuestId);
                if (!start.Succeeded)
                {
                    TempData["Error"] = start.Message;
                    return RedirectToAction("Details", "Bookings", new { id });
                }
                checkIn = start.Data!;
            }

            var steps = checkIn.CheckInProcess.GetSteps();
            var current = steps.FirstOrDefault(s => s.StepNumber == checkIn.CurrentStep) ?? steps.LastOrDefault();

            var model = new CheckInWizardViewModel
            {
                CheckIn = checkIn,
                Steps = steps,
                CurrentStep = current,
                CurrentDocument = current is { RequiresDocument: true }
                    ? checkIn.Documents.FirstOrDefault(d => d.DocumentType == CheckInService.DocumentTypeFor(current))
                    : null
            };

            return View(model);
        }

        // POST: /CheckIn/Step
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxUploadBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
        public async Task<IActionResult> Step(int bookingId, int stepNumber, bool acknowledged, IFormFile? document, string? documentType)
        {
            var result = await _checkIns.CompleteStepAsync(bookingId, GuestId, stepNumber, acknowledged, document, documentType);

            if (!result.Succeeded)
                TempData["Error"] = result.Message;
            else if (!string.IsNullOrEmpty(result.Message))
                TempData["Success"] = result.Message;

            return RedirectToAction(nameof(Index), new { id = bookingId });
        }

        // POST: /CheckIn/Back
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Back(int bookingId, int stepNumber)
        {
            var result = await _checkIns.GoToStepAsync(bookingId, GuestId, stepNumber);
            if (!result.Succeeded)
                TempData["Error"] = result.Message;

            return RedirectToAction(nameof(Index), new { id = bookingId });
        }
    }
}
