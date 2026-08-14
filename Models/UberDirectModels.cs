using System.Text.Json.Serialization;

namespace Cylo_Backend.Models
{
    // ==========================================
    // QUOTE RESPONSE MODEL
    // ==========================================
    public class UberQuoteResponse
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("created")]
        public DateTime Created { get; set; }

        [JsonPropertyName("expires")]
        public DateTime Expires { get; set; }

        [JsonPropertyName("fee")]
        public int Fee { get; set; } // Fee in cents (e.g. 6500 = R65.00)

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonPropertyName("duration")]
        public int Duration { get; set; } // Total delivery duration in minutes

        [JsonPropertyName("pickup_duration")]
        public int PickupDuration { get; set; }

        [JsonPropertyName("dropoff_eta")]
        public DateTime DropoffEta { get; set; }
    }

    // ==========================================
    // DELIVERY REQUEST MODELS
    // ==========================================
    public class UberDeliveryRequest
    {
        [JsonPropertyName("quote_id")]
        public string QuoteId { get; set; } = string.Empty;

        [JsonPropertyName("pickup_address")]
        public string PickupAddress { get; set; } = string.Empty; // Stringified JSON

        [JsonPropertyName("pickup_name")]
        public string PickupName { get; set; } = string.Empty;

        [JsonPropertyName("pickup_phone_number")]
        public string PickupPhoneNumber { get; set; } = string.Empty;

        [JsonPropertyName("pickup_latitude")]
        public double PickupLatitude { get; set; }

        [JsonPropertyName("pickup_longitude")]
        public double PickupLongitude { get; set; }

        [JsonPropertyName("dropoff_address")]
        public string DropoffAddress { get; set; } = string.Empty; // Stringified JSON

        [JsonPropertyName("dropoff_name")]
        public string DropoffName { get; set; } = string.Empty;

        [JsonPropertyName("dropoff_phone_number")]
        public string DropoffPhoneNumber { get; set; } = string.Empty;

        [JsonPropertyName("dropoff_latitude")]
        public double DropoffLatitude { get; set; }

        [JsonPropertyName("dropoff_longitude")]
        public double DropoffLongitude { get; set; }

        [JsonPropertyName("manifest_items")]
        public List<UberManifestItem> ManifestItems { get; set; } = new();

        [JsonPropertyName("external_id")]
        public string ExternalId { get; set; } = string.Empty; // Your Cylo Order Reference
    }

    public class UberManifestItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("weight")]
        public int Weight { get; set; } = 500; // in grams

        [JsonPropertyName("dimensions")]
        public UberItemDimensions? Dimensions { get; set; }
    }

    public class UberItemDimensions
    {
        [JsonPropertyName("length")]
        public int Length { get; set; } = 20;

        [JsonPropertyName("height")]
        public int Height { get; set; } = 10;

        [JsonPropertyName("depth")]
        public int Depth { get; set; } = 20;
    }

    // ==========================================
    // DELIVERY RESPONSE MODEL
    // ==========================================
    public class UberDeliveryResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("quote_id")]
        public string QuoteId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("complete")]
        public bool Complete { get; set; }

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("fee")]
        public int Fee { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonPropertyName("tracking_url")]
        public string TrackingUrl { get; set; } = string.Empty;

        [JsonPropertyName("created")]
        public DateTime Created { get; set; }
    }
}