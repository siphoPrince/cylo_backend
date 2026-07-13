using Cylo_Backend.Data;
using Cylo_Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cylo_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FollowController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public FollowController(ApplicationDbContext context)
        {
            _context = context;
        }
        [Authorize]
        [HttpPost("{targetUserId}")]
        public async Task<IActionResult> ToggleFollow(int targetUserId)
        {
            // 1. Identify the current logged-in user 👤
            // We extract the ID from the JWT token claims
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

            int followerId = int.Parse(userIdClaim);

            // 2. Prevent a user from following themselves 🚫
            if (followerId == targetUserId)
            {
                return BadRequest("You cannot follow yourself.");
            }

            // 3. Check if the follow relationship already exists in the database
            var existingFollow = await _context.Follows
                .FirstOrDefaultAsync(f => f.FollowerId == followerId && f.FollowingId == targetUserId);

            if (existingFollow != null)
            {
                // 4. UNFOLLOW: If it exists, remove it 📉
                _context.Follows.Remove(existingFollow);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Unfollowed successfully", isFollowing = false });
            }
            else
            {
                // 5. FOLLOW: If it doesn't exist, create a new record 📈
                var follow = new Follow
                {
                    FollowerId = followerId,
                    FollowingId = targetUserId
                };

                _context.Follows.Add(follow);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Followed successfully", isFollowing = true });
            }
        }

        [HttpGet("followers/{userId}")]
        public async Task<IActionResult> GetFollowers(int userId)
        {
            var followers = await _context.Follows
                .Where(f => f.FollowingId == userId)
                .Include(f => f.Follower)
                .ThenInclude(u => u.Profile)
                .Select(f => new
                {
                    id = f.FollowerId,
                    userName = f.Follower.Name,
                    handleName = f.Follower.Profile.HandleName,
                    profileImageUrl = f.Follower.Profile.ImageUrl
                })
                .ToListAsync();

            return Ok(followers);
        }

        [HttpGet("following/{userId}")]
        public async Task<IActionResult> GetFollowing(int userId)
        {
            var following = await _context.Follows
                .Where(f => f.FollowerId == userId)
                .Include(f => f.Following)
                .ThenInclude(u => u.Profile)
                .Select(f => new
                {
                    id = f.FollowingId,
                    userName = f.Following.Name,
                    handleName = f.Following.Profile.HandleName,
                    profileImageUrl = f.Following.Profile.ImageUrl
                })
                .ToListAsync();

            return Ok(following);
        }
    }
}
