using Cylo_Backend.Data;
using Cylo_Backend.Models;
using Cylo_Backend.Models.DTOs;
using Cylo_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Amazon.S3;
using Amazon.S3.Model;

// Resolve the conflict by creating an explicit alias for your database entity
using DbTag = Cylo_Backend.Models.Tag;

namespace Cylo_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UploadsController : ControllerBase
    {
        private readonly IStorageService _storageService;
        private readonly ApplicationDbContext _context;
        private readonly IAmazonS3 _s3Client;
        private readonly IConfiguration _config;

        public UploadsController(
            IStorageService storageService,
            ApplicationDbContext context,
            IAmazonS3 s3Client,
            IConfiguration config)
        {
            _storageService = storageService;
            _context = context;
            _s3Client = s3Client;
            _config = config;
        }

        [HttpPost("presigned-url")]
        public IActionResult GetPresignedUrl([FromBody] PresignedUrlRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Filename))
                return BadRequest(new { message = "Filename parameter is missing." });

            try
            {
                string bucketName = _config["CloudflareR2:BucketName"];
                string r2PublicDomain = _config["CloudflareR2:PublicUrl"];
                var expiryTime = DateTime.UtcNow.AddMinutes(10);

                var presignedRequest = new GetPreSignedUrlRequest
                {
                    BucketName = bucketName,
                    Key = $"posts/videos/{request.Filename}",
                    Verb = HttpVerb.PUT,
                    Expires = expiryTime,
                    ContentType = request.ContentType
                };

                string uploadUrl = _s3Client.GetPreSignedURL(presignedRequest);
                string publicAssetUrl = $"{r2PublicDomain}/posts/videos/{request.Filename}";

                return Ok(new { uploadUrl, publicAssetUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Edge provisioning failure: {ex.Message}" });
            }
        }

        [HttpPost("create-post")]
        public async Task<IActionResult> CreatePost([FromBody] PostCreateDto incomingData)
        {
            try
            {
                if (string.IsNullOrEmpty(incomingData.DirectMediaUrl))
                    return BadRequest(new { message = "No video asset URL provided from R2 upload phase." });

                var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(claimId, out int userId))
                    return Unauthorized(new { message = "User ID not found in token." });

                var categoryExists = await _context.Categories.AnyAsync(c => c.Id == incomingData.CategoryId);
                if (!categoryExists)
                    return BadRequest(new { message = "Invalid Category selected." });

                var newPost = new Post
                {
                    Title = incomingData.Title,
                    Description = incomingData.Description,
                    Quantity = incomingData.Quantity,
                    Price = incomingData.Price,
                    CategoryId = incomingData.CategoryId,
                    UserId = userId,
                    MediaUrl = incomingData.DirectMediaUrl,
                    Latitude = incomingData.Latitude,
                    Longitude = incomingData.Longitude,
                    CreatedAt = DateTime.UtcNow,
                    Status = "Available",
                    Tags = new List<DbTag>() // Fixed: Uses the clear DbTag alias
                };

                if (!string.IsNullOrWhiteSpace(incomingData.Tags))
                {
                    var tagNames = incomingData.Tags.Split(',')
                                       .Select(t => t.Trim().ToLower())
                                       .Where(t => !string.IsNullOrEmpty(t))
                                       .Distinct();

                    foreach (var tagName in tagNames)
                    {
                        var existingTag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == tagName);
                        if (existingTag != null)
                        {
                            newPost.Tags.Add(existingTag);
                        }
                        else
                        {
                            // Fixed: Explicitly creates your custom database Tag instance
                            newPost.Tags.Add(new DbTag { Name = tagName });
                        }
                    }
                }

                _context.Posts.Add(newPost);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Cylo Post created successfully!",
                    postId = newPost.Id,
                    videoUrl = incomingData.DirectMediaUrl
                });
            }
            catch (Exception ex)
            {
                var structuralError = ex.Message;
                if (ex.InnerException != null)
                {
                    structuralError += " | Inner: " + ex.InnerException.Message;
                    if (ex.InnerException.InnerException != null)
                    {
                        structuralError += " | Deep Inner: " + ex.InnerException.InnerException.Message;
                    }
                }

                return StatusCode(500, new
                {
                    message = "Server database save failure",
                    details = structuralError
                });
            }
        }
    }

    public class PresignedUrlRequestDto
    {
        public string Filename { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }
}