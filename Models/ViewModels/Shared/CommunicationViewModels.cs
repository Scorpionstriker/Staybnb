using System.ComponentModel.DataAnnotations;
using Staybnb.Models.Entities;

namespace Staybnb.Models.ViewModels.Shared
{
    // =====================================================================
    // MESSAGING
    // =====================================================================

    /// <summary>One row in the inbox list (the latest message with one other person).</summary>
    public class ConversationSummary
    {
        public string OtherUserId { get; set; } = string.Empty;
        public string OtherUserName { get; set; } = string.Empty;
        public string OtherUserInitials { get; set; } = string.Empty;
        public string LastMessage { get; set; } = string.Empty;
        public DateTime LastMessageAt { get; set; }
        public bool LastMessageFromMe { get; set; }
        public int UnreadCount { get; set; }
        public string? PropertyTitle { get; set; }
    }

    public class InboxViewModel
    {
        public List<ConversationSummary> Conversations { get; set; } = new();

        /// <summary>The open conversation (null on the plain inbox page).</summary>
        public ConversationViewModel? Active { get; set; }

        public string? Search { get; set; }
    }

    public class ConversationViewModel
    {
        public ApplicationUser OtherUser { get; set; } = null!;
        public string OtherUserRole { get; set; } = string.Empty;
        public List<Message> Messages { get; set; } = new();

        /// <summary>The listing this conversation is about (new messages are linked to it).</summary>
        public HostProperty? Property { get; set; }
        public string? PropertyCoverUrl { get; set; }
    }

    public class SendMessageInput
    {
        [Required]
        public string ReceiverId { get; set; } = string.Empty;

        public int? PropertyId { get; set; }

        [Required(ErrorMessage = "Type a message first.")]
        [StringLength(2000, ErrorMessage = "Messages are limited to 2000 characters.")]
        public string Content { get; set; } = string.Empty;
    }

    // =====================================================================
    // NOTIFICATIONS
    // =====================================================================
    public class NotificationCenterViewModel
    {
        /// <summary>all | unread | read | today</summary>
        public string Filter { get; set; } = "all";

        public List<Notification> Notifications { get; set; } = new();
        public Notification? Selected { get; set; }

        public int AllCount { get; set; }
        public int UnreadCount { get; set; }
        public int ReadCount { get; set; }
        public int TodayCount { get; set; }
    }
}
