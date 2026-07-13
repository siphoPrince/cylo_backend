namespace Cylo_Backend.Models.DTOs
{
    public class TradeSafeTransactionResponse
    {
        public string TransactionId { get; set; } = string.Empty;
        public string AllocationId { get; set; } = string.Empty;
        public string CheckoutUrl { get; set; } = string.Empty;
    }
}
