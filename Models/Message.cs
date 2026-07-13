namespace Cylo_Backend.Models
{
    public class Message
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public DateTime SentAt { get; set; }
        public bool IsEdited { get; set; } = false;
        // Relationships
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }

        // Optional: Connect it to an Order/Product for context
        public int? OrderId { get; set; }
        public bool IsRead { get; set; } = false;
        public virtual User? Sender { get; set; }
        public virtual User? Receiver { get; set; }
        
    }
}
