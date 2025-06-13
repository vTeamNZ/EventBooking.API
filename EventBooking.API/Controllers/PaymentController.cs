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
                    Metadata = new Dictionary<string, string>
                    {
                        { "eventId", request.EventId.ToString() },
                        { "eventTitle", request.EventTitle },
                        { "ticketDetails", request.TicketDetails }
                    }
                };

                // Add food details to metadata if present
                if (!string.IsNullOrEmpty(request.FoodDetails))
                {
                    options.Metadata.Add("foodDetails", request.FoodDetails);
                }

                _logger.LogInformation("Creating Stripe payment intent with amount: {Amount} {Currency}", request.Amount, request.Currency.ToUpperInvariant());

                var service = new PaymentIntentService();
                var paymentIntent = await service.CreateAsync(options);

                _logger.LogInformation("Created payment intent: {PaymentIntentId}", paymentIntent.Id);

                return Ok(new CreatePaymentIntentResponse 
                { 
                    ClientSecret = paymentIntent.ClientSecret,
                });
            }            
            catch (Exception ex)
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
            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    _configuration["Stripe:WebhookSecret"]
                );

                if (stripeEvent.Type == "payment_intent.succeeded")
                {
                    var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                    _logger.LogInformation("Payment succeeded: {PaymentIntentId}", paymentIntent?.Id);
                    
                    // TODO: Update booking status to confirmed
                    // TODO: Send confirmation email
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling webhook");
                return BadRequest();
            }
        }
    }
}
