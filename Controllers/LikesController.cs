using Cylo_Backend.Data;
using Cylo_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Cylo_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LikesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LikesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [Authorize]
        [HttpPost("toggle/{postId}")]
        public async Task<IActionResult> ToggleLike(int postId)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdString == null) return Unauthorized();

            if (!int.TryParse(userIdString, out int userId))
            {
                return BadRequest("Invalid User ID format");
            }

            // 1. Fetch the post directly
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == postId);
            if (post == null) return NotFound("Post not found");

            // 2. Check if the user already liked it
            var existingLike = await _context.Likes
                .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

            bool isLikedNow;

            if (existingLike != null)
            {
                _context.Likes.Remove(existingLike);
                isLikedNow = false;

                post.LikeCount = Math.Max(0, post.LikeCount - 1);
            }
            else
            {
                var newLike = new Like
                {
                    PostId = postId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Likes.Add(newLike);
                isLikedNow = true;

                post.LikeCount += 1;
            }

            // EF Core executes both the Like table change and Post table update atomically
            await _context.SaveChangesAsync();

            return Ok(new { isLiked = isLikedNow, likeCount = post.LikeCount });
        }
    }
}