using Cylo_Backend.Models.DTOs;

namespace Cylo_Backend.Models.Interface
{
    public interface IPaystackService
    {
        Task<PaystackInitializeDataDto?> InitializeTransactionAsync(string email, decimal amountInRand, string reference, Dictionary<string, object>? metadata = null);
        Task<PaystackVerifyDataDto?> VerifyTransactionAsync(string reference);
    }
}
