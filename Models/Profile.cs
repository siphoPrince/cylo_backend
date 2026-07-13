using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Cylo_Backend.Models
{
    public class Profile 
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        [JsonIgnore]
        public User? User { get; set; }
        public string? HandleName { get; set; }
        [Required]
        public string? Name { get; set; }
        public string? Bio {  get; set; }
        public string? SurName { get; set; }
        [Required]
        public string? Phone { get; set; }
        public string? ImageUrl { get; set; }

        // location
        public string? Suburb { get; set; } = "";
        public string? City { get; set; }
        public string? Province { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int SoldCount { get; set; } = 0;
        public DateTime? LastSeen { get; set; }
    }
}
