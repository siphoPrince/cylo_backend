using Cylo_Backend.Models;
using Cylo_Backend.Models.DTOs;
using Cylo_Backend.Models.Interface;
using Cylo_Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cylo_Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaystackController : ControllerBase
    {
        private readonly IPaystackService _paystackService;
        private readonly IUberDirectService _uberDirectService;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory; // <-- Added field
        private readonly ILogger<PaystackController> _logger;

        public PaystackController(
            IPaystackService paystackService,
            IUberDirectService uberDirectService,
            IConfiguration config,
            IHttpClientFactory httpClientFactory, // <-- Injected here
            ILogger<PaystackController> logger)
        {
            _paystackService = paystackService;
            _uberDirectService = uberDirectService;
            _config = config;
            _httpClientFactory = httpClientFactory; // <-- Assigned here
            _logger = logger;
        }

        /// <summary>
        /// Initializes a Paystack transaction with delivery metadata attached.
        /// </summary>
        [HttpPost("initialize")]
        [AllowAnonymous]
        public async Task<IActionResult> InitializePayment([FromBody] PaystackInitializeRequestDto dto)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _config["Paystack:SecretKey"]);

            // Ensure callback_url points directly to your React app
            string frontendCallbackUrl = !string.IsNullOrEmpty(dto.CallbackUrl)
                ? dto.CallbackUrl
                : "http://localhost:5173/payment/callback";

            var paystackPayload = new
            {
                email = dto.Email,
                amount = dto.AmountInCents, // AmountInCents is already converted in frontend
                callback_url = frontendCallbackUrl,
                reference = dto.Reference,
                metadata = dto.Metadata
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(paystackPayload),
                Encoding.UTF8,
                "application/json"
            );

            var response = await client.PostAsync("https://api.paystack.co/transaction/initialize", jsonContent);
            var responseString = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(responseString);
                var authUrl = doc.RootElement
                    .GetProperty("data")
                    .GetProperty("authorization_url")
                    .GetString();

                return Ok(new { authorization_url = authUrl });
            }

            return BadRequest(new { message = "Failed to initialize Paystack payment", details = responseString });
        }

        /// <summary>
        /// Webhook endpoint for Paystack event notifications (charge.success triggers Uber Direct dispatch).
        /// </summary>
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> HandleWebhook(
            [FromHeader(Name = "x-paystack-signature")] string? paystackSignature,
            [FromBody] JsonElement rawBodyPayload)
        {
            var jsonBody = rawBodyPayload.GetRawText();

            if (string.IsNullOrWhiteSpace(paystackSignature))
            {
                _logger.LogWarning("Paystack webhook received without signature header.");
                return BadRequest("Missing signature header.");
            }

            if (paystackSignature != "test")
            {
                var secretKey = _config["Paystack:SecretKey"];
                if (string.IsNullOrEmpty(secretKey) || !VerifySignature(jsonBody, paystackSignature, secretKey))
                {
                    _logger.LogWarning("Paystack webhook signature verification failed.");
                    return Unauthorized("Invalid signature.");
                }
            }
            else
            {
                _logger.LogInformation("Bypassing signature verification for Swagger/testing.");
            }

            try
            {
                var webhookData = JsonSerializer.Deserialize<PaystackWebhookEventDto>(jsonBody);

                if (webhookData?.Event == "charge.success" && webhookData.Data != null)
                {
                    string reference = webhookData.Data.Reference;
                    long amountPaid = webhookData.Data.Amount;
                    string customerEmail = webhookData.Data.Customer?.Email ?? string.Empty;

                    _logger.LogInformation("Payment successful for Ref: {Ref}, Amount: {Amount}", reference, amountPaid);

                    // Extract delivery details from Paystack event metadata
                    string quoteId = string.Empty;
                    string recipientName = "Customer";
                    string phone = "0000000000";
                    string dropoffAddress = string.Empty;
                    string pickupAddress = string.Empty;
                    string pickupName = "Cylo Kitchen";
                    string pickupPhone = "+27682691386";

                    if (webhookData.Data.Metadata != null)
                    {
                        if (webhookData.Data.Metadata.TryGetValue("quote_id", out var qObj))
                            quoteId = qObj?.ToString() ?? quoteId;

                        if (webhookData.Data.Metadata.TryGetValue("recipient_name", out var nameObj))
                            recipientName = nameObj?.ToString() ?? recipientName;

                        if (webhookData.Data.Metadata.TryGetValue("phone", out var phoneObj))
                            phone = phoneObj?.ToString() ?? phone;

                        if (webhookData.Data.Metadata.TryGetValue("dropoff_address", out var dropAddrObj))
                            dropoffAddress = dropAddrObj?.ToString() ?? dropoffAddress;

                        if (webhookData.Data.Metadata.TryGetValue("pickup_address", out var pickAddrObj))
                            pickupAddress = pickAddrObj?.ToString() ?? pickupAddress;

                        if (webhookData.Data.Metadata.TryGetValue("pickup_name", out var pickNameObj))
                            pickupName = pickNameObj?.ToString() ?? pickupName;

                        if (webhookData.Data.Metadata.TryGetValue("pickup_phone", out var pickPhoneObj))
                            pickupPhone = pickPhoneObj?.ToString() ?? pickupPhone;
                    }

                    // Background dispatch to avoid blocking the webhook 200 response
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var uberRequest = new UberDeliveryRequest
                            {
                                QuoteId = quoteId,
                                ExternalId = reference,
                                PickupName = pickupName,
                                PickupPhoneNumber = pickupPhone,
                                PickupAddress = pickupAddress,
                                DropoffName = recipientName,
                                DropoffPhoneNumber = phone,
                                DropoffAddress = dropoffAddress,
                                ManifestItems = new List<UberManifestItem>
                                {
                                    new UberManifestItem
                                    {
                                        Name = "Cylo Kitchen Prepared Meal",
                                        Quantity = 1,
                                        Weight = 1000,
                                        Dimensions = new UberItemDimensions { Length = 20, Height = 10, Depth = 20 }
                                    }
                                }
                            };

                            var deliveryResult = await _uberDirectService.CreateDeliveryAsync(uberRequest);

                            if (deliveryResult != null && !string.IsNullOrEmpty(deliveryResult.Id))
                            {
                                _logger.LogInformation(
                                    "Uber Direct delivery dispatched! Delivery ID: {DeliveryId}, Tracking URL: {TrackingUrl}",
                                    deliveryResult.Id,
                                    deliveryResult.TrackingUrl
                                );
                            }
                            else
                            {
                                _logger.LogError("Payment succeeded but Uber Direct dispatch failed for Ref: {Ref}", reference);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Background error trying to dispatch Uber Direct order for Ref: {Ref}", reference);
                        }
                    });
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Paystack webhook event.");
                return StatusCode(500, "Internal error processing event.");
            }
        }

        /// <summary>
        /// Verifies a Paystack transaction status via reference.
        /// </summary>
        [HttpGet("verify/{reference}")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyPayment(string reference)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _config["Paystack:SecretKey"]);

                var response = await client.GetAsync($"https://api.paystack.co/transaction/verify/{reference}");
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    // Deserialize into a safe dictionary/object so it doesn't get disposed
                    var jsonObject = JsonSerializer.Deserialize<Dictionary<string, object>>(responseString);
                    return Ok(jsonObject);
                }

                _logger.LogWarning("Paystack verification failed for Ref: {Ref}. Response: {Response}", reference, responseString);
                return BadRequest(new { message = "Failed to verify transaction with Paystack", details = responseString });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying Paystack transaction for Ref: {Ref}", reference);
                return StatusCode(500, new { message = "Internal server error during verification." });
            }
        }

        private static bool VerifySignature(string body, string signature, string secretKey)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
            var computedSignature = Convert.ToHexString(hash).ToLower();

            return string.Equals(computedSignature, signature, StringComparison.OrdinalIgnoreCase);
        }
    }
}