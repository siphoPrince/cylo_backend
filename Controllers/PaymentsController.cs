using Cylo_Backend.Data;
using Cylo_Backend.Models;
using Cylo_Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cylo_Backend.Models.DTOs;

namespace Cylo_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentsController : ControllerBase
    {
        private readonly TradeSafeService _tradeSafe;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public PaymentsController(TradeSafeService tradeSafe, ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _tradeSafe = tradeSafe;
            _context = context;
            _environment = environment;
        }

        [HttpPost("onboard/{userId}")]
        public async Task<IActionResult> OnboardUser(int userId, [FromBody] OnboardRequest request)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound("User not found");

            if (!string.IsNullOrEmpty(user.TradeSafeRecipientId))
            {
                return Ok(new { tradeSafeId = user.TradeSafeRecipientId, message = "User is already verified." });
            }

            user.Name = request.BusinessName;
            user.Mobile = request.ContactPhone;
            user.Email = request.Email;

            try
            {
                var tradeSafeId = await _tradeSafe.CreateUserTokenAsync(user);
                user.TradeSafeRecipientId = tradeSafeId;
                await _context.SaveChangesAsync();

                return Ok(new { tradeSafeId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("create-transaction")]
        public async Task<IActionResult> CreateTransaction([FromBody] TransactionRequestDto req)
        {
            var buyer = await _context.Users.FindAsync(req.BuyerId);
            var seller = await _context.Users.FindAsync(req.SellerId);

            if (buyer == null || seller == null)
                return NotFound(new { message = "One or more users not found." });

            if (string.IsNullOrEmpty(buyer.TradeSafeRecipientId) || string.IsNullOrEmpty(seller.TradeSafeRecipientId))
            {
                return BadRequest(new { message = "Both buyer and seller must be onboarded with TradeSafe." });
            }

            try
            {
                TradeSafeTransactionResponse result = await _tradeSafe.CreateTransactionAsync(
                    buyer.TradeSafeRecipientId,
                    seller.TradeSafeRecipientId,
                    req.Amount,
                    req.ItemDescription
                );

                if (string.IsNullOrEmpty(result.TransactionId) || string.IsNullOrEmpty(result.AllocationId))
                {
                    throw new Exception("TradeSafe did not return the required IDs.");
                }

                var newOrder = new Order
                {
                    TradeSafeId = result.TransactionId,
                    AllocationId = result.AllocationId,
                    CheckoutUrl = result.CheckoutUrl,
                    BuyerId = req.BuyerId,
                    SellerId = req.SellerId,
                    PostId = req.PostId,
                    Amount = req.Amount,
                    Status = "CREATED",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Orders.Add(newOrder);
                await _context.SaveChangesAsync();

                return Ok(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Transaction Error: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("complete-purchase/{postId}")]
        public async Task<IActionResult> CompletePurchase(int postId)
        {
            var post = await _context.Posts.FindAsync(postId);
            if (post == null) return NotFound();

            post.Quantity -= 1;

            if (post.Quantity <= 0)
            {
                // Soft execution layout: DB removal runs smoothly without failing 
                // if disk operations lock or files are shared across entities.
                if (!string.IsNullOrEmpty(post.MediaUrl))
                {
                    try
                    {
                        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                        var filePath = Path.Combine(uploadsFolder, Path.GetFileName(post.MediaUrl));

                        // Only delete if no other active posts rely on this exact media string
                        bool isShared = await _context.Posts.AnyAsync(p => p.Id != postId && p.MediaUrl == post.MediaUrl);
                        if (!isShared && System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }
                    catch (Exception fileEx)
                    {
                        // Log file exception but don't crash the database transaction
                        Console.WriteLine($"[MEDIA CLEANUP ERROR] Handled quietly: {fileEx.Message}");
                    }
                }
                _context.Posts.Remove(post);
            }
            else
            {
                _context.Entry(post).State = EntityState.Modified;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Inventory updated", remaining = Math.Max(0, post.Quantity) });
        }

        [HttpPost("cancel-transaction/{orderId}")]
        public async Task<IActionResult> CancelTransaction(int orderId, [FromBody] ActionReasonRequest req)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound("Order not found");

            try
            {
                await _tradeSafe.CancelTransactionAsync(order.TradeSafeId, req.Reason);

                order.Status = "CANCELED";
                order.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Cancellation request sent", state = order.Status });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("start-handover/{orderId}")]
        public async Task<IActionResult> StartHandover(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound(new { message = "Order not found." });

            if (order.Status != "FUNDS_DEPOSITED")
            {
                return BadRequest(new { message = "Cannot start handover until buyer deposits funds." });
            }

            if (string.IsNullOrEmpty(order.AllocationId))
            {
                return BadRequest(new { message = "This order is missing a valid TradeSafe Allocation ID. Cannot proceed." });
            }

            try
            {
                await _tradeSafe.StartDeliveryAsync(order.AllocationId);

                order.Status = "DELIVERY_STARTED";
                order.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Handover started successfully!", status = order.Status });
            }
            catch (Exception ex)
            {
                string safeErrorMessage = ex?.InnerException?.Message ?? ex?.Message ?? "Unknown gateway error occurred.";
                Console.WriteLine("[HANDOVER_CRASH_LOG] Order ID: " + orderId + " | Details: " + safeErrorMessage);

                return BadRequest(new { message = "TradeSafe Error: " + safeErrorMessage });
            }
        }

        [HttpPost("release-funds/{orderId}")]
        public async Task<IActionResult> ReleaseFunds(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound("Order not found");

            if (order.Status == "COMPLETED")
            {
                return Ok(new { message = "Order already completed.", status = "COMPLETED" });
            }

            var validReleaseStates = new[] { "DELIVERY_STARTED", "IN_TRANSIT", "FUNDS_DEPOSITED" };
            if (!validReleaseStates.Contains(order.Status))
            {
                return BadRequest(new { message = $"Cannot release funds at this current stage of transaction. Current state: {order.Status}" });
            }

            using var dbTransaction = await _context.Database.BeginTransactionAsync();

            try
            {
                order.Status = "COMPLETED";
                order.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _tradeSafe.ReleaseFundsAsync(order.AllocationId);
                await dbTransaction.CommitAsync();

                return Ok(new { message = "Funds released successfully!", status = "COMPLETED" });
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();

                string safeMsg = ex?.InnerException?.Message ?? ex?.Message ?? "Unknown gateway error during release.";
                Console.WriteLine($"[RELEASE FUNDS ROLLBACK] Order ID: {orderId} | Details: {safeMsg}");

                return BadRequest(new
                {
                    message = "TradeSafe Gateway Error. Your local changes were safely rolled back. Details: " + safeMsg
                });
            }
        }

        [HttpPost("dispute-transaction/{orderId}")]
        public async Task<IActionResult> DisputeTransaction(int orderId, [FromBody] ActionReasonRequest req)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound(new { message = "Transaction record not found." });

            if (order.Status == "COMPLETED" || order.Status == "CANCELED" || order.Status == "DISPUTED")
            {
                return BadRequest(new { message = $"Cannot dispute transaction while in state: {order.Status}" });
            }

            if (string.IsNullOrEmpty(order.AllocationId))
            {
                return BadRequest(new { message = "Order is missing an explicit allocation ID required to register dispute." });
            }

            try
            {
                await _tradeSafe.DisputeAllocationAsync(order.AllocationId);

                order.Status = "DISPUTED";
                order.DisputeReason = req.Reason;
                order.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    status = "DISPUTED",
                    message = "Transaction locked down successfully. Moving to resolution phase."
                });
            }
            catch (Exception ex)
            {
                string safeMsg = ex?.InnerException?.Message ?? ex?.Message ?? "Unknown gateway error during dispute.";
                Console.WriteLine($"[DISPUTE ESCROW CRASH] Order ID: {orderId} | Details: {safeMsg}");
                return BadRequest(new { message = "TradeSafe Gateway Error: " + safeMsg });
            }
        }

        [HttpPost("refund-transaction/{orderId}")]
        public async Task<IActionResult> RefundTransaction(int orderId, [FromBody] ActionReasonRequest req)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound("Order not found");

            if (order.Status != "CREATED" && order.Status != "FUNDS_DEPOSITED")
            {
                return BadRequest(new { message = "Order is too far along for a standard refund. Use the dispute system instead." });
            }

            try
            {
                await _tradeSafe.CancelTransactionAsync(order.TradeSafeId, req.Reason);

                order.Status = "CANCELED";
                order.DisputeReason = req.Reason;
                order.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new { message = "Order successfully voided and cancelled.", state = order.Status });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[REFUND ERROR] Order {orderId}: {ex.Message}");
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("delete-transaction/{orderId}")]
        public async Task<IActionResult> DeleteTransaction(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return NotFound(new { message = $"Order #{orderId} not found." });

            if (order.Status != "CREATED")
            {
                return BadRequest(new { message = "Only pending orders can be removed here." });
            }

            try
            {
                if (!string.IsNullOrEmpty(order.TradeSafeId))
                {
                    await _tradeSafe.DeleteTransactionAsync(order.TradeSafeId);
                }

                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Transaction successfully removed." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DELETE FALLBACK] Order {orderId}: {ex.Message}");

                try
                {
                    await _tradeSafe.CancelTransactionAsync(order.TradeSafeId, "User cleared pending item from dashboard.");

                    order.Status = "CANCELED";
                    order.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        message = "Transaction marked as CANCELED via gateway configuration fallback.",
                        status = "CANCELED"
                    });
                }
                catch (Exception fallbackEx)
                {
                    return BadRequest(new { message = "TradeSafe rejected execution rules.", details = fallbackEx.Message });
                }
            }
        }

        [HttpGet("my-orders/{userId}")]
        public async Task<IActionResult> GetUserOrders(
            string userId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string view = "buying",
            [FromQuery] string search = null)
        {
            if (!int.TryParse(userId, out int parsedUserId))
            {
                return BadRequest("Invalid User ID format.");
            }

            var query = _context.Orders.Include(o => o.Post).AsQueryable();

            if (view.Equals("buying", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(o => o.BuyerId == parsedUserId);
            }
            else
            {
                query = query.Where(o => o.SellerId == parsedUserId);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                if (int.TryParse(search, out int searchedId))
                {
                    query = query.Where(o => o.Id == searchedId || (o.Post != null && o.Post.Title.Contains(search)));
                }
                else
                {
                    query = query.Where(o => o.Post != null && o.Post.Title.Contains(search));
                }
            }

            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new {
                    o.Id,
                    o.BuyerId,
                    o.SellerId,
                    o.Amount,
                    o.Status,
                    o.CheckoutUrl,
                    o.CreatedAt,
                    ItemDescription = o.Post != null ? o.Post.Title : "Unknown Marketplace Item"
                })
                .ToListAsync();

            return Ok(new
            {
                items = orders,
                totalPages = totalPages == 0 ? 1 : totalPages
            });
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> TradeSafeWebhook([FromBody] JsonElement payload)
        {
            // TODO: CRITICAL PRODUCTION CHECK
            // Validate TradeSafe signing header signature here to prevent spoofing.
            // if (!IsWebhookAuthorized(Request.Headers)) return Unauthorized();

            try
            {
                if (!payload.TryGetProperty("id", out var idProp) || !payload.TryGetProperty("state", out var stateProp))
                {
                    return BadRequest(new { message = "Malformed webhook payload. Missing id or state." });
                }

                var transactionId = idProp.GetString();
                var state = stateProp.GetString();
                string? updatedStr = null;

                if (payload.TryGetProperty("updated_at", out var updatedProp))
                {
                    updatedStr = updatedProp.GetString();
                }

                // 🔍 EXTRACT INNER ALLOCATION STATE (The Source of Truth for Fulfillment)
                if (payload.TryGetProperty("allocations", out var allocationsProp) && allocationsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var allocation in allocationsProp.EnumerateArray())
                    {
                        if (allocation.TryGetProperty("state", out var allocStateProp))
                        {
                            var allocState = allocStateProp.GetString();

                            // If the nested allocation state has moved ahead, let it override the root transaction state
                            if (allocState == "INITIATED" || allocState == "DELIVERED" || allocState == "FUNDS_RELEASED")
                            {
                                state = allocState;

                                // Grab the allocation's specific updated timestamp if available
                                if (allocation.TryGetProperty("updated_at", out var allocUpdatedProp))
                                {
                                    updatedStr = allocUpdatedProp.GetString();
                                }
                                break;
                            }
                        }
                    }
                }

                var order = await _context.Orders.FirstOrDefaultAsync(o => o.TradeSafeId == transactionId);
                if (order == null)
                {
                    return NotFound(new { message = $"Order with TradeSafe ID {transactionId} not found." });
                }

                // 🛡️ TIME MACHINE GUARD
                if (!string.IsNullOrEmpty(updatedStr) && DateTime.TryParse(updatedStr, out DateTime incomingTimestamp))
                {
                    if (order.LastTradeSafeUpdate.HasValue && order.LastTradeSafeUpdate.Value >= incomingTimestamp)
                    {
                        return Ok(new { message = "Stale webhook payload ignored safely to prevent state regression." });
                    }

                    order.LastTradeSafeUpdate = incomingTimestamp;
                }

                // Map statuses accurately based on the corrected state string
                string targetedStatus = state switch
                {
                    "FUNDS_RECEIVED" or "DEPOSITED" or "FUNDS_DEPOSITED" => "FUNDS_DEPOSITED",
                    "INITIATED" or "DELIVERY_STARTED" => "DELIVERY_STARTED",
                    "IN_TRANSIT" => "IN_TRANSIT",
                    "DELIVERED" or "COMPLETE_DELIVERY" => "DELIVERED",
                    "FUNDS_RELEASED" or "ACCEPTED" or "COMPLETED" => "COMPLETED",
                    "DISPUTED" or "DISPUTE" => "DISPUTED",
                    "CANCELED" or "CANCELLED" => "CANCELED",
                    _ => order.Status
                };

                if (order.Status == targetedStatus)
                {
                    return Ok(new { message = "Duplicate webhook state execution skipped safely." });
                }

                string originalStatus = order.Status;
                order.Status = targetedStatus;

                if (order.Status == "COMPLETED" && originalStatus != "COMPLETED")
                {
                    var sellerProfile = await _context.Profiles.FirstOrDefaultAsync(p => p.UserId == order.SellerId);
                    if (sellerProfile != null)
                    {
                        sellerProfile.SoldCount += 1;
                    }
                }

                order.UpdatedAt = DateTime.UtcNow;

                string dynamicMessage = targetedStatus switch
                {
                    "FUNDS_DEPOSITED" => $"💰 Payment for Order #{order.Id} has been secured in escrow! You can now start handover.",
                    "CANCELED" => $"❌ Order #{order.Id} has been cancelled.",
                    "COMPLETED" => $"🎉 Funds released! Order #{order.Id} is complete.",
                    "DELIVERY_STARTED" => $"📦 Handover process initiated for Order #{order.Id}.",
                    "IN_TRANSIT" => $"🚚 Order #{order.Id} has been handed over to the courier and is in transit!",
                    "DELIVERED" => $"✅ Order #{order.Id} marked as delivered. Inspection window open.",
                    _ => $"Order #{order.Id} status updated to {targetedStatus}."
                };

                var partyIds = new List<int> { order.BuyerId, order.SellerId };

                foreach (var partyId in partyIds)
                {
                    var notification = new Notification
                    {
                        UserId = partyId,
                        Type = targetedStatus,
                        Title = "Cylo Order Update",
                        Message = dynamicMessage,
                        ActionUrl = "/orders",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Notifications.Add(notification);
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Webhook processed successfully", mappedTo = targetedStatus });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WEBHOOK ENGINE CRASH] Details: {ex.Message}");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}