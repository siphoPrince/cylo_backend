using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Cylo_Backend.Models.DTOs;
using Cylo_Backend.Models.Interface;

namespace Cylo_Backend.Services
{
    public class PaystackService : IPaystackService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<PaystackService> _logger;

        public PaystackService(HttpClient httpClient, IConfiguration config, ILogger<PaystackService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;

            var secretKey = _config["Paystack:SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                _logger.LogError("Paystack SecretKey is missing from appsettings configuration.");
                throw new InvalidOperationException("Paystack secret key is not configured in appsettings.");
            }

            var baseUrl = _config["Paystack:BaseUrl"] ?? "https://api.paystack.co/";
            if (!baseUrl.EndsWith("/"))
            {
                baseUrl += "/";
            }

            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        }

        public async Task<PaystackInitializeDataDto?> InitializeTransactionAsync(
            string email,
            decimal amountInRand,
            string reference,
            Dictionary<string, object>? metadata = null)
        {
            long amountInCents = (long)(amountInRand * 100);

            var nativePayload = new
            {
                email = email,
                amount = amountInCents,
                callback_url = _config["Paystack:CallbackUrl"] ?? string.Empty,
                reference = reference,
                metadata = metadata
            };

            var serializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };

            var jsonString = JsonSerializer.Serialize(nativePayload, serializerOptions);
            var jsonContent = new StringContent(jsonString, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("transaction/initialize", jsonContent);

            if (!response.IsSuccessStatusCode)
            {
                var errorResponseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Paystack Initialize Failed! Status: {Status}, Response: {Body}",
                    response.StatusCode, errorResponseBody);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var resultOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<PaystackInitializeResponseDto>(content, resultOptions);

            return result?.Status == true ? result.Data : null;
        }

        public async Task<PaystackVerifyDataDto?> VerifyTransactionAsync(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                _logger.LogWarning("VerifyTransactionAsync was called with an empty reference.");
                return null;
            }

            // Cleanly encode the reference to prevent bad route formatting
            var safeReference = Uri.EscapeDataString(reference);
            var response = await _httpClient.GetAsync($"transaction/verify/{safeReference}");

            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Paystack Verification Failed for ref: {Ref}. Status: {Status}, Response: {Body}",
                    reference, response.StatusCode, content);
                return null;
            }

            var resultOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<PaystackVerifyResponseDto>(content, resultOptions);

            return result?.Status == true ? result.Data : null;
        }
    }
}