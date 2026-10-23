using System.ComponentModel.DataAnnotations;
using Staybnb.Models.Enums;

namespace Staybnb.Models.Entities
{
    /// <summary>
    /// An entry in the bell-icon notification centre, e.g. "Booking Approved" or "New Message".
    /// </summary>
    public class Notification
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Message { get; set; } = string.Empty;

        public NotificationType Type { get; set; } = NotificationType.General;

        /// <summary>Read/Unread status - marked in the rubric, so it must update in the database.</summary>
        public bool IsRead { get; set; }

        public DateTime? ReadAt { get; set; }

        /// <summary>Where the "Take Action" button sends the user, e.g. "/Host/BookingRequests".</summary>
        [MaxLength(300)]
        public string? LinkUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;
    }
}
