using System.ComponentModel.DataAnnotations.Schema;

namespace Cylo_Backend.Models
{
    public class Order
    {
        public int Id { get; set; }
        public string TradeSafeId { get; set; }
        public string AllocationId { get; set; }
        public int BuyerId { get; set; }
        public int SellerId { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int PostId { get; set; }
        [ForeignKey("PostId")]
        public Post? Post { get; set; }
        // Navigation properties (Optional but recommended for EF Core)
        public User Buyer { get; set; }
        public User Seller { get; set; }
        public string? CheckoutUrl { get; set; }
        public string? DisputeReason { get; set; }        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastTradeSafeUpdate { get; set; }
    }
}