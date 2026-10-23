using Staybnb.Models.Enums;

namespace Staybnb.Models.Entities
{
    /// <summary>
    /// A Guest's progress through a property's check-in process for one booking (one-to-one with Booking).
    /// </summary>
    public class GuestCheckIn
    {
        public int Id { get; set; }

        public CheckInStatus Status { get; set; } = CheckInStatus.NotStarted;

        /// <summary>The step the guest is currently on (1-based).</summary>
        public int CurrentStep { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SubmittedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public int BookingId { get; set; }
        public Booking Booking { get; set; } = null!;

        public int CheckInProcessId { get; set; }
        public CheckInProcess CheckInProcess { get; set; } = null!;

        public ICollection<GuestDocument> Documents { get; set; } = new List<GuestDocument>();
    }
}
