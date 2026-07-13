using Cylo_Backend.Data;
using Cylo_Backend.Models;
using Cylo_Backend.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Cylo_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PostsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. GET: api/Posts (The Optimized Feed - Hides Sold Items)
        [HttpGet]
        public async Task<IActionResult> GetPosts(int pageNumber = 1, int pageSize = 10)
        {
            var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? currentUserId = int.TryParse(claimId, out int userId) ? userId : null;

            // Build base query cleanly
            var query = _context.Posts
                .Where(p => !p.IsSold && p.Quantity > 0);

            int totalCount = await query.CountAsync();
            int itemsToSkip = (pageNumber - 1) * pageSize;

            // We chain AsNoTracking() explicitly right before executing ToListAsync
            // to ensure safe object graph creation during pagination extraction
            var posts = await query
                .OrderByDescending(p => p.CreatedAt)
                .Include(p => p.Category)
                .Include(p => p.User).ThenInclude(u => u.Profile)
                .Include(p => p.Likes)
                .Skip(itemsToSkip)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            // Guard Clause: If page has no posts, return early to prevent Dictionary crashes
            if (!posts.Any())
            {
                return Ok(new PagedResponse<PostResponseDto>
                {
                    Data = new List<PostResponseDto>(),
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    HasNextPage = false,
                    TotalCount = totalCount
                });
            }

            // Fetch counts efficiently in-memory grouped by PostId
            var postIds = posts.Select(p => p.Id).ToList();
            var commentCountsDict = await _context.Comments
                .Where(c => postIds.Contains(c.PostId))
                .GroupBy(c => c.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PostId, x => x.Count);

            var bookmarkedPostIds = currentUserId.HasValue
                ? await _context.Bookmarks.Where(b => b.UserId == currentUserId.Value).Select(b => b.PostId).ToListAsync()
                : new List<int>();

            var response = new PagedResponse<PostResponseDto>
            {
                Data = posts.Select(p => {
                    int realCount = commentCountsDict.TryGetValue(p.Id, out int count) ? count : 0;
                    return MapToDto(p, currentUserId, bookmarkedPostIds, null, realCount);
                }).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                HasNextPage = (pageNumber * pageSize) < totalCount,
                TotalCount = totalCount
            };

            return Ok(response);
        }

        // 2. GET: api/Posts/{id} (For the BuyNow Page)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetPost(int id)
        {
            var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? currentUserId = int.TryParse(claimId, out int userId) ? userId : null;

            var post = await _context.Posts
                .Include(p => p.User).ThenInclude(u => u.Profile)
                .Include(p => p.Category)
                .Include(p => p.Likes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null) return NotFound(new { message = "Post not found." });

            int realCommentCount = await _context.Comments.CountAsync(c => c.PostId == post.Id);

            var isBookmarked = currentUserId.HasValue &&
                               await _context.Bookmarks.AnyAsync(b => b.PostId == id && b.UserId == currentUserId.Value);

            return Ok(MapToDto(post, currentUserId, null, isBookmarked, realCommentCount));
        }

        // 3. GET: api/Posts/explore 🗺️
        [HttpGet("explore")]
        [AllowAnonymous]
        public async Task<IActionResult> GetExploreFeed([FromQuery] ExploreSearchFilterDto filter)
        {
            var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? currentUserId = int.TryParse(claimId, out int userId) ? userId : null;

            // 1. Build the base database query with includes
            var query = _context.Posts
                .Include(p => p.Tags)
                .Include(p => p.Category)
                .Include(p => p.User).ThenInclude(u => u.Profile)
                .Include(p => p.Likes)
                .Where(p => !p.IsSold && p.Quantity > 0)
                .AsQueryable();

            // 2. Apply standard filters
            if (!string.IsNullOrWhiteSpace(filter.Category) && !filter.Category.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.Category.Name.ToLower() == filter.Category.ToLower());
            }

            if (filter.MinPrice.HasValue) query = query.Where(p => p.Price >= filter.MinPrice.Value);
            if (filter.MaxPrice.HasValue) query = query.Where(p => p.Price <= filter.MaxPrice.Value);

            if (!string.IsNullOrWhiteSpace(filter.SelectedTag))
            {
                var lowerTag = filter.SelectedTag.ToLower().Trim();
                query = query.Where(p => p.Tags.Any(t => t.Name.ToLower() == lowerTag));
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var lowerSearch = filter.Search.ToLower().Trim();
                query = query.Where(p =>
                    p.Title.ToLower().Contains(lowerSearch) ||
                    p.Description.ToLower().Contains(lowerSearch) ||
                    p.Tags.Any(t => t.Name.ToLower().Contains(lowerSearch))
                );
            }

            // 3. FAST DATABASE FILTER: Apply a bounding box to strip out faraway rows
            if (filter.Lat.HasValue && filter.Lon.HasValue)
            {
                double userLat = filter.Lat.Value;
                double userLon = filter.Lon.Value;
                double radius = filter.RadiusInKm;

                double latChange = radius / 111.0;
                double lonChange = radius / (111.0 * Math.Cos(userLat * Math.PI / 180.0));

                double minLat = userLat - latChange;
                double maxLat = userLat + latChange;
                double minLon = userLon - lonChange;
                double maxLon = userLon + lonChange;

                query = query.Where(p => p.Latitude >= minLat && p.Latitude <= maxLat &&
                                         p.Longitude >= minLon && p.Longitude <= maxLon);
            }

            // 4. EXECUTE QUERY: Bring only matching candidates into application memory
            var potentialPosts = await query
                .OrderByDescending(p => p.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            // 5. ACCURATE FILTER: Filter down to the precise circular radius
            if (filter.Lat.HasValue && filter.Lon.HasValue)
            {
                double userLat = filter.Lat.Value;
                double userLon = filter.Lon.Value;

                potentialPosts = potentialPosts.Where(p =>
                    CalculateHaversineDistance(userLat, userLon, p.Latitude, p.Longitude) <= filter.RadiusInKm
                ).ToList();
            }

            // 6. PAGINATION: Slice your items *after* location filters are finalized
            int totalCount = potentialPosts.Count;
            int itemsToSkip = (filter.PageNumber - 1) * filter.PageSize;

            var rawPosts = potentialPosts
                .Skip(itemsToSkip)
                .Take(filter.PageSize)
                .ToList();

            // 7. BATCH OPTIMIZATION: Build DTO mappings and count collections
            var postIds = rawPosts.Select(p => p.Id).ToList();
            var commentCountsDict = await _context.Comments
                .Where(c => postIds.Contains(c.PostId))
                .GroupBy(c => c.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PostId, x => x.Count);

            var bookmarkedPostIds = currentUserId.HasValue
                ? await _context.Bookmarks.Where(b => b.UserId == currentUserId.Value).Select(b => b.PostId).ToListAsync()
                : new List<int>();

            var mappedData = rawPosts.Select(p => {
                int realCount = commentCountsDict.TryGetValue(p.Id, out int count) ? count : 0;
                return MapToDto(p, currentUserId, bookmarkedPostIds, null, realCount);
            }).ToList();

            var response = new PagedResponse<PostResponseDto>
            {
                Data = mappedData,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize,
                HasNextPage = (filter.PageNumber * filter.PageSize) < totalCount,
                TotalCount = totalCount
            };

            return Ok(response);
        }

        private double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            double r = 6371;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return r * c;
        }

        // 4. GET: api/Posts/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserPosts(int userId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? currentUserId = int.TryParse(claimId, out int parsedId) ? parsedId : null;

            int itemsToSkip = (pageNumber - 1) * pageSize;

            var query = _context.Posts.Where(p => p.UserId == userId);
            int totalCount = await query.CountAsync();

            var dbPosts = await query
                .OrderByDescending(p => p.CreatedAt)
                .Include(p => p.Category)
                .Include(p => p.User).ThenInclude(u => u.Profile)
                .Include(p => p.Likes)
                .Skip(itemsToSkip)
                .Take(pageSize)
                .ToListAsync();

            var postIds = dbPosts.Select(p => p.Id).ToList();
            var commentCountsDict = await _context.Comments
                .Where(c => postIds.Contains(c.PostId))
                .GroupBy(c => c.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PostId, x => x.Count);

            var bookmarkedPostIds = currentUserId.HasValue
                ? await _context.Bookmarks.Where(b => b.UserId == currentUserId.Value).Select(b => b.PostId).ToListAsync()
                : new List<int>();

            var mappedData = dbPosts.Select(p => {
                int realCount = commentCountsDict.TryGetValue(p.Id, out int count) ? count : 0;
                return MapToDto(p, currentUserId, bookmarkedPostIds, null, realCount);
            }).ToList();

            var response = new PagedResponse<PostResponseDto>
            {
                Data = mappedData,
                PageNumber = pageNumber,
                PageSize = pageSize,
                HasNextPage = (pageNumber * pageSize) < totalCount,
                TotalCount = totalCount
            };

            return Ok(response);
        }

        // 5. GET: api/Posts/category/{categoryName}
        [HttpGet("category/{categoryName}")]
        public async Task<IActionResult> GetPostsByCategory(string categoryName, int pageNumber = 1, int pageSize = 10)
        {
            int itemsToSkip = (pageNumber - 1) * pageSize;

            var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? currentUserId = int.TryParse(claimId, out int userId) ? userId : null;

            var query = _context.Posts
                .Where(p => p.Category != null && p.Category.Name.ToLower() == categoryName.ToLower() && !p.IsSold);

            int totalCount = await query.CountAsync();

            var posts = await query
                .OrderByDescending(p => p.CreatedAt)
                .Include(p => p.User).ThenInclude(u => u.Profile)
                .Include(p => p.Likes)
                .Include(p => p.Category)
                .Skip(itemsToSkip)
                .Take(pageSize)
                .ToListAsync();

            var postIds = posts.Select(p => p.Id).ToList();
            var commentCountsDict = await _context.Comments
                .Where(c => postIds.Contains(c.PostId))
                .GroupBy(c => c.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PostId, x => x.Count);

            var bookmarkedPostIds = currentUserId.HasValue
                ? await _context.Bookmarks.Where(b => b.UserId == currentUserId.Value).Select(b => b.PostId).ToListAsync()
                : new List<int>();

            var response = new PagedResponse<PostResponseDto>
            {
                Data = posts.Select(p => {
                    int realCount = commentCountsDict.TryGetValue(p.Id, out int count) ? count : 0;
                    return MapToDto(p, currentUserId, bookmarkedPostIds, null, realCount);
                }).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                HasNextPage = (pageNumber * pageSize) < totalCount,
                TotalCount = totalCount
            };

            return Ok(response);
        }

        [Authorize]
        [HttpPost("create-post")] // 👈 Fixed: Explicitly added the custom sub-route path string
        public async Task<IActionResult> CreatePost([FromBody] PostCreateDto dto) // 👈 Fixed: Changed [FromForm] to [FromBody]
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return BadRequest(new { message = "Validation failed", errors = errors });
                }

                var claimId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
                if (string.IsNullOrEmpty(claimId) || !int.TryParse(claimId, out int currentUserId) || currentUserId == 0)
                {
                    return Unauthorized(new { message = "Invalid or expired user session context." });
                }

                if (string.IsNullOrWhiteSpace(dto.DirectMediaUrl))
                {
                    return BadRequest(new { message = "Please upload the image to Cloudflare R2 first and supply the DirectMediaUrl." });
                }

                var newPost = new Post
                {
                    Title = dto.Title,
                    Description = dto.Description,
                    Price = dto.Price,
                    Quantity = dto.Quantity,
                    CategoryId = dto.CategoryId,
                    MediaUrl = dto.DirectMediaUrl,
                    UserId = currentUserId,
                    IsSold = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Posts.Add(newPost);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, postId = newPost.Id, mediaUrl = dto.DirectMediaUrl });
            }
            catch (Exception ex)
            {
                var deepInnerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new
                {
                    message = "Critical backend failure caught in root block.",
                    details = deepInnerMsg
                });
            }
        }


        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePost(int id)
        {
            try
            {
                var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(claimId, out int currentUserId)) return Unauthorized();

                var post = await _context.Posts
                    .Include(p => p.Tags)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (post == null) return NotFound(new { message = "Post not found." });
                if (post.UserId != currentUserId) return Forbid();

                // 1. Fetch all comment IDs first to isolate dependencies
                var relatedCommentIds = await _context.Comments
                    .Where(c => c.PostId == id)
                    .Select(c => c.Id)
                    .ToListAsync();

                // 2. Wipe comment likes completely in one bulk sweep (Deepest Leaf Node)
                if (relatedCommentIds.Any())
                {
                    var commentLikes = _context.CommentLikes.Where(cl => relatedCommentIds.Contains(cl.CommentId));
                    _context.CommentLikes.RemoveRange(commentLikes);
                }

                // 3. Wipe the actual comments
                var relatedComments = _context.Comments.Where(c => c.PostId == id);
                _context.Comments.RemoveRange(relatedComments);

                // 4. Wipe core post likes
                var relatedLikes = _context.Likes.Where(l => l.PostId == id);
                _context.Likes.RemoveRange(relatedLikes);

                // 5. Wipe post bookmarks
                var relatedBookmarks = _context.Bookmarks.Where(b => b.PostId == id);
                _context.Bookmarks.RemoveRange(relatedBookmarks);

                // 6. Clear many-to-many relationship tracking links
                if (post.Tags != null)
                {
                    post.Tags.Clear();
                }

                // 7. Remove the post itself
                _context.Posts.Remove(post);

                await _context.SaveChangesAsync();

                return Ok(new { message = "Post deleted successfully." });
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { message = "Database deletion block caught", details = innerMsg });
            }
        }


        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePost(int id, [FromBody] PostUpdateDto updatedPostDto)
        {
            var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(claimId, out int currentUserId)) return Unauthorized();

            // Fetch tracked entity explicitly to ensure modifications save reliably
            var existingPost = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id);
            if (existingPost == null) return NotFound(new { message = "Post not found." });
            if (existingPost.UserId != currentUserId) return Forbid();

            // Prevent over-posting vulnerabilities by binding exclusively to the DTO properties
            existingPost.Title = updatedPostDto.Title;
            existingPost.Description = updatedPostDto.Description;
            existingPost.Price = updatedPostDto.Price;
            existingPost.CategoryId = updatedPostDto.CategoryId;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return StatusCode(500, "Database error during update.");
            }

            return NoContent();
        }

        [HttpPost("{id}/decrement")]
        [Authorize]
        public async Task<IActionResult> DecrementQuantity(int id)
        {
            var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(claimId, out int currentUserId))
                return Unauthorized(new { message = "Invalid user context." });

            // Enforce isolation with a transaction block to prevent multi-user race conditions
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id);
                if (post == null) return NotFound(new { message = "Post not found." });

                if (post.UserId != currentUserId) return Forbid();
                if (post.Quantity <= 0 || post.IsSold) return BadRequest(new { message = "This item is out of stock." });

                post.Quantity -= 1;

                if (post.Quantity == 0)
                {
                    post.IsSold = true;

                    var sellerProfile = await _context.Profiles.FirstOrDefaultAsync(p => p.UserId == post.UserId);
                    if (sellerProfile != null)
                    {
                        sellerProfile.SoldCount += 1;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { newQuantity = post.Quantity, isSold = post.IsSold });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "An error occurred while updating the item inventory securely.");
            }
        }

        [Authorize]
        [HttpPost("{id}/sold")]
        public async Task<IActionResult> MarkAsSold(int id)
        {
            var claimId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(claimId, out int currentUserId))
                return Unauthorized(new { message = "Invalid user context." });

            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == id);
            if (post == null) return NotFound(new { message = "Post not found." });

            if (post.UserId != currentUserId) return Forbid();

            if (post.IsSold) return BadRequest(new { message = "This item has already been sold." });

            var sellerProfile = await _context.Profiles.FirstOrDefaultAsync(p => p.UserId == post.UserId);
            if (sellerProfile == null) return NotFound(new { message = "Seller profile not found." });

            post.IsSold = true;
            post.Quantity = 0;
            sellerProfile.SoldCount += 1;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new
                {
                    message = "Item successfully marked as sold!",
                    newSoldCount = sellerProfile.SoldCount,
                    isItemSoldOut = post.IsSold
                });
            }
            catch (DbUpdateException)
            {
                return StatusCode(500, "An error occurred while finalizing the sale.");
            }
        }

        private async Task<bool> ExecuteStockReductionAsync(Post post)
        {
            if (post.Quantity > 0)
            {
                post.Quantity -= 1;

                if (post.Quantity == 0)
                {
                    post.IsSold = true;

                    var sellerProfile = await _context.Profiles.FirstOrDefaultAsync(p => p.UserId == post.UserId);
                    if (sellerProfile != null)
                    {
                        sellerProfile.SoldCount += 1;
                    }
                }

                return true;
            }

            return false;
        }

        // Modified signature with an optional 5th parameter defaulting to 0 to safely bridge old and new calls
        private PostResponseDto MapToDto(Post post, int? currentUserId, List<int>? bookmarkedPostIds = null, bool? singleBookmarkStatus = null, int realCommentCount = 0)
        {
            bool isBookmarked = false;
            if (singleBookmarkStatus.HasValue)
            {
                isBookmarked = singleBookmarkStatus.Value;
            }
            else if (bookmarkedPostIds != null)
            {
                isBookmarked = bookmarkedPostIds.Contains(post.Id);
            }

            int sellerSoldCount = post.User?.Profile?.SoldCount ?? 0;

            return new PostResponseDto
            {
                Id = post.Id,
                UserId = post.UserId,
                Title = post.Title,
                Name = post.User?.Profile?.Name,
                HandleName = post.User?.Profile?.HandleName,
                SurName = post.User?.Profile?.SurName,
                SellerEmail = post.User?.Email,
                SellerPhone = post.User?.Profile?.Phone,
                Description = post.Description,
                Bio = post.User?.Profile?.Bio,
                ProfilePictureUrl = post.User?.Profile?.ImageUrl,
                Price = post.Price,
                MediaUrl = post.MediaUrl,
                Quantity = post.Quantity,
                IsSold = post.IsSold,
                IsVerified = sellerSoldCount >= 12,
                CommentCount = realCommentCount, // Safely mapped using the dictionary lookup values
                LikeCount = post.LikeCount,
                CategoryName = post.Category != null ? post.Category.Name : "General",
                IsLikedByCurrentUser = currentUserId.HasValue &&
                                       post.Likes != null &&
                                       post.Likes.Any(l => l.UserId == currentUserId.Value),
                IsBookmarkedByCurrentUser = isBookmarked
            };
        }
    }
}