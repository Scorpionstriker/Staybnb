using Staybnb.Models.Common;
using Staybnb.Models.Entities;
using Staybnb.Models.ViewModels.Shared;

namespace Staybnb.Services.Interfaces
{
    /// <summary>
    /// Database-backed messaging (no SignalR). A conversation is all messages between two users.
    /// </summary>
    public interface IMessageService
    {
        /// <summary>The inbox: one summary per person this user has messaged, newest first.</summary>
        Task<List<ConversationSummary>> GetConversationsAsync(string userId, string? search = null);

        /// <summary>All messages between the two users, oldest first.</summary>
        Task<List<Message>> GetMessagesAsync(string userId, string otherUserId);

        /// <summary>Marks every message the other user sent to this user as read (and the related notifications).</summary>
        Task<int> MarkConversationReadAsync(string userId, string otherUserId);

        Task<ServiceResult<Message>> SendAsync(string senderId, string receiverId, string content, int? propertyId);

        Task<int> GetUnreadCountAsync(string userId);
    }
}
