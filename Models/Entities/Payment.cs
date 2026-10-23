using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models.Enums;

namespace Staybnb.Models.Entities
{
    /// <summary>
    /// Simulated payment for a booking (one-to-one). No real payment gateway is used.
    /// </summary>
    public class Payment
    {
        public int Id { get; set; }

        [Precision(18, 2)]
        public decimal Amount { get; set; }

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        /// <summary>A generated reference such as "STB-20260128-0001".</summary>
        [MaxLength(50)]
        public string? Reference { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? PaidAt { get; set; }

        public int BookingId { get; set; }
        public Booking Booking { get; set; } = null!;
    }
}
