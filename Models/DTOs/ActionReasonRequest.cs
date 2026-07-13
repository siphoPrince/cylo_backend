using System.ComponentModel.DataAnnotations;

namespace Cylo_Backend.Models.DTOs
{
    public class ActionReasonRequest
    {
        [Required]
        public string Reason { get; set; } = string.Empty;

        public string? Details { get; set; }
    }
}
