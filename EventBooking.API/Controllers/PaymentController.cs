using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using EventBooking.API.Models;
using EventBooking.API.Models.Payment;
using EventBooking.API.Models.Payments;
using EventBooking.API.Data;
using EventBooking.API.DTOs;

namespace EventBooking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger;
        private readonly AppDbContext _context;

        public PaymentController(
            IConfiguration configuration,
            ILogger<PaymentController> logger,
            AppDbContext context)
        {
            _configuration = configuration;
            _logger = logger;
            _context = context;
            StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
        }

        [HttpGet("config")]
        [AllowAnonymous]
        public IActionResult GetConfig()
        {
            return Ok(new PaymentConfig 
            { 
                PublishableKey = _configuration["Stripe:PublishableKey"] ?? string.Empty 
            });
        }

        [HttpPost("create-payment-intent")]
        [AllowAnonymous]
        public async Task<ActionResult<CreatePaymentIntentResponse>> CreatePaymentIntent(
            [FromBody] CreatePaymentIntentRequest request)
        {
            try
            {
                _logger.LogInformation("Received payment intent request: {@Request}", request);

                if (request == null)
                {
                    return BadRequest("Request cannot be null");
                }

                // Create payment intent directly with the amount from the request
                var options = new PaymentIntentCreateOptions
                {
                    Amount = request.Amount, // Amount is already in cents from frontend
                    Currency = request.Currency.ToUpperInvariant(),
                    Description = request.Description,
                    ReceiptEmail = request.Email, // Set receipt email
                    AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                    {
                        Enabled = true,
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        { "eventId", request.EventId.ToString() },
                        { "eventTitle", request.EventTitle },
                        { "ticketDetails", request.TicketDetails },
                        { "customerEmail", request.Email ?? "" } // Store email in metadata as well
                    }
                };

                // Add food details to metadata if present
                if (!string.IsNullOrEmpty(request.FoodDetails))
                {
                    options.Metadata.Add("foodDetails", request.FoodDetails);
                }

                _logger.LogInformation("Creating Stripe payment intent with amount: {Amount} {Currency}, Email: {Email}", 
                    request.Amount, 
                    request.Currency.ToUpperInvariant(),
                    request.Email);

                var service = new PaymentIntentService();
                var paymentIntent = await service.CreateAsync(options);

                _logger.LogInformation("Created payment intent: {PaymentIntentId} with receipt email: {Email}", 
                    paymentIntent.Id,
                    paymentIntent.ReceiptEmail);

                return Ok(new CreatePaymentIntentResponse 
                { 
                    ClientSecret = paymentIntent.ClientSecret,
                });
            }              catch (Exception ex)
            {
                var errorMessage = $"Error creating payment intent: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Inner exception: {ex.InnerException.Message}";
                }
                _logger.LogError(ex, errorMessage);
                return StatusCode(500, errorMessage);
            }
        }

        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> HandleStripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            _logger.LogInformation("Received webhook from Stripe");
            
            try
            {
                // Try to get the webhook secret
                var webhookSecret = _configuration["Stripe:WebhookSecret"];

                // If we have a webhook secret, try to construct the event
                if (!string.IsNullOrEmpty(webhookSecret) && 
                    Request.Headers.TryGetValue("Stripe-Signature", out var signature))
                {
                    try
                    {
                        var stripeEvent = EventUtility.ConstructEvent(
                            json,
                            signature,
                            webhookSecret
                        );

                        if (stripeEvent.Type == "payment_intent.succeeded")
                        {
                            var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                            _logger.LogInformation("Payment succeeded: {PaymentIntentId}", paymentIntent?.Id);
                            
                            // TODO: Update booking status to confirmed
                            // TODO: Send confirmation email
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but don't re-throw - we want to acknowledge the webhook
                        _logger.LogWarning(ex, "Error processing webhook event: {Message}", ex.Message);
                    }
                }
                else
                {
                    _logger.LogInformation("Acknowledged Stripe webhook ping (no processing)");
                }
                
                // Always acknowledge receipt of the webhook
                return Ok(new { received = true });
            }
            catch (Exception ex)
            {
                // Log the exception but still return 200 OK to prevent Stripe from retrying
                _logger.LogError(ex, "Error handling Stripe webhook: {Message}", ex.Message);
                return Ok(new { received = true });
            }
        }

        [HttpGet("verify-payment/{paymentIntentId}")]
        [AllowAnonymous]
        public async Task<ActionResult<PaymentStatusResponse>> VerifyPayment(string paymentIntentId)
        {
            try
            {
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(paymentIntentId);

                return Ok(new PaymentStatusResponse
                {
                    Status = paymentIntent.Status,
                    IsSuccessful = paymentIntent.Status == "succeeded",
                    ReceiptEmail = paymentIntent.ReceiptEmail,
                    Amount = paymentIntent.Amount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying payment intent {PaymentIntentId}: {Message}", paymentIntentId, ex.Message);
                return StatusCode(500, $"Error verifying payment: {ex.Message}");
            }
        }
    }
}
