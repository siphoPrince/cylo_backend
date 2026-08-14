using System.Text.Json.Serialization;

namespace Cylo_Backend.Models.DTOs
{
    public class DeliveryQuoteRequest
    {
        [JsonPropertyName("pickupStreet")]
        public string PickupStreet { get; set; }

        [JsonPropertyName("pickupCity")]
        public string PickupCity { get; set; }

        [JsonPropertyName("pickupZip")]
        public string PickupZip { get; set; }

        [JsonPropertyName("dropoffStreet")]
        public string DropoffStreet { get; set; }

        [JsonPropertyName("dropoffCity")]
        public string DropoffCity { get; set; }

        [JsonPropertyName("dropoffZip")]
        public string DropoffZip { get; set; }
    }
}