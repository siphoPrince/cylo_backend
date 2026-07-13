using Cylo_Backend.Data;
using Cylo_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Cylo_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MessagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MessagesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int currentUserId)) return Unauthorized();

            // Step 1: Group and get the latest message data from the database efficiently
            var conversationGroups = await _context.Messages
                .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
                .Where(m => m.OrderId != null) // Ensure we only target order-based chats for this view
                .GroupBy(m => m.OrderId)
                .Select(g => new
                {
                    OrderId = g.Key,
                    LatestMsg = g.OrderByDescending(m => m.SentAt).FirstOrDefault(),
                    UnreadCount = g.Count(m => m.ReceiverId == currentUserId && !m.IsRead)
                })
                .ToListAsync();

            // Step 2: Extract all target distinct User IDs to pull profiles in bulk
            var otherUserIds = conversationGroups
                .Where(c => c.LatestMsg != null)
                .Select(c => c.LatestMsg.SenderId == currentUserId ? c.LatestMsg.ReceiverId : c.LatestMsg.SenderId)
                .Distinct()
                .ToList();

            // Step 3: Fetch all required profiles in ONE single query (Saves the database!)
            var profilesMap = await _context.Profiles
                .Where(p => otherUserIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId);

            // Step 4: Map the final payload inside server memory
            var processedConversations = conversationGroups.Select(c => {
                if (c.LatestMsg == null) return null;

                int otherUserId = (c.LatestMsg.SenderId == currentUserId) ? c.LatestMsg.ReceiverId : c.LatestMsg.SenderId;
                profilesMap.TryGetValue(otherUserId, out var profile);

                return new
                {
                    OrderId = c.OrderId,
                    LastMessage = c.LatestMsg.Content,
                    LastMessageTime = c.LatestMsg.SentAt,
                    UnreadCount = c.UnreadCount,
                    OtherUser = profile != null ? new
                    {
                        UserId = profile.UserId,
                        HandleName = profile.HandleName,
                        ImageUrl = profile.ImageUrl,
                        LastSeen = profile.LastSeen,
                        IsOnline = false // Will be updated on the client via SignalR presence socket
                    } : null
                };
            })
            .Where(x => x != null)
            .ToList();

            return Ok(processedConversations);
        }

        // 2. GET: api/messages/initiate-order-chat/{orderId} (Bootstrap New Escrow Inquiry Chats)
        [HttpGet("initiate-order-chat/{orderId}")]
        public async Task<IActionResult> InitiateOrderChat(int orderId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int currentUserId)) return Unauthorized();

            // Look up the order context to discover the buyer and seller identities
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return NotFound(new { message = "Order context not found." });

            // Identify the other participant's ID
            int otherUserId = (order.BuyerId == currentUserId) ? order.SellerId : order.BuyerId;

            // Fetch their profile parameters
            var otherProfile = await _context.Profiles.FirstOrDefaultAsync(p => p.UserId == otherUserId);

            // Return a payload matching your exact conversation structure
            var dynamicStub = new
            {
                OrderId = orderId,
                LastMessage = "Starting a new inquiry...",
                SentAt = DateTime.UtcNow,
                UnreadCount = 0,
                OtherUser = new
                {
                    UserId = otherUserId,
                    HandleName = otherProfile?.HandleName ?? "User",
                    ImageUrl = otherProfile?.ImageUrl,
                    IsOnline = false,
                    LastSeen = otherProfile?.LastSeen
                }
            };

            return Ok(dynamicStub);
        }

        // 3. GET: api/messages/order/{orderId} (Full Chat History)
        [HttpGet("order/{orderId}")]
        public async Task<IActionResult> GetChatHistoryByOrder(int orderId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int currentUserId)) return Unauthorized();

            var messages = await _context.Messages
                .Where(m => m.OrderId == orderId &&
                           (m.SenderId == currentUserId || m.ReceiverId == currentUserId))
                .OrderBy(m => m.SentAt)
                .Select(m => new {
                    m.Id,
                    m.Content,
                    m.SenderId,
                    m.ReceiverId,
                    m.SentAt,
                    m.OrderId
                })
                .ToListAsync();

            return Ok(messages);
        }

        // 4. PUT: api/messages/{id} (Edit Message text content)
        [HttpPut("{id}")]
        public async Task<IActionResult> EditMessage(int id, [FromBody] string newContent)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int currentUserId)) return Unauthorized();

            var message = await _context.Messages.FindAsync(id);
            if (message == null) return NotFound();
            if (message.SenderId != currentUserId) return Forbid();

            message.Content = newContent;
            message.IsEdited = true;
            await _context.SaveChangesAsync();

            return Ok(message);
        }

        // 5. DELETE: api/messages/{id} (Remove Message)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int currentUserId)) return Unauthorized();

            var message = await _context.Messages.FindAsync(id);
            if (message == null) return NotFound();
            if (message.SenderId != currentUserId) return Forbid();

            _context.Messages.Remove(message);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Message deleted" });
        }

        // 6. POST: api/messages/read/{orderId} (Clear Unread Badges)
        [HttpPost("read/{orderId}")]
        public async Task<IActionResult> MarkAsRead(int orderId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out int currentUserId)) return Unauthorized();

            var unreadMessages = await _context.Messages
                .Where(m => m.OrderId == orderId && m.ReceiverId == currentUserId && !m.IsRead)
                .ToListAsync();

            unreadMessages.ForEach(m => m.IsRead = true);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}