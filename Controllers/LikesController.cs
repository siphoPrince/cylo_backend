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
    public class LikesController : ControllerBase // Changed to ControllerBase since this is an API
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

            // 1. Fetch the post directly to let EF map the properties and update the columns safely
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == postId);
            if (post == null) return NotFound("Post not found");

            // 2. Check if the user already liked it
            var existingLike = await _context.Likes
                .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

            bool isLikedNow;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (existingLike != null)
                {
                    _context.Likes.Remove(existingLike);
                    isLikedNow = false;

                    // Safe update using EF properties directly instead of raw SQL strings
                    post.LikeCount = Math.Max(0, post.LikeCount - 1);
                }
                else
                {
                    var newLike = new Like
                    {
                        PostId = postId,
                        UserId = userId
                    };
                    _context.Likes.Add(newLike);
                    isLikedNow = true;

                    post.LikeCount += 1;
                }

                // Save everything inside the atomic transaction block safely
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                // This will display the exact column naming mismatch if the database fails here!
                var deepError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { message = "Like transaction failed", details = deepError });
            }

            return Ok(new { isLiked = isLikedNow, likeCount = post.LikeCount });
        }
    }
}