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
    public class CommentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CommentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // --- DTOs ---
        public class CommentCreateDto
        {
            public string Content { get; set; } = string.Empty;
            public int PostId { get; set; }
            public int? ParentCommentId { get; set; }
        }

        public class CommentUpdateDto
        {
            public string Content { get; set; } = string.Empty;
        }

        // Helper to safely parse user ID from token
        private int? GetCurrentUserId()
        {
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(claimId, out int userId)) return userId;
            return null;
        }

        // --- GET ALL COMMENTS FOR A POST ---
        [HttpGet("post/{postId}")]
        public async Task<IActionResult> GetCommentsForPost(int postId)
        {
            var currentUserId = GetCurrentUserId();

            var response = await _context.Comments
                .Where(c => c.PostId == postId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new
                {
                    id = c.Id,
                    content = c.Content,
                    createdAt = c.CreatedAt,
                    postId = c.PostId,
                    userId = c.UserId,
                    parentCommentId = c.ParentCommentId,

                    // Explicit conditional expressions translate perfectly to SQL CASE statements
                    handleName = c.User != null && c.User.Profile != null && c.User.Profile.HandleName != null
                        ? c.User.Profile.HandleName
                        : "user",

                    profilePictureUrl = c.User != null && c.User.Profile != null && c.User.Profile.ImageUrl != null
                        ? c.User.Profile.ImageUrl
                        : "",

                    likesCount = _context.CommentLikes.Count(l => l.CommentId == c.Id),
                    isLikedByMe = currentUserId.HasValue && _context.CommentLikes.Any(l => l.CommentId == c.Id && l.UserId == currentUserId.Value)
                })
                .ToListAsync();

            return Ok(response);
        }
        // POST: api/Comments
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> PostComment([FromBody] CommentCreateDto dto)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null) return Unauthorized("Invalid token identity.");

            if (string.IsNullOrWhiteSpace(dto.Content)) return BadRequest("Content cannot be empty.");

            var postExists = await _context.Posts.AnyAsync(p => p.Id == dto.PostId);
            if (!postExists) return NotFound("The post does not exist.");

            if (dto.ParentCommentId.HasValue)
            {
                var parentExists = await _context.Comments.AnyAsync(c => c.Id == dto.ParentCommentId.Value);
                if (!parentExists) return BadRequest("The parent comment you are replying to does not exist.");
            }

            var comment = new Comments // Ensure this matches your actual entity name syntax
            {
                Content = dto.Content,
                PostId = dto.PostId,
                UserId = currentUserId.Value,
                ParentCommentId = dto.ParentCommentId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            // Fetch relations dynamically to construct the return response payload
            var savedComment = await _context.Comments
                .Include(c => c.User)
                    .ThenInclude(u => u.Profile)
                .FirstOrDefaultAsync(c => c.Id == comment.Id);

            if (savedComment == null) return StatusCode(500, "Error saving comment.");

            var response = new
            {
                id = savedComment.Id,
                content = savedComment.Content,
                createdAt = savedComment.CreatedAt,
                postId = savedComment.PostId,
                userId = savedComment.UserId,
                parentCommentId = savedComment.ParentCommentId,
                handleName = savedComment.User?.Profile?.HandleName ?? "user",
                profilePictureUrl = savedComment.User?.Profile?.ImageUrl ?? "",
                likesCount = 0,
                isLikedByMe = false
            };

            return CreatedAtAction(nameof(GetComment), new { id = comment.Id }, response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetComment(int id)
        {
            var comment = await _context.Comments
                .Include(c => c.User).ThenInclude(u => u.Profile)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null) return NotFound();
            
            return Ok(comment);
        }

        // PUT: api/Comments/{id}
        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateComment(int id, [FromBody] CommentUpdateDto dto)
        {
            var currentUserId = GetCurrentUserId();
            var comment = await _context.Comments.FindAsync(id);

            if (comment == null) return NotFound();
            if (comment.UserId != currentUserId) return Forbid();
            if (string.IsNullOrWhiteSpace(dto.Content)) return BadRequest("Content cannot be empty.");

            comment.Content = dto.Content;
            await _context.SaveChangesAsync();

            return Ok(new { id = comment.Id, content = comment.Content });
        }

        // DELETE: api/Comments/{id}
        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var currentUserId = GetCurrentUserId();
            var comment = await _context.Comments
                .Include(c => c.Replies) 
                .FirstOrDefaultAsync(c => c.Id == id);

            if (comment == null) return NotFound();
            if (comment.UserId != currentUserId) return Forbid();

            // Handle Soft Delete gracefully if comment has sub-replies nested underneath it
            if (comment.Replies != null && comment.Replies.Any())
            {
                comment.Content = "This comment was deleted.";
                await _context.SaveChangesAsync();
                return Ok(new { id = comment.Id, isSoftDeleted = true });
            }

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST: api/Comments/{id}/like
        [Authorize]
        [HttpPost("{id}/like")]
        public async Task<IActionResult> ToggleLikeComment(int id)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null) return Unauthorized();

            var commentExists = await _context.Comments.AnyAsync(c => c.Id == id);
            if (!commentExists) return NotFound("Comment not found.");

            var existingLike = await _context.CommentLikes
                .FirstOrDefaultAsync(l => l.CommentId == id && l.UserId == currentUserId.Value);

            bool isLikedByMe;

            if (existingLike != null)
            {
                _context.CommentLikes.Remove(existingLike);
                isLikedByMe = false;
            }
            else
            {
                var commentLike = new CommentLike 
                {
                    CommentId = id,
                    UserId = currentUserId.Value
                };
                _context.CommentLikes.Add(commentLike);
                isLikedByMe = true;
            }

            await _context.SaveChangesAsync();
            var directLikesCount = await _context.CommentLikes.CountAsync(l => l.CommentId == id);
            return Ok(new { isLiked = isLikedByMe, likesCount = directLikesCount });
        }
    }
}