using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Staybnb.Data;
using Staybnb.Models.Constants;
using Staybnb.Models.Entities;
using Staybnb.Models.Settings;
using Staybnb.Models.ViewModels.Guest;
using Staybnb.Services;
using Staybnb.Services.Interfaces;

namespace Staybnb.Controllers
{
    /// <summary>
    /// Property discovery: the Airbnb-style gallery (with filters) and the property details page.
    /// Anyone may browse; booking itself is restricted to Guests (BookingsController).
    /// </summary>
    public class PropertiesController : Controller
    {
        private const int PageSize = 12;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBookingService _bookings;
        private readonly StaybnbSettings _settings;

        public PropertiesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IBookingService bookings,
            IOptions<StaybnbSettings> settings)
        {
            _context = context;
            _userManager = userManager;
            _bookings = bookings;
            _settings = settings.Value;
        }

        // Gallery:  /Properties?City=Durban&Type=Cabin&Guests=2&CheckIn=...&CheckOut=...
        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] PropertySearchViewModel search)
        {
            search.Page = Math.Max(1, search.Page);

            // Ignore impossible date ranges rather than showing an error on the gallery
            var hasDates = search.CheckIn.HasValue && search.CheckOut.HasValue
                           && search.CheckOut.Value.Date > search.CheckIn.Value.Date
                           && search.CheckIn.Value.Date >= DateTime.Today;
            if (!hasDates)
            {
                search.CheckIn = null;
                search.CheckOut = null;
            }

            // Only ACTIVE (approved + switched on) listings are shown to guests
            var query = _context.HostProperties.AsNoTracking().Where(p => p.IsActive);

            if (!string.IsNullOrWhiteSpace(search.City))
            {
                var term = search.City.Trim();
                query = query.Where(p => p.City.Contains(term) || p.Address.Contains(term) || p.Title.Contains(term));
            }

            if (search.Type.HasValue)
                query = query.Where(p => p.PropertyType == search.Type.Value);

            if (search.Guests is > 0)
                query = query.Where(p => p.MaxGuests >= search.Guests.Value);

            if (search.MinPrice.HasValue)
                query = query.Where(p => p.PricePerNight >= search.MinPrice.Value);

            if (search.MaxPrice.HasValue)
                query = query.Where(p => p.PricePerNight <= search.MaxPrice.Value);

            if (hasDates)
                query = query.AvailableBetween(search.CheckIn!.Value, search.CheckOut!.Value);

            query = search.Sort switch
            {
                "price_asc" => query.OrderBy(p => p.PricePerNight),
                "price_desc" => query.OrderByDescending(p => p.PricePerNight),
                "rating" => query.OrderByDescending(p => p.Reviews.Average(r => (double?)r.Rating) ?? 0)
                                 .ThenByDescending(p => p.Reviews.Count),
                "newest" => query.OrderByDescending(p => p.CreatedAt),
                _ => query.OrderByDescending(p => p.Reviews.Count).ThenByDescending(p => p.CreatedAt)
            };

            search.TotalResults = await query.CountAsync();
            search.TotalPages = (int)Math.Ceiling(search.TotalResults / (double)PageSize);
            if (search.TotalPages > 0 && search.Page > search.TotalPages)
                search.Page = search.TotalPages;

            search.Results = await query
                .Skip((search.Page - 1) * PageSize)
                .Take(PageSize)
                .ToCards()
                .ToListAsync();

            // Hearts + total price for the searched dates
            var wishlist = await _context.GetWishlistIdsAsync(_userManager.GetUserId(User));
            foreach (var card in search.Results)
            {
                card.IsWishlisted = wishlist.Contains(card.Id);
                if (hasDates)
                {
                    var price = _bookings.CalculatePrice(
                        new HostProperty { PricePerNight = card.PricePerNight, CleaningFee = card.CleaningFee },
                        search.CheckIn!.Value, search.CheckOut!.Value);
                    card.StayTotal = price.Total;
                    card.StayNights = price.Nights;
                }
            }

            search.Cities = await _context.HostProperties
                .Where(p => p.IsActive)
                .Select(p => p.City)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return View(search);
        }

        // Details:  /Properties/View/5
        // (method is called Details because "View" is already a method on Controller)
        [HttpGet]
        [ActionName("View")]
        public async Task<IActionResult> Details(int id, DateTime? checkIn, DateTime? checkOut, int? guests)
        {
            var property = await _context.HostProperties
                .AsNoTracking()
                .Include(p => p.Host)
                .Include(p => p.Images)
                .Include(p => p.Amenities)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (property == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var isOwner = userId != null && property.HostId == userId;
            var isAdmin = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.SuperAdmin);

            // Inactive listings are only visible to their Host (preview) and Admins
            if (!property.IsActive && !isOwner && !isAdmin)
                return NotFound();

            var reviews = await _context.Reviews
                .AsNoTracking()
                .Include(r => r.Reviewer)
                .Where(r => r.PropertyId == id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // Default booking box: the searched dates, otherwise tomorrow for 2 nights
            var start = checkIn?.Date >= DateTime.Today ? checkIn.Value.Date : DateTime.Today.AddDays(1);
            var end = checkOut?.Date > start ? checkOut.Value.Date : start.AddDays(2);
            var guestCount = Math.Clamp(guests ?? 1, 1, property.MaxGuests);

            var model = new PropertyDetailsViewModel
            {
                Property = property,
                Host = property.Host,
                Images = property.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.Id).ToList(),
                Amenities = property.Amenities.OrderBy(a => a.Name).ToList(),
                Reviews = reviews,
                AverageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 2) : 0,
                HostListingCount = await _context.HostProperties.CountAsync(p => p.HostId == property.HostId && p.IsActive),
                HostReviewCount = await _context.Reviews.CountAsync(r => r.Property.HostId == property.HostId),
                BookedRanges = await _bookings.GetBookedRangesAsync(id),
                IsWishlisted = userId != null && await _context.WishlistItems.AnyAsync(w => w.UserId == userId && w.PropertyId == id),
                IsAuthenticated = userId != null,
                IsGuest = User.IsInRole(Roles.Guest),
                IsOwner = isOwner,
                ServiceFeePercentage = _settings.ServiceFeePercentage,
                CheckIn = start,
                CheckOut = end,
                Guests = guestCount,
                InitialPrice = _bookings.CalculatePrice(property, start, end)
            };

            return View("Details", model);
        }
    }
}
