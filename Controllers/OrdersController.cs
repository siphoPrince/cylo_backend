using Cylo_Backend.Data;
using Cylo_Backend.Models;
using Cylo_Backend.Models.DTOs;
using Cylo_Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cylo_Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly TradeSafeService _tradeSafeService;

        public OrdersController(ApplicationDbContext context, TradeSafeService tradeSafeService)
        {
            _context = context;
            _tradeSafeService = tradeSafeService;
        }

        // GET: api/orders/buying/{userId}
        [HttpGet("buying/{userId}")]
        public async Task<ActionResult<IEnumerable<Order>>> GetBuyingOrders(int userId)
        {
            var orders = await _context.Orders
                .Include(o => o.Post)
                .Where(o => o.BuyerId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return Ok(orders);
        }

        // GET: api/orders/selling/{userId}
        [HttpGet("selling/{userId}")]
        public async Task<ActionResult<IEnumerable<Order>>> GetSellingOrders(int userId)
        {
            var orders = await _context.Orders
                .Include(o => o.Post)
                .Where(o => o.SellerId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return Ok(orders);
        }

        // 1. SELLER ACTION: Start Handover / Delivery
        [HttpPost("{id}/start-delivery")]
        public async Task<IActionResult> StartDelivery(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound(new { message = "Order not found." });

            if (order.Status != "FUNDS_DEPOSITED")
            {
                return BadRequest(new { message = "Cannot start handover until buyer deposits funds into escrow." });
            }

            try
            {
                await _tradeSafeService.StartDeliveryAsync(order.AllocationId);

                order.Status = "DELIVERY_STARTED";
                order.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return Ok(new { message = "Handover process initiated successfully", status = order.Status });
            }
            catch (Exception ex)
            {
                string safeError = ex?.InnerException?.Message ?? ex?.Message ?? "Unknown gateway error during handover.";
                Console.WriteLine($"[ORDERS_CONTROLLER_HANDOVER_ERROR] Order ID: {id} | Details: {safeError}");
                return BadRequest(new { message = "TradeSafe Error: " + safeError });
            }
        }

        // 2. SELLER ACTION: Package Handed to Courier
        [HttpPost("{id}/in-transit")]
        public async Task<IActionResult> MarkInTransit(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound(new { message = "Order not found." });

            try
            {
                await _tradeSafeService.SetAllocationInTransitAsync(order.AllocationId);

                order.Status = "IN_TRANSIT";
                order.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return Ok(new { message = "Order marked as in transit", status = order.Status });
            }
            catch (Exception ex)
            {
                string safeError = ex?.InnerException?.Message ?? ex?.Message ?? "Unknown gateway error.";
                return BadRequest(new { message = "TradeSafe Error: " + safeError });
            }
        }

        // 3. SELLER ACTION: Delivery Completed
        [HttpPost("{id}/complete-delivery")]
        public async Task<IActionResult> CompleteDelivery(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound(new { message = "Order not found." });

            try
            {
                await _tradeSafeService.CompleteAllocationDeliveryAsync(order.AllocationId);

                order.Status = "DELIVERED";
                order.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return Ok(new { message = "Delivery completed. Inspection window open.", status = order.Status });
            }
            catch (Exception ex)
            {
                string safeError = ex?.InnerException?.Message ?? ex?.Message ?? "Unknown gateway error.";
                return BadRequest(new { message = "TradeSafe Error: " + safeError });
            }
        }

        // 4. BUYER ACTION: Accept Delivery & Release Funds Early
        [HttpPost("{id}/release")]
        public async Task<IActionResult> ReleaseFunds(int id)
        {

            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound(new { message = "Order not found." });

            if (order.Status == "COMPLETED")
            {
                return Ok(new { message = "Order already completed.", status = "COMPLETED" });
            }

            // Aligned with PaymentsController allowable states
            var validReleaseStates = new[] { "DELIVERY_STARTED", "IN_TRANSIT", "DELIVERED", "FUNDS_DEPOSITED" };
            if (!validReleaseStates.Contains(order.Status))
            {
                return BadRequest(new { message = $"Cannot release funds at this stage. Current state: {order.Status}" });
            }

            // Wrapped in a transaction to safely handle local database state out-of-sync risks
            using var dbTransaction = await _context.Database.BeginTransactionAsync();

            try
            {
                string originalStatus = order.Status;
                order.Status = "COMPLETED";
                order.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                await _tradeSafeService.ReleaseFundsAsync(order.AllocationId);
                await dbTransaction.CommitAsync();

                return Ok(new { message = "Funds released to seller successfully", status = "COMPLETED" });
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();

                string safeError = ex?.InnerException?.Message ?? ex?.Message ?? "Unknown gateway error during release.";
                Console.WriteLine($"[ORDERS_RELEASE_ROLLBACK] Order ID: {id} | Details: {safeError}");

                return BadRequest(new { message = "TradeSafe Gateway Error. Order state rolled back. Details: " + safeError });
            }
        }

        // 5. BUYER ACTION: Log Dispute
        [HttpPost("{id}/dispute")]
        public async Task<IActionResult> DisputeOrder(int id, [FromBody] ActionReasonRequest request)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound(new { message = "Order not found." });

            if (order.Status == "COMPLETED" || order.Status == "CANCELED" || order.Status == "DISPUTED")
            {
                return BadRequest(new { message = $"Cannot dispute transaction while in state: {order.Status}" });
            }

            try
            {
                await _tradeSafeService.DisputeAllocationAsync(order.AllocationId);

                order.Status = "DISPUTED";
                order.DisputeReason = request.Reason;
                order.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return Ok(new { message = "Order disputed. Funds frozen in escrow.", status = order.Status });
            }
            catch (Exception ex)
            {
                string safeError = ex?.InnerException?.Message ?? ex?.Message ?? "Unknown gateway error during dispute initiation.";
                return BadRequest(new { message = "TradeSafe Error: " + safeError });
            }
        }
    }
}