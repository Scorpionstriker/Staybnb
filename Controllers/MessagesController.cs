using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models.Constants;
using Staybnb.Models.Entities;
using Staybnb.Models.ViewModels.Shared;
using Staybnb.Services.Interfaces;

namespace Staybnb.Controllers
{
    /// <summary>
    /// Inbox for every signed-in user (Guest, Host, Admin).
    ///   /Messages                                   -> inbox
    ///   /Messages/Conversation?userId=...&amp;propertyId=5 -> inbox + open conversation
    /// </summary>
    [Authorize]
    public class MessagesController : Controller
    {
        private readonly IMessageService _messages;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MessagesController(IMessageService messages, ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _messages = messages;
            _context = context;
            _userManager = userManager;
        }

        private string UserId => _userManager.GetUserId(User)!;

        // GET: /Messages?search=cabin
        [HttpGet]
        public async Task<IActionResult> Index(string? search)
        {
            var model = new InboxViewModel
            {
                Search = search,
                Conversations = await _messages.GetConversationsAsync(UserId, search)
            };
            return View(model);
        }

        // GET: /Messages/Conversation?userId=abc&propertyId=5
        [HttpGet]
        public async Task<IActionResult> Conversation(string userId, int? propertyId)
        {
            var me = UserId;
            if (string.IsNullOrWhiteSpace(userId) || userId == me)
                return RedirectToAction(nameof(Index));

            var other = await _userManager.FindByIdAsync(userId);
            if (other == null) return NotFound();

            // Opening the conversation marks the other person's messages as READ in the database
            await _messages.MarkConversationReadAsync(me, userId);

            var messages = await _messages.GetMessagesAsync(me, userId);

            // Which listing is this about? The one in the link, otherwise the latest one mentioned
            var aboutId = propertyId ?? messages.LastOrDefault(m => m.PropertyId != null)?.PropertyId;
            HostProperty? property = null;
            string? cover = null;
            if (aboutId.HasValue)
            {
                property = await _context.HostProperties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == aboutId.Value);
                cover = await _context.PropertyImages.AsNoTracking()
                    .Where(i => i.PropertyId == aboutId.Value)
                    .OrderByDescending(i => i.IsCover).ThenBy(i => i.Id)
                    .Select(i => i.ImageUrl).FirstOrDefaultAsync();
            }

            var roles = await _userManager.GetRolesAsync(other);

            var model = new InboxViewModel
            {
                Conversations = await _messages.GetConversationsAsync(me),
                Active = new ConversationViewModel
                {
                    OtherUser = other,
                    OtherUserRole = roles.Contains(Roles.SuperAdmin) || roles.Contains(Roles.Admin) ? "Staybnb Admin"
                                  : roles.Contains(Roles.Host) ? "Host"
                                  : "Guest",
                    Messages = messages,
                    Property = property,
                    PropertyCoverUrl = cover
                }
            };

            return View(nameof(Index), model);
        }

        // POST: /Messages/Send
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(SendMessageInput input)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Message not sent.";
                return RedirectToAction(nameof(Conversation), new { userId = input.ReceiverId, propertyId = input.PropertyId });
            }

            var result = await _messages.SendAsync(UserId, input.ReceiverId, input.Content, input.PropertyId);
            if (!result.Succeeded)
                TempData["Error"] = result.Message;

            return RedirectToAction(nameof(Conversation), new { userId = input.ReceiverId, propertyId = input.PropertyId });
        }
    }
}
