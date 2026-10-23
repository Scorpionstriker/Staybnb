using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models.Entities
{
    /// <summary>
    /// A database-backed message between two users (no SignalR / real-time).
    /// A "conversation" is simply all messages between the same two users.
    /// </summary>
    public class Message
    {
        public int Id { get; set; }

        [Required, MaxLength(2000)]
        public string Content { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>Set to true when the receiver opens the conversation.</summary>
        public bool IsRead { get; set; }

        public DateTime? ReadAt { get; set; }

        [Required]
        public string SenderId { get; set; } = string.Empty;
        public ApplicationUser Sender { get; set; } = null!;

        [Required]
        public string ReceiverId { get; set; } = string.Empty;
        public ApplicationUser Receiver { get; set; } = null!;

        /// <summary>Optional: the listing the message is about (e.g. a guest's question to a host).</summary>
        public int? PropertyId { get; set; }
        public HostProperty? Property { get; set; }
    }
}
