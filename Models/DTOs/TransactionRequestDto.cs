using System.ComponentModel.DataAnnotations;

namespace Cylo_Backend.Models.DTOs
{
    public class TransactionRequestDto
    {
        [Required]
        public int OrderId { get; set; }

        [Required]
        public int BuyerId { get; set; } 

        [Required]
        public int SellerId { get; set; } 

        [Required]
        public int PostId { get; set; } 

        [Required]
        public string ItemDescription { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required]
        public string PaymentMethod { get; set; } = "Escrow";
    }
}
