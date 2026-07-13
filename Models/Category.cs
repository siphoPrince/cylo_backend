using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class Category
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string? Name { get; set; }

    public string? IconUrl { get; set; }

    [JsonIgnore]
    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
}