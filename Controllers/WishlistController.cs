using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models.Constants;
using Staybnb.Models.Entities;
using Staybnb.Models.ViewModels.Guest;
using Staybnb.Services;

namespace Staybnb.Controllers
{
    /// <summary>The heart icon: Guests can save properties to their wishlist.</summary>
    [Authorize(Roles = Roles.Guest)]
    public class WishlistController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public WishlistController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Wishlist
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;

            var cards = await _context.WishlistItems
                .AsNoTracking()
                .Where(w => w.UserId == userId && w.Property.IsActive)
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => w.Property)
                .ToCards()
                .ToListAsync();

            cards.ForEach(c => c.IsWishlisted = true);
            return View(new WishlistViewModel { Properties = cards });
        }

        // POST: /Wishlist/Toggle
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int propertyId, string? returnUrl)
        {
            var userId = _userManager.GetUserId(User)!;

            var existing = await _context.WishlistItems.FirstOrDefaultAsync(w => w.UserId == userId && w.PropertyId == propertyId);
            if (existing != null)
            {
                _context.WishlistItems.Remove(existing);
            }
            else if (await _context.HostProperties.AnyAsync(p => p.Id == propertyId && p.IsActive))
            {
                _context.WishlistItems.Add(new WishlistItem { UserId = userId, PropertyId = propertyId, CreatedAt = DateTime.UtcNow });
            }

            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }
    }
}
