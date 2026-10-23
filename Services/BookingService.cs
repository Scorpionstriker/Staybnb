using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Staybnb.Data;
using Staybnb.Models.Common;
using Staybnb.Models.Constants;
using Staybnb.Models.Entities;
using Staybnb.Models.Enums;
using Staybnb.Models.Settings;
using Staybnb.Services.Interfaces;

namespace Staybnb.Services
{
    public class BookingService : IBookingService
    {
        /// <summary>
        /// The booking "state machine": which status may change into which.
        /// Anything not listed here is refused.
        /// </summary>
        private static readonly Dictionary<BookingStatus, BookingStatus[]> AllowedTransitions = new()
        {
            [BookingStatus.Pending] = new[] { BookingStatus.Approved, BookingStatus.Rejected, BookingStatus.Cancelled },
            [BookingStatus.Approved] = new[] { BookingStatus.CheckedIn, BookingStatus.Cancelled },
            [BookingStatus.CheckedIn] = new[] { BookingStatus.Completed },
            [BookingStatus.Rejected] = Array.Empty<BookingStatus>(),
            [BookingStatus.Cancelled] = Array.Empty<BookingStatus>(),
            [BookingStatus.Completed] = Array.Empty<BookingStatus>()
        };

        private readonly ApplicationDbContext _context;
        private readonly IActivityLogService _activityLog;
        private readonly INotificationService _notifications;
        private readonly StaybnbSettings _settings;

        public BookingService(
            ApplicationDbContext context,
            IActivityLogService activityLog,
            INotificationService notifications,
            IOptions<StaybnbSettings> settings)
        {
            _context = context;
            _activityLog = activityLog;
            _notifications = notifications;
            _settings = settings.Value;
        }

        public static bool CanTransition(BookingStatus from, BookingStatus to) =>
            AllowedTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);

        // =====================================================================
        // PRICE
        // =====================================================================

        public PriceBreakdown CalculatePrice(HostProperty property, DateTime checkIn, DateTime checkOut)
        {
            var nights = Math.Max(0, (checkOut.Date - checkIn.Date).Days);
            var subtotal = property.PricePerNight * nights;
            var serviceFee = Math.Round(subtotal * _settings.ServiceFeePercentage, 2, MidpointRounding.AwayFromZero);
            var cleaningFee = nights > 0 ? property.CleaningFee : 0m;

            return new PriceBreakdown
            {
                Nights = nights,
                PricePerNight = property.PricePerNight,
                Subtotal = subtotal,
                CleaningFee = cleaningFee,
                ServiceFee = serviceFee,
                ServiceFeePercentage = _settings.ServiceFeePercentage,
                // TotalPrice = (PricePerNight x Nights) + CleaningFee + ServiceFee
                Total = subtotal + cleaningFee + serviceFee
            };
        }

        // =====================================================================
        // VALIDATION + AVAILABILITY
        // =====================================================================

        public ServiceResult ValidateRequest(HostProperty property, DateTime checkIn, DateTime checkOut, int numberOfGuests)
        {
            var today = DateTime.Today;
            checkIn = checkIn.Date;
            checkOut = checkOut.Date;

            if (!property.IsActive)
                return ServiceResult.Fail("This property is not currently accepting bookings.");

            if (checkIn < today)
                return ServiceResult.Fail("Check-in date cannot be in the past.");

            if (checkOut <= checkIn)
                return ServiceResult.Fail("Check-out date must be after the check-in date.");

            if (checkIn > today.AddDays(_settings.MaxDaysInAdvance))
                return ServiceResult.Fail($"Bookings can only be made up to {_settings.MaxDaysInAdvance} days in advance.");

            var nights = (checkOut - checkIn).Days;
            if (nights < _settings.MinNights)
                return ServiceResult.Fail($"The minimum stay is {_settings.MinNights} night(s).");

            if (nights > _settings.MaxNights)
                return ServiceResult.Fail($"The maximum stay is {_settings.MaxNights} nights.");

            if (numberOfGuests < 1)
                return ServiceResult.Fail("At least one guest is required.");

            if (numberOfGuests > property.MaxGuests)
                return ServiceResult.Fail($"This property allows a maximum of {property.MaxGuests} guest(s).");

            return ServiceResult.Ok();
        }

