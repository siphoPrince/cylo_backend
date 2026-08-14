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

        private int? GetCurrentUserId()
        {
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(claimId, out int userId) ? userId : null;
        }

        [HttpGet("post/{postId}")]
        public async Task<IActionResult> GetCommentsForPost(int postId)
        {
            var currentUserId = GetCurrentUserId();

            var response = await _context.Comments
                .AsNoTracking()
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
                    handleName = c.User != null && c.User.Profile != null && c.User.Profile.HandleName != null
                        ? c.User.Profile.HandleName
                        : "user",
                    profilePictureUrl = c.User != null && c.User.Profile != null && c.User.Profile.ImageUrl != null
                        ? c.User.Profile.ImageUrl
                        : "",
                    likesCount = c.CommentLikes.Count,
                    isLikedByMe = currentUserId.HasValue && c.CommentLikes.Any(l => l.UserId == currentUserId.Value)
                })
                .ToListAsync();

            return Ok(response);
        }

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
                var parentComment = await _context.Comments.FirstOrDefaultAsync(c => c.Id == dto.ParentCommentId.Value);
                if (parentComment == null) return BadRequest("The parent comment does not exist.");
                if (parentComment.PostId != dto.PostId) return BadRequest("Parent comment belongs to a different post.");
            }

            var userProfile = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == currentUserId.Value)
                .Select(u => new {
                    HandleName = u.Profile != null ? u.Profile.HandleName : "user",
                    ImageUrl = u.Profile != null ? u.Profile.ImageUrl : ""
                })
                .FirstOrDefaultAsync();

            var comment = new Comments
            {
                Content = dto.Content,
                PostId = dto.PostId,
                UserId = currentUserId.Value,
                ParentCommentId = dto.ParentCommentId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            var response = new
            {
                id = comment.Id,
                content = comment.Content,
                createdAt = comment.CreatedAt,
                postId = comment.PostId,
                userId = comment.UserId,
                parentCommentId = comment.ParentCommentId,
                handleName = userProfile?.HandleName ?? "user",
                profilePictureUrl = userProfile?.ImageUrl ?? "",
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

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateComment(int id, [FromBody] CommentUpdateDto dto)
        {
            var currentUserId = GetCurrentUserId();
            var comment = await _context.Comments.FindAsync(id);

            if (comment == null) return NotFound();
            if (comment.UserId != currentUserId) return Forbid();
            if (comment.Content == "This comment was deleted.") return BadRequest("Cannot edit a deleted comment.");
            if (string.IsNullOrWhiteSpace(dto.Content)) return BadRequest("Content cannot be empty.");

            comment.Content = dto.Content;
            await _context.SaveChangesAsync();

            return Ok(new { id = comment.Id, content = comment.Content });
        }

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

            bool isSoftDeleted = false;

            if (comment.Replies != null && comment.Replies.Any())
            {
                comment.Content = "This comment was deleted.";
                isSoftDeleted = true;
            }
            else
            {
                _context.Comments.Remove(comment);
            }

            await _context.SaveChangesAsync();
            return Ok(new { id = id, isSoftDeleted = isSoftDeleted });
        }

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
                _context.CommentLikes.Add(new CommentLike
                {
                    CommentId = id,
                    UserId = currentUserId.Value
                });
                isLikedByMe = true;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Handles concurrent rapid-click race conditions cleanly
                isLikedByMe = await _context.CommentLikes.AnyAsync(l => l.CommentId == id && l.UserId == currentUserId.Value);
            }

            var directLikesCount = await _context.CommentLikes.CountAsync(l => l.CommentId == id);
            return Ok(new { isLiked = isLikedByMe, likesCount = directLikesCount });
        }
    }
}