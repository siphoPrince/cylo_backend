using System.ComponentModel.DataAnnotations.Schema;

namespace Cylo_Backend.Models
{
    public class Bookmark
    {
        public int Id { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }
        public User User { get; set; }

        public int PostId { get; set; }
        public Post Post { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
