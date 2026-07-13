namespace Cylo_Backend.Models
{
    public class Dispute
    {
        public int Id { get; set; }
        public int OrderId { get; set; }

        // Who started the dispute? (Buyer or Seller)
        public string RaisedByUserId { get; set; }

        public string Reason { get; set; }

        // Possible values: "Open", "Under_Review", "Resolved", "Cancelled"
        public string Status { get; set; } = "Open";

        // Detailed description from the user
        public string Description { get; set; }

        // Path to an image showing the issue (optional)
        public string? EvidenceUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ResolvedAt { get; set; }

        // Navigation properties (if using Entity Framework)
        public virtual Order Order { get; set; }
    }
}
