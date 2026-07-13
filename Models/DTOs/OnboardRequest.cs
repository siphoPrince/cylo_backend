using System.ComponentModel.DataAnnotations;

namespace Cylo_Backend.Models.DTOs
{
    public class OnboardRequest
    {
        [Required]
        public string BusinessName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Phone]
        public string ContactPhone { get; set; } = string.Empty;

        public string? NationalId { get; set; }
    }
}