        public async Task<bool> IsAvailableAsync(int propertyId, DateTime checkIn, DateTime checkOut, int? excludeBookingId = null)
        {
            checkIn = checkIn.Date;
            checkOut = checkOut.Date;

            // Two stays overlap when each one starts before the other ends.
            // (Check-out day = next guest's check-in day is allowed.)
            var overlapExists = await _context.Bookings.AnyAsync(b =>
                b.PropertyId == propertyId
                && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn)
                && (excludeBookingId == null || b.Id != excludeBookingId)
                && b.CheckInDate < checkOut
                && b.CheckOutDate > checkIn);

            return !overlapExists;
        }

        public Task<List<BookedRange>> GetBookedRangesAsync(int propertyId) =>
            _context.Bookings
                .AsNoTracking()
                .Where(b => b.PropertyId == propertyId
                            && (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn)
                            && b.CheckOutDate >= DateTime.Today)
                .OrderBy(b => b.CheckInDate)
                .Select(b => new BookedRange { CheckIn = b.CheckInDate, CheckOut = b.CheckOutDate })
                .ToListAsync();

        // =====================================================================
        // CREATE (Guest requests a booking)
        // =====================================================================

        public async Task<ServiceResult<Booking>> CreateBookingAsync(int propertyId, string guestId, DateTime checkIn,
            DateTime checkOut, int numberOfGuests, string? guestMessage)
        {
            var property = await _context.HostProperties
                .Include(p => p.Host)
                .FirstOrDefaultAsync(p => p.Id == propertyId);

            if (property == null)
                return ServiceResult<Booking>.Fail("Property not found.");

            if (property.HostId == guestId)
                return ServiceResult<Booking>.Fail("You cannot book your own property.");

            var validation = ValidateRequest(property, checkIn, checkOut, numberOfGuests);
            if (!validation.Succeeded)
                return ServiceResult<Booking>.Fail(validation.Message);

            if (!await IsAvailableAsync(propertyId, checkIn, checkOut))
                return ServiceResult<Booking>.Fail("Sorry, those dates are no longer available. Please choose different dates.");

            // Stop the same guest sending duplicate overlapping requests for the same place
            var duplicate = await _context.Bookings.AnyAsync(b =>
                b.PropertyId == propertyId && b.GuestId == guestId && b.Status == BookingStatus.Pending
                && b.CheckInDate < checkOut.Date && b.CheckOutDate > checkIn.Date);
            if (duplicate)
                return ServiceResult<Booking>.Fail("You already have a pending request for these dates.");

            var price = CalculatePrice(property, checkIn, checkOut);

            var booking = new Booking
            {
                PropertyId = property.Id,
                GuestId = guestId,
                CheckInDate = checkIn.Date,
                CheckOutDate = checkOut.Date,
                NumberOfGuests = numberOfGuests,
                PricePerNight = price.PricePerNight,
                CleaningFee = price.CleaningFee,
                ServiceFee = price.ServiceFee,
                TotalPrice = price.Total,
                GuestMessage = string.IsNullOrWhiteSpace(guestMessage) ? null : guestMessage.Trim(),
                Status = BookingStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                Payment = new Payment
                {
                    Amount = price.Total,
                    Status = PaymentStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                }
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            await _activityLog.LogAsync(guestId, ActivityActions.BookingRequested, ActivityType.BookingStatusChange,
                $"Booking #{booking.Id} requested for '{property.Title}' " +
                $"({booking.CheckInDate:yyyy-MM-dd} to {booking.CheckOutDate:yyyy-MM-dd}, {Money(booking.TotalPrice)}).");

            await _notifications.CreateAsync(property.HostId,
                "New Booking Request",
                $"You have a new booking request for '{property.Title}' from {booking.CheckInDate:dd MMM} to {booking.CheckOutDate:dd MMM yyyy}.",
                NotificationType.BookingRequested,
                "/Host/BookingRequests");

            return ServiceResult<Booking>.Ok(booking, "Your booking request has been sent to the Host.");
        }

        // =====================================================================
        // HOST ACTIONS
        // =====================================================================

        public async Task<ServiceResult> ApproveAsync(int bookingId, string hostId)
        {
            var booking = await LoadBookingAsync(bookingId);
            var check = CheckHostCanChange(booking, hostId, BookingStatus.Approved);
            if (!check.Succeeded) return check;

            // Another booking may have been approved for the same dates in the meantime
            if (!await IsAvailableAsync(booking!.PropertyId, booking.CheckInDate, booking.CheckOutDate, booking.Id))
                return ServiceResult.Fail("These dates overlap another approved booking. Reject this request instead.");

            var oldStatus = booking.Status;
            booking.Status = BookingStatus.Approved;
            booking.UpdatedAt = DateTime.UtcNow;

            // Simulated payment: charged once the Host accepts
            booking.Payment ??= new Payment { Amount = booking.TotalPrice, CreatedAt = DateTime.UtcNow };
            booking.Payment.Status = PaymentStatus.Paid;
            booking.Payment.PaidAt = DateTime.UtcNow;
            booking.Payment.Reference ??= $"STB-{DateTime.UtcNow:yyyyMMdd}-{booking.Id:D5}";

            await _context.SaveChangesAsync();

            await LogStatusChangeAsync(hostId, booking, oldStatus, ActivityActions.BookingApproved);
            await _activityLog.LogAsync(booking.GuestId, ActivityActions.PaymentCompleted, ActivityType.BookingStatusChange,
                $"Payment {booking.Payment.Reference} of {Money(booking.Payment.Amount)} for booking #{booking.Id} marked as paid.");

            await _notifications.CreateAsync(booking.GuestId,
                "Booking Approved",
                $"Great news! Your stay at '{booking.Property.Title}' from {booking.CheckInDate:dd MMM} to {booking.CheckOutDate:dd MMM yyyy} has been approved. " +
                "You can now complete online check-in.",
                NotificationType.BookingApproved,
                $"/Bookings/Details/{booking.Id}");

            return ServiceResult.Ok($"Booking #{booking.Id} approved.");
        }

        public async Task<ServiceResult> RejectAsync(int bookingId, string hostId, string? reason)
        {
            var booking = await LoadBookingAsync(bookingId);
            var check = CheckHostCanChange(booking, hostId, BookingStatus.Rejected);
            if (!check.Succeeded) return check;

            var oldStatus = booking!.Status;
            booking.Status = BookingStatus.Rejected;
            booking.UpdatedAt = DateTime.UtcNow;
            if (booking.Payment != null)
                booking.Payment.Status = PaymentStatus.Cancelled;

            await _context.SaveChangesAsync();

            await LogStatusChangeAsync(hostId, booking, oldStatus, ActivityActions.BookingRejected, reason);

            var reasonText = string.IsNullOrWhiteSpace(reason) ? "" : $" Reason: {reason.Trim()}";
            await _notifications.CreateAsync(booking.GuestId,
                "Booking Declined",
                $"Unfortunately your request for '{booking.Property.Title}' ({booking.CheckInDate:dd MMM} - {booking.CheckOutDate:dd MMM yyyy}) was declined.{reasonText}",
                NotificationType.BookingRejected,
                $"/Bookings/Details/{booking.Id}");

            return ServiceResult.Ok($"Booking #{booking.Id} rejected.");
        }

        /// <summary>Called when the Host has verified the guest's check-in documents.</summary>
        public async Task<ServiceResult> MarkCheckedInAsync(int bookingId, string hostId)
        {
            var booking = await LoadBookingAsync(bookingId);
            var check = CheckHostCanChange(booking, hostId, BookingStatus.CheckedIn);
            if (!check.Succeeded) return check;

            var oldStatus = booking!.Status;
            booking.Status = BookingStatus.CheckedIn;
            booking.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await LogStatusChangeAsync(hostId, booking, oldStatus, ActivityActions.BookingCheckedIn);

            await _notifications.CreateAsync(booking.GuestId,
                "Check-in Complete",
                $"Your check-in for '{booking.Property.Title}' has been verified by the Host. Enjoy your stay!",
                NotificationType.DocumentVerified,
                $"/Bookings/Details/{booking.Id}");

            return ServiceResult.Ok($"Booking #{booking.Id} marked as checked in.");
        }

        public async Task<ServiceResult> CompleteAsync(int bookingId, string hostId)
        {
            var booking = await LoadBookingAsync(bookingId);
            var check = CheckHostCanChange(booking, hostId, BookingStatus.Completed);
            if (!check.Succeeded) return check;

            if (booking!.CheckOutDate > DateTime.Today)
                return ServiceResult.Fail("A stay can only be completed on or after the check-out date.");

            var oldStatus = booking.Status;
            booking.Status = BookingStatus.Completed;
            booking.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await LogStatusChangeAsync(hostId, booking, oldStatus, ActivityActions.BookingCompleted);

            await _notifications.CreateAsync(booking.GuestId,
                "Thanks for staying!",
                $"Your stay at '{booking.Property.Title}' is complete. We'd love to hear how it went - leave a review.",
                NotificationType.General,
                $"/Bookings/Details/{booking.Id}");

            return ServiceResult.Ok($"Booking #{booking.Id} completed.");
        }

        // =====================================================================
        // GUEST ACTIONS
        // =====================================================================

        public async Task<ServiceResult> CancelAsync(int bookingId, string guestId)
        {
            var booking = await LoadBookingAsync(bookingId);
            if (booking == null || booking.GuestId != guestId)
                return ServiceResult.Fail("Booking not found.");

            if (!CanTransition(booking.Status, BookingStatus.Cancelled))
                return ServiceResult.Fail($"A booking that is {booking.Status} cannot be cancelled.");

            if (booking.Status == BookingStatus.Approved && booking.CheckInDate <= DateTime.Today)
                return ServiceResult.Fail("This stay has already started and can no longer be cancelled.");

            var oldStatus = booking.Status;
            booking.Status = BookingStatus.Cancelled;
            booking.UpdatedAt = DateTime.UtcNow;

            if (booking.Payment != null)
                booking.Payment.Status = booking.Payment.Status == PaymentStatus.Paid
                    ? PaymentStatus.Refunded
                    : PaymentStatus.Cancelled;

            await _context.SaveChangesAsync();

            await LogStatusChangeAsync(guestId, booking, oldStatus, ActivityActions.BookingCancelled);

            await _notifications.CreateAsync(booking.Property.HostId,
                "Booking Cancelled",
                $"{booking.Guest.FullName} cancelled their booking for '{booking.Property.Title}' ({booking.CheckInDate:dd MMM} - {booking.CheckOutDate:dd MMM yyyy}).",
                NotificationType.BookingCancelled,
                "/Host/BookingRequests");

            return ServiceResult.Ok("Your booking has been cancelled.");
        }

        // =====================================================================
        // HELPERS
        // =====================================================================

        private Task<Booking?> LoadBookingAsync(int bookingId) =>
            _context.Bookings
                .Include(b => b.Property)
                .Include(b => b.Guest)
                .Include(b => b.Payment)
                .FirstOrDefaultAsync(b => b.Id == bookingId);

        /// <summary>The booking must exist, belong to one of this Host's properties, and allow the change.</summary>
        private static ServiceResult CheckHostCanChange(Booking? booking, string hostId, BookingStatus newStatus)
        {
            // Same message for "doesn't exist" and "not yours" so Hosts can't probe other Hosts' bookings
            if (booking == null || booking.Property.HostId != hostId)
                return ServiceResult.Fail("Booking not found.");

            if (!CanTransition(booking.Status, newStatus))
                return ServiceResult.Fail($"A booking that is {booking.Status} cannot be changed to {newStatus}.");

            return ServiceResult.Ok();
        }

        private Task LogStatusChangeAsync(string userId, Booking booking, BookingStatus oldStatus, string action, string? note = null)
        {
            var details = $"Booking #{booking.Id} for '{booking.Property.Title}' changed from {oldStatus} to {booking.Status}.";
            if (!string.IsNullOrWhiteSpace(note))
                details += $" Note: {note.Trim()}";

            return _activityLog.LogAsync(userId, action, ActivityType.BookingStatusChange, details);
        }

        private static string Money(decimal amount) =>
            "R " + amount.ToString("N2", CultureInfo.InvariantCulture);
    }
}
