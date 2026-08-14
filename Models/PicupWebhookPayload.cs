using System.Text.Json.Serialization;

namespace Cylo_Backend.Models
{
    public class PicupWebhookPayload
    {
        [JsonPropertyName("order_id")]
        public string OrderId { get; set; } = string.Empty;

        [JsonPropertyName("driver_name")]
        public string? DriverName { get; set; }

        [JsonPropertyName("order_waybill_number")]
        public string OrderWaybillNumber { get; set; } = string.Empty;

        [JsonPropertyName("business_reference")]
        public string BusinessReference { get; set; } = string.Empty;

        [JsonPropertyName("parcel_reference")]
        public string ParcelReference { get; set; } = string.Empty;

        [JsonPropertyName("parcel_state")]
        public string ParcelState { get; set; } = string.Empty;

        [JsonPropertyName("origin_customer_name")]
        public string OriginCustomerName { get; set; } = string.Empty;

        [JsonPropertyName("origin_customer_phone")]
        public string OriginCustomerPhone { get; set; } = string.Empty;

        [JsonPropertyName("destination_customer_name")]
        public string DestinationCustomerName { get; set; } = string.Empty;

        [JsonPropertyName("destination_customer_phone")]
        public string DestinationCustomerPhone { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public DateTimeOffset Timestamp { get; set; }

        [JsonPropertyName("parcel_waybill_number")]
        public string ParcelWaybillNumber { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public int Status { get; set; } // 0: None, 1: Planning, 2: InProgress, 3: Complete

        [JsonPropertyName("status_description")]
        public string StatusDescription { get; set; } = string.Empty;

        [JsonPropertyName("tracking_link")]
        public string TrackingLink { get; set; } = string.Empty;

        [JsonPropertyName("courier_code")]
        public string CourierCode { get; set; } = string.Empty;

        [JsonPropertyName("event_code")]
        public int EventCode { get; set; }

        [JsonPropertyName("event_description")]
        public string EventDescription { get; set; } = string.Empty;

        /// <summary>
        /// Contains POC/POD image URLs for events 30/60, or failure reasons for 35.
        /// </summary>
        [JsonPropertyName("event_arg1")]
        public string? EventArg1 { get; set; }

        [JsonPropertyName("event_arg2")]
        public string? EventArg2 { get; set; }

        [JsonPropertyName("is_last_mile")]
        public bool IsLastMile { get; set; }

        [JsonPropertyName("Vehicle_id")]
        public string? VehicleId { get; set; }

        [JsonPropertyName("location")]
        public string? Location { get; set; }

        [JsonPropertyName("payment_type")]
        public int PaymentType { get; set; }
    }
}
