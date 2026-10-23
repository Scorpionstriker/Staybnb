using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models.Common;
using Staybnb.Models.Entities;
using Staybnb.Models.Enums;
using Staybnb.Models.ViewModels.Shared;
using Staybnb.Services.Interfaces;

namespace Staybnb.Services
{
    public class MessageService : IMessageService
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notifications;

        public MessageService(ApplicationDbContext context, INotificationService notifications)
        {
            _context = context;
            _notifications = notifications;
        }

        /// <summary>The URL of a conversation with a user - also used as the notification link.</summary>
        public static string ConversationUrl(string otherUserId) => $"/Messages/Conversation?userId={otherUserId}";

        // =====================================================================
        // INBOX
        // =====================================================================

        public async Task<List<ConversationSummary>> GetConversationsAsync(string userId, string? search = null)
        {
            // Load this user's messages, then group them per "other person" in memory
            // (simple and fast enough for the size of this project)
            var messages = await _context.Messages
                .AsNoTracking()
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Include(m => m.Property)
                .Where(m => m.SenderId == userId || m.ReceiverId == userId)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

            var conversations = messages
                .GroupBy(m => m.SenderId == userId ? m.ReceiverId : m.SenderId)
                .Select(g =>
                {
                    var latest = g.First(); // already newest-first
                    var other = latest.SenderId == userId ? latest.Receiver : latest.Sender;
                    return new ConversationSummary
                    {
                        OtherUserId = g.Key,
                        OtherUserName = other.FullName,
                        OtherUserInitials = DisplayHelpers.Initials(other),
                        LastMessage = latest.Content.Length > 80 ? latest.Content[..80] + "..." : latest.Content,
                        LastMessageAt = latest.Timestamp,
                        LastMessageFromMe = latest.SenderId == userId,
                        UnreadCount = g.Count(m => m.ReceiverId == userId && !m.IsRead),
                        PropertyTitle = g.Select(m => m.Property?.Title).FirstOrDefault(t => t != null)
                    };
                })
                .OrderByDescending(c => c.LastMessageAt)
                .ToList();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                conversations = conversations
                    .Where(c => c.OtherUserName.Contains(term, StringComparison.OrdinalIgnoreCase)
                                || (c.PropertyTitle?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                                || c.LastMessage.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return conversations;
        }

        public Task<List<Message>> GetMessagesAsync(string userId, string otherUserId) =>
            _context.Messages
                .AsNoTracking()
                .Include(m => m.Property)
                .Where(m => (m.SenderId == userId && m.ReceiverId == otherUserId)
                         || (m.SenderId == otherUserId && m.ReceiverId == userId))
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

        // =====================================================================
        // READ / UNREAD (written to the database - marked in the rubric)
        // =====================================================================

        public async Task<int> MarkConversationReadAsync(string userId, string otherUserId)
        {
            var now = DateTime.UtcNow;
            var updated = await _context.Messages
                .Where(m => m.SenderId == otherUserId && m.ReceiverId == userId && !m.IsRead)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(m => m.IsRead, true)
                    .SetProperty(m => m.ReadAt, now));

            // The "New Message" notification for this conversation is now read too
            await _notifications.MarkReadByLinkAsync(userId, ConversationUrl(otherUserId));

            return updated;
        }

        public Task<int> GetUnreadCountAsync(string userId) =>
            _context.Messages.CountAsync(m => m.ReceiverId == userId && !m.IsRead);

        // =====================================================================
        // SEND
        // =====================================================================

        public async Task<ServiceResult<Message>> SendAsync(string senderId, string receiverId, string content, int? propertyId)
        {
            content = (content ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(content))
                return ServiceResult<Message>.Fail("Type a message first.");

            if (content.Length > 2000)
                return ServiceResult<Message>.Fail("Messages are limited to 2000 characters.");

            if (senderId == receiverId)
                return ServiceResult<Message>.Fail("You can't send a message to yourself.");

            var sender = await _context.Users.FirstOrDefaultAsync(u => u.Id == senderId);
            var receiver = await _context.Users.FirstOrDefaultAsync(u => u.Id == receiverId);
            if (sender == null || receiver == null)
                return ServiceResult<Message>.Fail("That user no longer exists.");

            if (propertyId.HasValue && !await _context.HostProperties.AnyAsync(p => p.Id == propertyId.Value))
                propertyId = null;

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = content,
                PropertyId = propertyId,
                Timestamp = DateTime.UtcNow,
                IsRead = false
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            // ---------- Notification (one unread "New Message" per conversation, kept up to date) ----------
            var link = ConversationUrl(senderId);
            var preview = content.Length > 120 ? content[..120] + "..." : content;
            var existing = await _context.Notifications.FirstOrDefaultAsync(n =>
                n.UserId == receiverId && !n.IsRead && n.Type == NotificationType.NewMessage && n.LinkUrl == link);

            if (existing != null)
            {
                existing.Message = $"{sender.FirstName}: {preview}";
                existing.CreatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
            else
            {
                await _notifications.CreateAsync(receiverId,
                    $"New Message from {sender.FirstName}",
                    $"{sender.FirstName}: {preview}",
                    NotificationType.NewMessage,
                    link);
            }

            return ServiceResult<Message>.Ok(message);
        }
    }
}
