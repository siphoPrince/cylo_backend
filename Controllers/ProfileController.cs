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
    [Authorize] // Protects all routes in this controller
    public class ProfileController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProfileController(ApplicationDbContext context,IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet("search")]
        [AllowAnonymous] // Allows unauthenticated users to search for creators on the Explore page
        public async Task<IActionResult> SearchProfiles([FromQuery] string query, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Search query cannot be empty.");

            var lowerQuery = query.ToLower();

            // Query matches against Name, Surname, or HandleName
            var profiles = await _context.Profiles
                .Include(p => p.User)
                .Where(p => p.Name.ToLower().Contains(lowerQuery)
                         || p.SurName.ToLower().Contains(lowerQuery)
                         || p.HandleName.ToLower().Contains(lowerQuery))
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new {
                    p.UserId,
                    p.Name,
                    p.SurName,
                    p.HandleName,
                    p.ImageUrl,
                    p.Suburb,
                    p.City
                })
                .ToListAsync();

            return Ok(profiles);
        }
        [HttpGet("{userId}")]
        public async Task<ActionResult<Profile>> GetProfile(int userId)
        {
            var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? currentUserId = int.TryParse(claimId, out int id) ? id : null;

            var profile = await _context.Profiles
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null) return NotFound(new { message = "Profile not found." });

            bool isSellerPayable = profile.User != null &&
                (!string.IsNullOrEmpty(profile.User.TradeSafeRecipientId));

            bool isFollowing = false;
            if (currentUserId.HasValue)
            {
                isFollowing = await _context.Follows
                    .AnyAsync(f => f.FollowerId == currentUserId && f.FollowingId == userId);
            }

            var followersCount = await _context.Follows.CountAsync(f => f.FollowingId == userId);
            var followingCount = await _context.Follows.CountAsync(f => f.FollowerId == userId);

            return Ok(new
            {
                profile = new
                {
                    profile.Id,
                    profile.UserId,
                    profile.HandleName,
                    profile.Name,
                    profile.SurName,
                    profile.Bio,
                    profile.Phone,
                    profile.ImageUrl,
                    profile.SoldCount,
                    Suburb = profile.Suburb ?? "",
                    City = profile.City ?? "",
                    Province = profile.Province ?? "",
                    profile.Latitude,
                    profile.Longitude
                },
                followersCount,
                followingCount,
                isFollowing,
                paymentStatus = new
                {
                    isPayable = isSellerPayable,
                    sellerId = userId,
                    isFicaVerified = profile.User?.IsFicaVerified ?? false,
                    preferredGateway = !string.IsNullOrEmpty(profile.User?.TradeSafeRecipientId) ? "TradeSafe" : "Paystack"
                }
            });
        }

        [HttpPost("save")]
        public async Task<ActionResult<Profile>> SaveProfile([FromForm] ProfileUpdateDto incomingProfile)
        {
            try
            {
                var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(claimId, out int currentUserId)) return Unauthorized();

                // --- IMAGE HANDLING ---
                if (incomingProfile.imageFile != null && incomingProfile.imageFile.Length > 0)
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(incomingProfile.imageFile.FileName);
                    var uploadPath = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");

                    if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);
                    var filePath = Path.Combine(uploadPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await incomingProfile.imageFile.CopyToAsync(stream);
                    }
                    incomingProfile.ImageUrl = fileName;
                }

                var existingProfile = await _context.Profiles
                    .FirstOrDefaultAsync(p => p.UserId == currentUserId);

                if (existingProfile == null)
                {
                    // Create new profile entity from DTO data
                    var newProfile = new Profile
                    {
                        UserId = currentUserId,
                        Name = incomingProfile.Name,
                        SurName = incomingProfile.SurName,
                        HandleName = incomingProfile.HandleName,
                        Bio = incomingProfile.Bio,
                        Phone = incomingProfile.Phone,
                        // Fixed: Mapping Suburb correctly on creation if incomingProfile DTO supports it
                        Suburb = incomingProfile.Suburb ?? "",
                        City = incomingProfile.City,
                        Province = incomingProfile.Province,
                        Latitude = incomingProfile.Latitude,
                        Longitude = incomingProfile.Longitude,
                        ImageUrl = incomingProfile.ImageUrl
                    };

                    _context.Profiles.Add(newProfile);
                    await _context.SaveChangesAsync();
                    return Ok(newProfile);
                }
                else
                {
                    // Update existing entity from DTO data
                    existingProfile.Name = incomingProfile.Name;
                    existingProfile.SurName = incomingProfile.SurName;
                    existingProfile.HandleName = incomingProfile.HandleName;
                    existingProfile.Bio = incomingProfile.Bio;
                    existingProfile.Phone = incomingProfile.Phone;
                    // Fixed: Save updated Suburb mapping securely to the DB context
                    existingProfile.Suburb = incomingProfile.Suburb ?? "";
                    existingProfile.City = incomingProfile.City;
                    existingProfile.Province = incomingProfile.Province;
                    existingProfile.Latitude = incomingProfile.Latitude;
                    existingProfile.Longitude = incomingProfile.Longitude;

                    if (!string.IsNullOrEmpty(incomingProfile.ImageUrl))
                    {
                        existingProfile.ImageUrl = incomingProfile.ImageUrl;
                    }

                    await _context.SaveChangesAsync();
                    return Ok(existingProfile);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("explore")]
        [AllowAnonymous]
        public async Task<IActionResult> Explore(
            [FromQuery] string? search,
            [FromQuery] string? category,
            [FromQuery] decimal? minPrice,
            [FromQuery] decimal? maxPrice,
            [FromQuery] string? suburb,
            [FromQuery] string? city,
            [FromQuery] string? province)
        {
            // 1. Core query joining your Posts to the author's Profile record
            var query = from post in _context.Posts
                        join profile in _context.Profiles on post.UserId equals profile.UserId
                        select new
                        {
                            Post = post,
                            Profile = profile
                        };

            // 2. Filter by Keyword across text fields
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(q =>
                    q.Post.Title.ToLower().Contains(search) ||
                    q.Post.Description.ToLower().Contains(search));
            }

            // 3. Filter by Category Name property safely
            if (!string.IsNullOrWhiteSpace(category) && category != "All")
            {
                query = query.Where(q => q.Post.Category.Name == category);
            }

            // 4. Filter by Price Range (ZAR)
            if (minPrice.HasValue) query = query.Where(q => q.Post.Price >= minPrice.Value);
            if (maxPrice.HasValue) query = query.Where(q => q.Post.Price <= maxPrice.Value);

            // 5. Fixed: Accurate native Suburb matching column logic
            if (!string.IsNullOrWhiteSpace(suburb))
            {
                query = query.Where(q => q.Profile.Suburb == suburb);
            }
            else if (!string.IsNullOrWhiteSpace(city))
            {
                query = query.Where(q => q.Profile.City == city);
            }
            else if (!string.IsNullOrWhiteSpace(province))
            {
                query = query.Where(q => q.Profile.Province == province);
            }

            // 6. Project combined data safely back to the React Application UI
            var results = await query
                .OrderByDescending(q => q.Post.Id)
                .Select(q => new
                {
                    q.Post.Id,
                    q.Post.Title,
                    q.Post.Description,
                    q.Post.Price,
                    q.Post.MediaUrl,
                    HandleName = q.Profile.HandleName,
                    AvatarUrl = q.Profile.ImageUrl,
                    City = q.Profile.City,
                    Province = q.Profile.Province,
                    // Fixed: Serves the authentic database Suburb string directly down to UI elements
                    Suburb = q.Profile.Suburb
                })
                .ToListAsync();

            return Ok(results);
        }
    }
}
