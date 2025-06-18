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
using System.IO;

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
                _logger.LogInformation("Received payment intent request: {@Request}", request);                if (request == null)
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
        }        [HttpGet("complete")]
        [AllowAnonymous] // Allow unauthenticated access since users will be redirected here
        public async Task<IActionResult> PaymentComplete([FromQuery] string payment_intent)
        {
            try
            {
                _logger.LogInformation("Payment completion check for payment_intent: {PaymentIntentId}", payment_intent);
                
                // Verify the payment with Stripe
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(payment_intent);
                
                _logger.LogInformation("Payment status: {Status} for intent: {PaymentIntentId}", 
                    paymentIntent.Status, paymentIntent.Id);
                    
                if (paymentIntent.Status == "succeeded")
                {
                    // Extract event and ticket details from metadata
                    paymentIntent.Metadata.TryGetValue("eventId", out string eventIdStr);
                    paymentIntent.Metadata.TryGetValue("eventTitle", out string eventTitle);
                    paymentIntent.Metadata.TryGetValue("ticketDetails", out string ticketDetails);
                    paymentIntent.Metadata.TryGetValue("customerEmail", out string customerEmail);
                    
                    // Log successful payment details
                    _logger.LogInformation("Successful payment for event: {EventTitle}, Customer: {Email}",
                        eventTitle, customerEmail);
                    
                    // Here you would typically:
                    // 1. Update related booking/reservation status
                    // 2. Send confirmation email
                    // For now, we'll just log the details
                    
                    // Redirect to success page with event ID
                    if (int.TryParse(eventIdStr, out int eventId))
                    {
                        return Redirect($"{_configuration["Frontend:Url"]}/booking-success?eventId={eventId}");
                    }
                    else
                    {
                        // Fallback if event ID isn't available
                        return Redirect($"{_configuration["Frontend:Url"]}/booking-success");
                    }
                }
                else if (paymentIntent.Status == "canceled" || paymentIntent.Status == "payment_failed")
                {
                    // Handle failed payment
                    _logger.LogWarning("Payment failed or canceled: {PaymentIntentId}, Status: {Status}", 
                        paymentIntent.Id, paymentIntent.Status);
                    
                    return Redirect($"{_configuration["Frontend:Url"]}/payment-failed");
                }
                else
                {
                    // Payment is still processing or in another state
                    _logger.LogInformation("Payment in progress: {PaymentIntentId}, Status: {Status}", 
                        paymentIntent.Id, paymentIntent.Status);
                    
                    return Redirect($"{_configuration["Frontend:Url"]}/payment-processing");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment completion for intent: {PaymentIntentId}", payment_intent);
                return Redirect($"{_configuration["Frontend:Url"]}/payment-error");
            }
        }        [HttpGet("verify-payment/{paymentIntentId}")]
        [AllowAnonymous]
        public async Task<ActionResult<PaymentStatusResponse>> VerifyPayment(string paymentIntentId)
        {
            try
            {
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(paymentIntentId);
                
                string bookingReference = null;
                int eventId = 0;
                
                if (paymentIntent.Status == "succeeded" && 
                    paymentIntent.Metadata.TryGetValue("eventId", out var eventIdStr) &&
                    int.TryParse(eventIdStr, out eventId))
                {
                    // Create a booking record
                    bookingReference = await CreateBookingFromPayment(paymentIntent);
                    _logger.LogInformation("Created booking with reference {BookingReference} for payment {PaymentIntentId}", 
                        bookingReference, paymentIntentId);
                }

                return Ok(new PaymentStatusResponse
                {
                    Status = paymentIntent.Status,
                    IsSuccessful = paymentIntent.Status == "succeeded",
                    ReceiptEmail = paymentIntent.ReceiptEmail,
                    Amount = paymentIntent.Amount,
                    BookingReference = bookingReference,
                    EventId = eventId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying payment intent {PaymentIntentId}: {Message}", paymentIntentId, ex.Message);
                return StatusCode(500, $"Error verifying payment: {ex.Message}");
            }
        }
        
        private async Task<string> CreateBookingFromPayment(PaymentIntent paymentIntent)
        {
            try
            {
                // Extract information from payment intent
                if (!paymentIntent.Metadata.TryGetValue("eventId", out var eventIdStr) ||
                    !int.TryParse(eventIdStr, out var eventId))
                {
                    throw new Exception("Missing or invalid eventId in payment metadata");
                }
                
                paymentIntent.Metadata.TryGetValue("customerEmail", out var customerEmail);
                paymentIntent.Metadata.TryGetValue("ticketDetails", out var ticketDetailsJson);
                paymentIntent.Metadata.TryGetValue("foodDetails", out var foodDetailsJson);
                
                // Generate unique booking reference
                var bookingReference = $"BK-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8)}";
                
                // TODO: Create booking record in database
                // This would be specific to your application's data model
                // For now, we'll just return the reference
                
                return bookingReference;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating booking from payment: {Message}", ex.Message);
                throw;
            }
        }
    }
}
