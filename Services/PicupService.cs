using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cylo_Backend.Services
{
    public interface IPicupService
    {
        Task<bool> RequestFoodDeliveryAsync(string orderId, string recipientName, string phone, string address);
    }

    public class PicupService : IPicupService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<PicupService> _logger;

        public PicupService(HttpClient httpClient, IConfiguration config, ILogger<PicupService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public async Task<bool> RequestFoodDeliveryAsync(string orderId, string recipientName, string phone, string address)
        {
            try
            {
                string baseUrl = _config["Picup:BaseUrl"] ?? "https://staging.picup.co.za";
                string apiKey = _config["Picup:ApiKey"] ?? "";

                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("Picup integration aborted: ApiKey is missing in appsettings.");
                    return false;
                }

                // 1. Structure the outbound HTTP request targeting the core orders creation route
                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/v1/orders");
                request.Headers.Add("api-key", apiKey);

                // 2. Build the production-grade, multi-stop payload matching Picup's routing engine schemas
                var picupOrderPayload = new PicupOrderCreateRequest
                {
                    BusinessReference = orderId,
                    Instructions = "Food delivery: Handle carefully and keep upright.",

                    // Stop 1: The Origin (Your Restaurant location coordinates)
                    PickupStop = new PicupStopDetails
                    {
                        CustomerName = "Cylo Central Kitchen",
                        CustomerPhone = "0111234567",
                        AddressLine1 = "12 Restaurant Boulevard, Food District",
                        Latitude = -26.2041,  // Replace with real restaurant coordinate variables
                        Longitude = 28.0473
                    },

                    // Stop 2: The Destination (The Customer address coordinates parsed to your app)
                    DeliveryStop = new PicupStopDetails
                    {
                        CustomerName = recipientName,
                        CustomerPhone = phone,
                        AddressLine1 = address,
                        Latitude = -26.2415,  // Pass active device geocoding variables here
                        Longitude = 28.0922
                    }
                };

                var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                request.Content = new StringContent(JsonSerializer.Serialize(picupOrderPayload, jsonOptions), Encoding.UTF8, "application/json");

                // 3. Fire payload off to Picup's sandbox or live production server environment
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Picup successfully registered delivery for Cylo Order: {Id}", orderId);
                    return true;
                }

                var errorDetails = await response.Content.ReadAsStringAsync();
                _logger.LogError("Picup API validation failed. Status: {Status}, Details: {Err}", response.StatusCode, errorDetails);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal communication crash attempting to ping Picup servers.");
                return false;
            }
        }
    }

    // =========================================================================
    // PICUP OUTBOUND ORDER DATA TRANSFER OBJECTS (DTOs)
    // =========================================================================

    public class PicupOrderCreateRequest
    {
        [JsonPropertyName("business_reference")]
        public string BusinessReference { get; set; } = string.Empty;

        [JsonPropertyName("instructions")]
        public string Instructions { get; set; } = string.Empty;

        [JsonPropertyName("pickup_stop")]
        public PicupStopDetails PickupStop { get; set; } = new();

        [JsonPropertyName("delivery_stop")]
        public PicupStopDetails DeliveryStop { get; set; } = new();
    }

    public class PicupStopDetails
    {
        [JsonPropertyName("customer_name")]
        public string CustomerName { get; set; } = string.Empty;

        [JsonPropertyName("customer_phone")]
        public string CustomerPhone { get; set; } = string.Empty;

        [JsonPropertyName("address_line1")]
        public string AddressLine1 { get; set; } = string.Empty;

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
    }
}
