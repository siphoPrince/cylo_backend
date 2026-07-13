using Cylo_Backend.Data;
using Cylo_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Cylo_Backend.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/notifications/unread-counts/5
        [HttpGet("unread-counts")]
        public async Task<IActionResult> GetUnreadCounts()
        {
            // Extract userId securely from the JWT token claims instead of the URL path
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "Invalid user token configuration." });
            }

            // Count unread system activity notifications
            var unreadNotifications = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead && n.Type != "MESSAGE")
                .CountAsync();

            // Count unread direct messages 
            var unreadMessages = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsRead && n.Type == "MESSAGE")
                .CountAsync();

            return Ok(new
            {
                notificationsCount = unreadNotifications,
                messagesCount = unreadMessages
            });
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetUserNotifications(int userId)
        {
            // Fetch the latest 50 notifications for this user, ordered by newest first
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();

            return Ok(notifications);
        }

        // POST: api/notifications/mark-all-read/5
        [HttpPost("mark-all-read/{userId}")]
        public async Task<IActionResult> MarkAllAsRead(int userId, [FromQuery] string type)
        {
            var query = _context.Notifications.Where(n => n.UserId == userId && !n.IsRead);

            if (type == "MESSAGE")
                query = query.Where(n => n.Type == "MESSAGE");
            else
                query = query.Where(n => n.Type != "MESSAGE");

            var unreadList = await query.ToListAsync();
            foreach (var notif in unreadList)
            {
                notif.IsRead = true;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Status updated to read." });
        }
    }
}