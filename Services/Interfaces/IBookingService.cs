using Staybnb.Models.Common;
using Staybnb.Models.Entities;

namespace Staybnb.Services.Interfaces
{
    /// <summary>
    /// The booking engine: price calculation, availability, and every booking status change
    /// (Pending -> Approved -> CheckedIn -> Completed, or Rejected / Cancelled).
    /// Each status change is saved, written to the ActivityLog and notified to the other party.
    /// </summary>
    public interface IBookingService
    {
        /// <summary>TotalPrice = (PricePerNight x Nights) + CleaningFee + ServiceFee</summary>
        PriceBreakdown CalculatePrice(HostProperty property, DateTime checkIn, DateTime checkOut);

        /// <summary>Checks dates, guest count and that the property is bookable (does not check availability).</summary>
        ServiceResult ValidateRequest(HostProperty property, DateTime checkIn, DateTime checkOut, int numberOfGuests);

        /// <summary>True if no Approved / CheckedIn booking overlaps the given dates.</summary>
        Task<bool> IsAvailableAsync(int propertyId, DateTime checkIn, DateTime checkOut, int? excludeBookingId = null);

        /// <summary>Date ranges that are already taken (for greying out the date picker).</summary>
        Task<List<BookedRange>> GetBookedRangesAsync(int propertyId);

        /// <summary>Guest requests a booking. Creates the Booking (Pending) and a Payment (Pending).</summary>
        Task<ServiceResult<Booking>> CreateBookingAsync(int propertyId, string guestId, DateTime checkIn,
            DateTime checkOut, int numberOfGuests, string? guestMessage);

        // ---------- Host actions ----------
        Task<ServiceResult> ApproveAsync(int bookingId, string hostId);
        Task<ServiceResult> RejectAsync(int bookingId, string hostId, string? reason);
        Task<ServiceResult> MarkCheckedInAsync(int bookingId, string hostId);
        Task<ServiceResult> CompleteAsync(int bookingId, string hostId);

        // ---------- Guest actions ----------
        Task<ServiceResult> CancelAsync(int bookingId, string guestId);
    }

    public class BookedRange
    {
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
    }
}
