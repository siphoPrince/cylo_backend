using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Cylo_Backend.Data;
using Cylo_Backend.Models;

namespace Cylo_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BookmarksController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public BookmarksController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpPost("{postId}")]
        public async Task<IActionResult> ToggleBookmark(int postId)
        {
            // 1. Get the string ID from the token
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 2. Convert it to int to match your database schema
            if (!int.TryParse(userIdString, out int userId))
            {
                return Unauthorized("Invalid User ID format.");
            }

            // 3. Check if bookmark already exists
            var existing = await _context.Bookmarks
                .FirstOrDefaultAsync(b => b.PostId == postId && b.UserId == userId);

            if (existing != null)
            {
                _context.Bookmarks.Remove(existing);
                await _context.SaveChangesAsync();
                return Ok(new { isBookmarked = false, message = "Removed from saved" });
            }

            // 4. Create new bookmark
            var bookmark = new Bookmark
            {
                PostId = postId,
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Bookmarks.Add(bookmark);
            await _context.SaveChangesAsync();

            return Ok(new { isBookmarked = true, message = "Added to saved" });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetMyBookmarks()
        {
            // 1. Get the string ID from the token
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 2. Convert to int for the query
            if (!int.TryParse(userIdString, out int userId))
            {
                return Unauthorized("Invalid User ID format.");
            }

            // 3. Fetch saved posts for this user
            var savedPosts = await _context.Bookmarks
                .Where(b => b.UserId == userId)
                .Include(b => b.Post)
                .Select(b => new {
                    b.Id,
                    b.CreatedAt,
                    Post = new
                    {
                        b.Post.Id,
                        b.Post.Title,
                        b.Post.Price,
                        b.Post.MediaUrl,
                        b.Post.Description
                    }
                })
                .ToListAsync();

            return Ok(savedPosts);
        }
    }
}