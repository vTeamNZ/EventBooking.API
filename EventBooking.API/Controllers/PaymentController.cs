using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
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
                var webhookSecret = _configuration["Stripe:WebhookSecret"];

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

                        if (stripeEvent.Type == "checkout.session.completed")
                        {
                            var session = stripeEvent.Data.Object as Session;
                            _logger.LogInformation("Checkout session completed: {SessionId}", session?.Id);
                            
                            // TODO: Update booking status to confirmed
                            // TODO: Send confirmation email
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing webhook event: {Message}", ex.Message);
                    }
                }
                else
                {
                    _logger.LogInformation("Acknowledged Stripe webhook ping (no processing)");
                }
                
                return Ok(new { received = true });
            }
            catch (Exception ex)
            {
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

        [HttpPost("create-checkout-session")]
        [AllowAnonymous]
        public async Task<ActionResult<CreateCheckoutSessionResponse>> CreateCheckoutSession(
            [FromBody] CreateCheckoutSessionRequest request)
        {
            try
            {
                _logger.LogInformation("Received checkout session request: {@Request}", request);

                if (request == null)
                {
                    return BadRequest("Request cannot be null");
                }

                var lineItems = new List<SessionLineItemOptions>();

                // Add ticket line items
                if (request.TicketDetails != null && request.TicketDetails.Any())
                {
                    foreach (var ticket in request.TicketDetails)
                    {
                        lineItems.Add(new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                UnitAmount = (long)(ticket.UnitPrice * 100), // Convert to cents
                                Currency = "nzd",
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = $"{ticket.Type} Ticket - {request.EventTitle}",
                                    Description = $"{ticket.Type} ticket for {request.EventTitle}",
                                }
                            },
                            Quantity = ticket.Quantity,
                        });
                    }
                }

                // Add food line items
                if (request.FoodDetails != null && request.FoodDetails.Any())
                {
                    foreach (var food in request.FoodDetails)
                    {
                        lineItems.Add(new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                UnitAmount = (long)(food.UnitPrice * 100), // Convert to cents
                                Currency = "nzd",
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = food.Name,
                                    Description = $"Food item for {request.EventTitle}",
                                }
                            },
                            Quantity = food.Quantity,
                        });
                    }
                }

                var options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card", "afterpay_clearpay"},
                    LineItems = lineItems,
                    Mode = "payment",
                    SuccessUrl = $"{request.SuccessUrl}?session_id={{CHECKOUT_SESSION_ID}}",
                    CancelUrl = request.CancelUrl,
                    CustomerEmail = request.Email,
                    PaymentIntentData = new SessionPaymentIntentDataOptions
                    {
                        Description = $"Tickets for {request.EventTitle} - {request.FirstName}"
                    },
                    Metadata = new Dictionary<string, string>
                    {
                        { "eventId", request.EventId.ToString() },
                        { "eventTitle", request.EventTitle },
                        { "ticketDetails", System.Text.Json.JsonSerializer.Serialize(request.TicketDetails) },
                        { "foodDetails", request.FoodDetails != null ? System.Text.Json.JsonSerializer.Serialize(request.FoodDetails) : "" },
                        { "customerFirstName", request.FirstName ?? "" },
                        { "customerLastName", request.LastName ?? "" },
                        { "customerMobile", request.Mobile ?? "" }
                    }
                };

                var service = new SessionService();
                var session = await service.CreateAsync(options);

                _logger.LogInformation("Created checkout session: {SessionId} for event: {EventId}", 
                    session.Id, request.EventId);

                return Ok(new CreateCheckoutSessionResponse 
                { 
                    SessionId = session.Id,
                    Url = session.Url
                });
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error creating checkout session: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Inner exception: {ex.InnerException.Message}";
                }
                _logger.LogError(ex, errorMessage);
                return StatusCode(500, errorMessage);
            }
        }

        [HttpGet("verify-session/{sessionId}")]
        [AllowAnonymous]
        public async Task<ActionResult<CheckoutSessionStatusResponse>> VerifySession(string sessionId)
        {
            try
            {
                var service = new SessionService();
                var session = await service.GetAsync(sessionId);

                // Get the payment intent ID associated with this session
                string paymentIntentId = session.PaymentIntentId;
                
                // Get formatted payment ID (shorter version for display)
                string displayPaymentId = paymentIntentId?.Replace("pi_", "");

                // Extract event title from metadata
                session.Metadata.TryGetValue("eventTitle", out var eventTitle);

                return Ok(new CheckoutSessionStatusResponse
                {
                    Status = session.Status,
                    PaymentStatus = session.PaymentStatus,
                    IsSuccessful = session.PaymentStatus == "paid",
                    CustomerEmail = session.CustomerEmail,
                    AmountTotal = session.AmountTotal,
                    PaymentId = displayPaymentId,  // Add the payment ID for display
                    EventTitle = eventTitle        // Add the event title
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying session {SessionId}: {Message}", sessionId, ex.Message);
                return StatusCode(500, $"Error verifying session: {ex.Message}");
            }
        }
    }
}
