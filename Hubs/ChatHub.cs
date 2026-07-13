using Cylo_Backend.Data;
using Cylo_Backend.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Cylo_Backend.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;

        public ChatHub(ApplicationDbContext context)
        {
            _context = context;
        }

        private string? GetCurrentUserId()
        {
            return Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? Context.User?.FindFirst("id")?.Value
                ?? Context.UserIdentifier;
        }

        public override async Task OnConnectedAsync()
        {
            var userIdString = GetCurrentUserId();
            if (int.TryParse(userIdString, out int userId))
            {
                var profile = await _context.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
                if (profile != null)
                {
                    profile.LastSeen = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    await Clients.Others.SendAsync("UserPresenceChanged", userId, true, DateTime.UtcNow);
                }
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userIdString = GetCurrentUserId();
            if (int.TryParse(userIdString, out int userId))
            {
                var profile = await _context.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
                if (profile != null)
                {
                    profile.LastSeen = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    await Clients.Others.SendAsync("UserPresenceChanged", userId, false, profile.LastSeen);
                }
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task SendPrivateMessage(int receiverId, string content, int? orderId)
        {
            var senderIdString = GetCurrentUserId();
            if (!int.TryParse(senderIdString, out int senderId)) return;

            // SECURITY GUARD: Validate order participation before saving or routing context
            if (orderId.HasValue)
            {
                var orderExistsAndBelongs = await _context.Orders
                    .AnyAsync(o => o.Id == orderId.Value && (o.BuyerId == senderId || o.SellerId == senderId));

                if (!orderExistsAndBelongs)
                {
                    throw new HubException("Unauthorized: You do not have access to this order context.");
                }
            }

            var newMessage = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = content,
                SentAt = DateTime.UtcNow,
                OrderId = orderId
            };

            _context.Messages.Add(newMessage);
            await _context.SaveChangesAsync();

            var senderProfile = await _context.Profiles.FirstOrDefaultAsync(p => p.UserId == senderId);
            var receiverProfile = await _context.Profiles.FirstOrDefaultAsync(p => p.UserId == receiverId);

            // Payload dispatched to the Receiver
            var receiverPayload = new
            {
                Id = newMessage.Id,
                Content = newMessage.Content,
                SenderId = newMessage.SenderId,
                ReceiverId = newMessage.ReceiverId,
                SentAt = newMessage.SentAt,
                OrderId = newMessage.OrderId,
                LastMessage = newMessage.Content,
                LastMessageTime = newMessage.SentAt,
                UnreadCount = 1,
                OtherUser = new
                {
                    UserId = senderProfile?.UserId ?? senderId,
                    HandleName = senderProfile?.HandleName ?? "User",
                    ImageUrl = senderProfile?.ImageUrl
                }
            };

            // Payload dispatched back to the Caller (Sender)
            var callerPayload = new
            {
                Id = newMessage.Id,
                Content = newMessage.Content,
                SenderId = newMessage.SenderId,
                ReceiverId = newMessage.ReceiverId,
                SentAt = newMessage.SentAt,
                OrderId = newMessage.OrderId,
                LastMessage = newMessage.Content,
                LastMessageTime = newMessage.SentAt,
                UnreadCount = 0,
                OtherUser = new
                {
                    UserId = receiverProfile?.UserId ?? receiverId,
                    HandleName = receiverProfile?.HandleName ?? "User",
                    ImageUrl = receiverProfile?.ImageUrl
                }
            };

            await Clients.User(receiverId.ToString()).SendAsync("ReceiveMessage", receiverPayload);
            await Clients.Caller.SendAsync("ReceiveMessage", callerPayload);
        }

        public async Task EditMessage(int messageId, string newContent)
        {
            var userIdString = GetCurrentUserId();
            if (!int.TryParse(userIdString, out int currentUserId)) return;

            var message = await _context.Messages.FindAsync(messageId);
            if (message == null || message.SenderId != currentUserId)
            {
                throw new HubException("Unauthorized or message not found.");
            }

            message.Content = newContent;
            message.IsEdited = true;
            await _context.SaveChangesAsync();

            // Broadcast mutations to both users
            await Clients.User(message.ReceiverId.ToString()).SendAsync("MessageEdited", new { messageId, newContent });
            await Clients.Caller.SendAsync("MessageEdited", new { messageId, newContent });
        }

        public async Task SendTypingStatus(string recipientId, bool isTyping)
        {
            var senderId = Context.UserIdentifier;
            // Route this only to the specific user's active connection lines
            await Clients.User(recipientId).SendAsync("ReceiveTypingStatus", senderId, isTyping);
        }

        public async Task DeleteMessage(int messageId)
        {
            var userIdString = GetCurrentUserId();
            if (!int.TryParse(userIdString, out int currentUserId)) return;

            var message = await _context.Messages.FindAsync(messageId);
            if (message == null || message.SenderId != currentUserId)
            {
                throw new HubException("Unauthorized or message not found.");
            }

            int receiverId = message.ReceiverId;
            _context.Messages.Remove(message);
            await _context.SaveChangesAsync();

            // Broadcast deletions to both users
            await Clients.User(receiverId.ToString()).SendAsync("MessageDeleted", messageId);
            await Clients.Caller.SendAsync("MessageDeleted", messageId);
        }
    }
}