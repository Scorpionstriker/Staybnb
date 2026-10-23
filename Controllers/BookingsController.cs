using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models.Constants;
using Staybnb.Models.Entities;
using Staybnb.Models.Enums;
using Staybnb.Models.ViewModels.Guest;
using Staybnb.Services.Interfaces;

namespace Staybnb.Controllers
{
    /// <summary>
    /// The Guest booking engine: request to book, My Trips, booking details, cancel and review.
    /// Guest role only - Hosts get Access Denied (a user is either a Guest or a Host).
    /// </summary>
    [Authorize(Roles = Roles.Guest)]
    public class BookingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBookingService _bookings;
        private readonly INotificationService _notifications;

        public BookingsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IBookingService bookings,
            INotificationService notifications)
        {
            _context = context;
            _userManager = userManager;
            _bookings = bookings;
            _notifications = notifications;
        }

        private string GuestId => _userManager.GetUserId(User)!;

        // Request to book

        // GET: /Bookings/Confirm?propertyId=5&checkIn=2026-01-28&checkOut=2026-01-30&guests=2
        [HttpGet]
        public async Task<IActionResult> Confirm(int propertyId, DateTime checkIn, DateTime checkOut, int guests = 1)
        {
            var model = new BookingConfirmViewModel
            {
                PropertyId = propertyId,
                CheckIn = checkIn.Date,
                CheckOut = checkOut.Date,
                Guests = guests
            };

            if (!await FillConfirmModelAsync(model))
                return NotFound();

            return View(model);
        }

        // POST: /Bookings/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookingConfirmViewModel model)
        {
            if (!ModelState.IsValid)
            {
                if (!await FillConfirmModelAsync(model)) return NotFound();
                return View(nameof(Confirm), model);
            }

            // The price is recalculated on the server inside the service - never trusted from the browser
            var result = await _bookings.CreateBookingAsync(
                model.PropertyId, GuestId, model.CheckIn, model.CheckOut, model.Guests, model.GuestMessage);

            if (!result.Succeeded)
            {
                if (!await FillConfirmModelAsync(model)) return NotFound();
                model.Problem = result.Message;
                return View(nameof(Confirm), model);
            }

            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(Details), new { id = result.Data!.Id });
        }

        /// <summary>Loads the property, price and any problem with the dates. False = property missing.</summary>
        private async Task<bool> FillConfirmModelAsync(BookingConfirmViewModel model)
        {
            var property = await _context.HostProperties
                .AsNoTracking()
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == model.PropertyId);

            if (property == null) return false;

            model.Property = property;
            model.CoverImageUrl = property.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault();
            model.Price = _bookings.CalculatePrice(property, model.CheckIn, model.CheckOut);

            var validation = _bookings.ValidateRequest(property, model.CheckIn, model.CheckOut, model.Guests);
            if (!validation.Succeeded)
                model.Problem = validation.Message;
            else if (!await _bookings.IsAvailableAsync(property.Id, model.CheckIn, model.CheckOut))
                model.Problem = "Sorry, those dates are already booked. Please choose different dates.";

            return true;
        }

        // My trips

        // GET: /Bookings?filter=upcoming
        [HttpGet]
        public async Task<IActionResult> Index(string filter = "upcoming")
        {
            var guestId = GuestId;
            var today = DateTime.Today;
            filter = (filter ?? "upcoming").ToLowerInvariant();

            var all = _context.Bookings.AsNoTracking().Where(b => b.GuestId == guestId);

            var query = filter switch
            {
                "pending" => all.Where(b => b.Status == BookingStatus.Pending),
                "past" => all.Where(b => b.Status == BookingStatus.Completed
                                         || ((b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn) && b.CheckOutDate < today)),
                "cancelled" => all.Where(b => b.Status == BookingStatus.Cancelled || b.Status == BookingStatus.Rejected),
                _ => all.Where(b => (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn) && b.CheckOutDate >= today)
            };

            var bookings = await query
                .Include(b => b.Property).ThenInclude(p => p.Images)
                .Include(b => b.Property).ThenInclude(p => p.Host)
                .Include(b => b.GuestCheckIn)
                .ToListAsync();

            bookings = filter is "past" or "cancelled"
                ? bookings.OrderByDescending(b => b.CheckInDate).ToList()
                : bookings.OrderBy(b => b.CheckInDate).ToList();

            return View(new MyTripsViewModel
            {
                Filter = filter,
                Bookings = bookings,
                UpcomingCount = await all.CountAsync(b => (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn) && b.CheckOutDate >= today),
                PendingCount = await all.CountAsync(b => b.Status == BookingStatus.Pending)
            });
        }

        // Booking details

        // GET: /Bookings/Details/12
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var guestId = GuestId;
            var booking = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.Payment)
                .Include(b => b.GuestCheckIn)
                .Include(b => b.Property).ThenInclude(p => p.Images)
                .Include(b => b.Property).ThenInclude(p => p.Host)
                .FirstOrDefaultAsync(b => b.Id == id && b.GuestId == guestId);

            if (booking == null) return NotFound();

            var existingReview = await _context.Reviews.AsNoTracking()
                .FirstOrDefaultAsync(r => r.PropertyId == booking.PropertyId && r.ReviewerId == guestId);

            var model = new BookingDetailsViewModel
            {
                Booking = booking,
                CoverImageUrl = booking.Property.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault(),
                CanCancel = booking.Status == BookingStatus.Pending
                            || (booking.Status == BookingStatus.Approved && booking.CheckInDate > DateTime.Today),
                CanCheckIn = booking.Status == BookingStatus.Approved,
                CanReview = booking.Status == BookingStatus.Completed && existingReview == null,
                ExistingReview = existingReview
            };

            return View(model);
        }

        // POST: /Bookings/Cancel/12
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var result = await _bookings.CancelAsync(id, GuestId);
            TempData[result.Succeeded ? "Success" : "Error"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        // Review (after a completed stay)

        // POST: /Bookings/AddReview
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReview(ReviewInputModel input)
        {
            var guestId = GuestId;
            var booking = await _context.Bookings
                .Include(b => b.Property)
                .FirstOrDefaultAsync(b => b.Id == input.BookingId && b.GuestId == guestId);

            if (booking == null) return NotFound();

            if (booking.Status != BookingStatus.Completed)
            {
                TempData["Error"] = "You can leave a review once your stay is completed.";
                return RedirectToAction(nameof(Details), new { id = input.BookingId });
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please choose a star rating between 1 and 5.";
                return RedirectToAction(nameof(Details), new { id = input.BookingId });
            }

            var alreadyReviewed = await _context.Reviews.AnyAsync(r => r.PropertyId == booking.PropertyId && r.ReviewerId == guestId);
            if (alreadyReviewed)
            {
                TempData["Error"] = "You have already reviewed this place.";
                return RedirectToAction(nameof(Details), new { id = input.BookingId });
            }

            _context.Reviews.Add(new Review
            {
                PropertyId = booking.PropertyId,
                ReviewerId = guestId,
                Rating = input.Rating,
                Comment = input.Comment?.Trim() ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            await _notifications.CreateAsync(booking.Property.HostId,
                "New Review",
                $"A guest left a {input.Rating}-star review for '{booking.Property.Title}'.",
                NotificationType.General,
                "/Host/Reviews");

            TempData["Success"] = "Thanks for your review!";
            return RedirectToAction(nameof(Details), new { id = input.BookingId });
        }
    }
}
