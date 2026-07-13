using System.ComponentModel.DataAnnotations;

namespace Cylo_Backend.Models
{
    public class ProfileUpdateDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Bio { get; set; }
        public string? SurName { get; set; }
        [Required]
        public string? Phone { get; set; }
        public string? ImageUrl { get; set; }
        public string? HandleName { get; set; }
        //location
        public string? City { get; set; }
        public string? Province { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Suburb { get; set; }
        public IFormFile? imageFile { get; set; }

    }
}
