using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Cylo_Backend.Models.DTOs
{
    public class PostCreateDto
    {
        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public decimal Price { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        public int CategoryId { get; set; }

        // The URL string sent from the client after uploading directly to Cloudflare R2
        [Required(ErrorMessage = "The verified Cloudflare storage tracking link is required.")]
        public string DirectMediaUrl { get; set; } = string.Empty;

        [Required]
        public double Latitude { get; set; }

        [Required]
        public double Longitude { get; set; }

        public string? Tags { get; set; }

        // Optional: If you ever need to fallback to uploading files directly to your server
        public IFormFile? MediaFile { get; set; }
    }
}