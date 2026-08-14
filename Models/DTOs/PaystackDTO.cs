using System.Text.Json.Serialization;

namespace Cylo_Backend.Models.DTOs
{
    // ==========================================
    // 1. INITIALIZE TRANSACTION DTOs
    // ==========================================

    public class PaystackInitializeRequestDto
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Amount in smallest currency unit (e.g., Cents for ZAR/NGN). R100.00 = 10000.
        /// </summary>
        [JsonPropertyName("amount")]
        public long AmountInCents { get; set; }

        [JsonPropertyName("callback_url")]
        public string CallbackUrl { get; set; } = string.Empty;

        [JsonPropertyName("reference")]
        public string? Reference { get; set; }

        /// <summary>
        /// Optional custom metadata attached to the transaction (e.g., Cylo Order ID, UserId).
        /// </summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public class PaystackInitializeResponseDto
    {
        [JsonPropertyName("status")]
        public bool Status { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public PaystackInitializeDataDto? Data { get; set; }
    }

    public class PaystackInitializeDataDto
    {
        [JsonPropertyName("authorization_url")]
        public string AuthorizationUrl { get; set; } = string.Empty;

        [JsonPropertyName("access_code")]
        public string AccessCode { get; set; } = string.Empty;

        [JsonPropertyName("reference")]
        public string Reference { get; set; } = string.Empty;
    }

    // ==========================================
    // 2. VERIFY TRANSACTION DTOs
    // ==========================================

    public class PaystackVerifyResponseDto
    {
        [JsonPropertyName("status")]
        public bool Status { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public PaystackVerifyDataDto? Data { get; set; }
    }

    public class PaystackVerifyDataDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty; // e.g., "success", "failed"

        [JsonPropertyName("reference")]
        public string Reference { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public long Amount { get; set; }

        [JsonPropertyName("gateway_response")]
        public string GatewayResponse { get; set; } = string.Empty;

        [JsonPropertyName("paid_at")]
        public DateTime? PaidAt { get; set; }

        [JsonPropertyName("channel")]
        public string Channel { get; set; } = string.Empty;

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonPropertyName("customer")]
        public PaystackCustomerDto? Customer { get; set; }

        // ADD THIS PROPERTY TO FIX THE COMPILER ERROR
        /// <summary>
        /// Custom metadata parameters attached to the transaction passed back by the webhook.
        /// </summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }
    }


    public class PaystackCustomerDto
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("customer_code")]
        public string CustomerCode { get; set; } = string.Empty;
    }

    // ==========================================
    // 3. WEBHOOK EVENT DTO
    // ==========================================

    public class PaystackWebhookEventDto
    {
        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty; // e.g., "charge.success"

        [JsonPropertyName("data")]
        public PaystackVerifyDataDto? Data { get; set; }
    }
}