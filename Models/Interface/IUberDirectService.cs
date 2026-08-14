using Cylo_Backend.Models;

namespace Cylo_Backend.Services
{
    public interface IUberDirectService
    {
        Task<UberQuoteResponse?> CreateDeliveryQuoteAsync(string pickupAddress, string dropoffAddress);
        Task<UberDeliveryResponse?> CreateDeliveryAsync(UberDeliveryRequest request);
    }
}