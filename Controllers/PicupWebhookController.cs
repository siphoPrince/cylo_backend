using Cylo_Backend.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Cylo_Backend.Controllers
{
    [ApiController]
    [Route("api/webhooks/picup")] // Kept clean with a single explicit route
    public class PicupWebhookController : ControllerBase
    {
        private readonly ILogger<PicupWebhookController> _logger;
        // TODO: Inject your Order/Database Service here (e.g., ICyloOrderService)

        public PicupWebhookController(ILogger<PicupWebhookController> logger)
        {
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> HandleCallback([FromBody] PicupWebhookPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.BusinessReference))
            {
                _logger.LogWarning("Received malformed or null Picup webhook payload.");
                return BadRequest("Invalid payload.");
            }

            _logger.LogInformation("Picup Webhook: OrderRef {OrderRef}, Status: {StatusDesc}, Event [{Code}]: {EventDesc}",
                payload.BusinessReference, payload.StatusDescription, payload.EventCode, payload.EventDescription);

            try
            {
                switch (payload.EventCode)
                {
                    case 20: // Driver Assigned
                        _logger.LogInformation("Driver {Driver} assigned to Order {Ref}. Tracking: {Link}",
                            payload.DriverName, payload.BusinessReference, payload.TrackingLink);
                        // TODO: Save payload.TrackingLink and driver info to DB, push notification to user: "Driver is on their way to the restaurant!"
                        break;

                    case 40: // Parcel Collected from Restaurant
                        _logger.LogInformation("Order {Ref} has been picked up by the driver.", payload.BusinessReference);
                        // TODO: Update Cylo order status to "In Transit", notify user: "Your food is on the way!"
                        break;

                    case 60: // Delivery Successful
                        var podLink = payload.EventArg1; // Proof of Delivery image url
                        _logger.LogInformation("Order {Ref} delivered successfully. POD: {Pod}", payload.BusinessReference, podLink);
                        // TODO: Update Cylo order status to "Delivered", save POD link, clear active tracking
                        break;

                    case 35: // Delivery Failed / Driver cannot complete
                    case 70: // Cancelled
                    case 75: // Returned to Restaurant
                        _logger.LogWarning("Order {Ref} stopped/failed with code {Code}. Reason: {Reason}",
                            payload.BusinessReference, payload.EventCode, payload.EventArg1);
                        // TODO: Update Cylo order status to "Failed" or "Cancelled", alert restaurant or support team
                        break;

                    default:
                        _logger.LogInformation("Unhandled intermediate Picup event code: {Code}", payload.EventCode);
                        break;
                }
            }
            catch (Exception ex)
            {
                // Catching errors ensures we still return a 200 OK to Picup so they don't hammer your API with retries
                _logger.LogError(ex, "Error processing DB updates for Picup Order {Ref}", payload.BusinessReference);
            }

            // Always return 200 OK immediately to stop Picup's retry loop
            return Ok();
        }
    }
}
