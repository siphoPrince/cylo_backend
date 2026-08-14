using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cylo_Backend.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Cylo_Backend.Services
{
    public class UberDirectService : IUberDirectService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;
        private readonly ILogger<UberDirectService> _logger;

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public UberDirectService(
            HttpClient httpClient,
            IMemoryCache cache,
            IConfiguration config,
            ILogger<UberDirectService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _config = config;
            _logger = logger;
        }

        private string BaseUrl => _config["UberDirect:BaseUrl"] ?? "https://sandbox-api.uber.com";

        /// <summary>
        /// Retrieves and caches the OAuth Bearer token from Uber.
        /// </summary>
        private async Task<string?> GetAccessTokenAsync()
        {
            const string cacheKey = "UberDirect_AccessToken";

            if (_cache.TryGetValue(cacheKey, out string? token) && !string.IsNullOrEmpty(token))
            {
                return token;
            }

            try
            {
                var clientId = _config["UberDirect:ClientId"];
                var clientSecret = _config["UberDirect:ClientSecret"];

                var authContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", clientId!),
                    new KeyValuePair<string, string>("client_secret", clientSecret!),
                    new KeyValuePair<string, string>("grant_type", "client_credentials"),
                    new KeyValuePair<string, string>("scope", "eats.deliveries direct.organizations")
                });

                var response = await _httpClient.PostAsync("https://auth.uber.com/oauth/v2/token", authContent);

                if (!response.IsSuccessStatusCode)
                {
                    var authError = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Uber OAuth Failed: {StatusCode} - {Error}", response.StatusCode, authError);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var accessToken = doc.RootElement.GetProperty("access_token").GetString()!;
                var expiresInSeconds = doc.RootElement.GetProperty("expires_in").GetInt32();

                // Cache token slightly shorter than expiration window for safety
                _cache.Set(cacheKey, accessToken, TimeSpan.FromSeconds(expiresInSeconds - 300));

                return accessToken;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network or DNS resolution failure while requesting Uber OAuth token.");
                return null;
            }
        }

        /// <summary>
        /// Step 1 in Checkout: Gets delivery fee quote and dropoff duration.
        /// </summary>
        public async Task<UberQuoteResponse?> CreateDeliveryQuoteAsync(string pickupAddressJson, string dropoffAddressJson)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Unable to acquire Uber Direct access token.");
                    return null;
                }

                var customerId = _config["UberDirect:CustomerId"];
                var requestUri = $"{BaseUrl}/v1/customers/{customerId}/delivery_quotes";

                var payload = new
                {
                    pickup_address = pickupAddressJson,
                    dropoff_address = dropoffAddressJson
                };

                var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
                    Content = JsonContent.Create(payload)
                };

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Uber Quote Error: {Error}", error);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<UberQuoteResponse>(content, _jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error in CreateDeliveryQuoteAsync to endpoint {BaseUrl}", BaseUrl);
                return null;
            }
        }

        /// <summary>
        /// Step 2 after payment: Dispatches delivery using quote_id.
        /// </summary>
        public async Task<UberDeliveryResponse?> CreateDeliveryAsync(UberDeliveryRequest deliveryRequest)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("Unable to acquire Uber Direct access token.");
                    return null;
                }

                var customerId = _config["UberDirect:CustomerId"];
                var requestUri = $"{BaseUrl}/v1/customers/{customerId}/deliveries";

                var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
                    Content = JsonContent.Create(deliveryRequest)
                };

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Uber Delivery Error: {Error}", error);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<UberDeliveryResponse>(content, _jsonOptions);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error in CreateDeliveryAsync to endpoint {BaseUrl}", BaseUrl);
                return null;
            }
        }
    }
}