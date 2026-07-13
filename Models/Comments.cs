using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Cylo_Backend.Models
{
    public class Comments
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }

        public int PostId { get; set; }
        [JsonIgnore]
        public Post Post { get; set; }

        public int UserId { get; set; }
        [JsonIgnore]
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        // --- NEW FOR REPLIES ---
        public int? ParentCommentId { get; set; } // Nullable because top-level comments don't have a parent

        [JsonIgnore]
        [ForeignKey("ParentCommentId")]
        public virtual Comments? ParentComment { get; set; }

        public virtual ICollection<Comments> Replies { get; set; } = new List<Comments>();

        // --- NEW FOR LIKES ---
        public virtual ICollection<CommentLike> CommentLikes { get; set; } = new List<CommentLike>();
    }

    // Join table to handle the Many-to-Many relationship (A user can like many comments; a comment can have many likes)
    public class CommentLike
    {
        public int Id { get; set; }
        public int CommentId { get; set; }
        [JsonIgnore]
        public Comments Comment { get; set; }
        public int UserId { get; set; }
        [JsonIgnore]
        public User User { get; set; }
    }
}