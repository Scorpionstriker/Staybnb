using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models.Enums;

namespace Staybnb.Models.Entities
{
    /// <summary>
    /// A Guest's reservation of a property.
    /// TotalPrice = (PricePerNight x Nights) + CleaningFee + ServiceFee
    /// The prices are copied onto the booking when it is made, so a Host changing
    /// their price later does not change existing bookings.
    /// </summary>
    public class Booking
    {
        public int Id { get; set; }

        [DataType(DataType.Date)]
        public DateTime CheckInDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime CheckOutDate { get; set; }

        [Range(1, 50)]
        public int NumberOfGuests { get; set; } = 1;

        /// <summary>Snapshot of the property's nightly price at booking time.</summary>
        [Precision(18, 2)]
        public decimal PricePerNight { get; set; }

        [Precision(18, 2)]
        public decimal CleaningFee { get; set; }

        [Precision(18, 2)]
        public decimal ServiceFee { get; set; }

        [Precision(18, 2)]
        public decimal TotalPrice { get; set; }

        public BookingStatus Status { get; set; } = BookingStatus.Pending;

        /// <summary>Optional note from the Guest to the Host when requesting the booking.</summary>
        [MaxLength(500)]
        public string? GuestMessage { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        /// <summary>Calculated, not stored.</summary>
        [NotMapped]
        public int Nights => (CheckOutDate.Date - CheckInDate.Date).Days;

        // ---------- Foreign keys ----------
        public int PropertyId { get; set; }
        public HostProperty Property { get; set; } = null!;

        [Required]
        public string GuestId { get; set; } = string.Empty;
        public ApplicationUser Guest { get; set; } = null!;

        // ---------- One-to-one children ----------
        public Payment? Payment { get; set; }

        public GuestCheckIn? GuestCheckIn { get; set; }
    }
}
