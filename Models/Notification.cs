namespace Cylo_Backend.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public int UserId { get; set; } // The recipient of the notification
        public string Type { get; set; } // "ORDER_PAID", "DELIVERED", "MESSAGE", etc.
        public string Title { get; set; }
        public string Message { get; set; }
        public bool IsRead { get; set; } = false;
        public string ActionUrl { get; set; } // e.g., "/orders/5" or "/chat/12"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
