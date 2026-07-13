using Cylo_Backend.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

public class Post
{
    [Key]
    public int Id { get; set; }
    public bool IsSold { get; set; } = false;
    [Required]
    public bool IsDeleted { get; set; } = false;
    public int UserId { get; set; }
    public int Quantity { get; set; } = 1;

    [Required]
    public int CategoryId { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 5)]
    public string? Title { get; set; }

    [Required]
    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    public string Status { get; set; } = "Available";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public double Latitude { get; set; }
    [NotMapped]
    public int CommentCount => Comments?.Count ?? 0;
    public double Longitude { get; set; } 

    public string? MediaUrl { get; set; }
    public int LikeCount { get; set; } = 0;

    [JsonIgnore]
    public virtual User? User { get; set; }

    [JsonIgnore]
    public virtual Category? Category { get; set; }
    [JsonIgnore]
    public virtual ICollection<Like> Likes { get; set; } = new List<Like>();
    public virtual ICollection<Comments> Comments { get; set; } = new List<Comments>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}