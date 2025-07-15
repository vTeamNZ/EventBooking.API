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
using EventBooking.API.Services; // Add this line

namespace EventBooking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger;
        private readonly AppDbContext _context;
        private readonly IEventStatusService _eventStatusService;
        private readonly IBookingConfirmationService _bookingConfirmationService;

        public PaymentController(
            IConfiguration configuration,
            ILogger<PaymentController> logger,
            AppDbContext context,
            IEventStatusService eventStatusService,
            IBookingConfirmationService bookingConfirmationService)
        {
            _configuration = configuration;
            _logger = logger;
            _context = context;
            _eventStatusService = eventStatusService;
            _bookingConfirmationService = bookingConfirmationService;
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
                            
                            // Process the booking confirmation
                            if (session != null)
                            {
                                await _bookingConfirmationService.ProcessPaymentSuccessAsync(session.Id, session.PaymentIntentId);
                            }
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

        private async Task<bool> ValidateSelectedSeats(int eventId, List<string> selectedSeats)
        {
            if (selectedSeats == null || !selectedSeats.Any())
            {
                return false;
            }

            foreach (var seatNumber in selectedSeats)
            {
                var seat = await _context.Seats
                    .FirstOrDefaultAsync(s => s.EventId == eventId && s.SeatNumber == seatNumber);

                if (seat == null || (seat.Status != SeatStatus.Available && seat.Status != SeatStatus.Reserved))
                {
                    return false;
                }
            }

            return true;
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

                // Check if event is still active before creating checkout session
                var eventItem = await _context.Events.FindAsync(request.EventId);
                if (eventItem == null)
                {
                    return BadRequest("Event not found");
                }

                if (_eventStatusService.IsEventExpired(eventItem.Date))
                {
                    return BadRequest("This event has ended and is no longer available for booking");
                }

                // Validate selected seats
                if (request.SelectedSeats != null && request.SelectedSeats.Any())
                {
                    var seatsValid = await ValidateSelectedSeats(request.EventId, request.SelectedSeats);
                    if (!seatsValid)
                    {
                        return BadRequest("One or more selected seats are not available");
                    }
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
                    // Remove PaymentMethodTypes to let Stripe automatically select based on Dashboard settings
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
                        { "customerMobile", request.Mobile ?? "" },
                        { "selectedSeats", request.SelectedSeats != null && request.SelectedSeats.Any() ? 
                            string.Join(";", request.SelectedSeats) : 
                            "" }
                    }
                };

                // Log the seats being stored
                var seatsToStore = request.SelectedSeats != null && request.SelectedSeats.Any() ? 
                    string.Join(";", request.SelectedSeats) : "";
                _logger.LogInformation("Creating checkout session for event {EventId} with seats: '{Seats}'", 
                    request.EventId, seatsToStore);

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

                // Process booking confirmation for paid sessions
                if (session.PaymentStatus == "paid")
                {
                    // This ensures booking is processed even if webhook fails
                    await _bookingConfirmationService.ProcessPaymentSuccessAsync(sessionId, paymentIntentId);
                }

                // Get customer name from metadata
                session.Metadata.TryGetValue("customerFirstName", out var firstName);
                session.Metadata.TryGetValue("customerLastName", out var lastName);
                string customerName = $"{firstName} {lastName}".Trim();

                // Get booked seats for this session - ALWAYS get from metadata regardless of payment status
                List<string> bookedSeats = new List<string>();
                
                // First try to get selected seats directly from metadata
                session.Metadata.TryGetValue("selectedSeats", out var selectedSeatsString);
                _logger.LogInformation("Session {SessionId} - Selected seats string from metadata: '{SelectedSeats}'", sessionId, selectedSeatsString);
                
                if (!string.IsNullOrEmpty(selectedSeatsString))
                {
                    try
                    {
                        // Split by semicolon since seats were stored using string.Join(";", request.SelectedSeats)
                        var selectedSeats = selectedSeatsString.Split(';', StringSplitOptions.RemoveEmptyEntries);
                        if (selectedSeats != null && selectedSeats.Length > 0)
                        {
                            _logger.LogInformation("Session {SessionId} - Successfully parsed {Count} seats from metadata: {Seats}", 
                                sessionId, selectedSeats.Length, string.Join(", ", selectedSeats));
                            bookedSeats = selectedSeats.ToList();
                        }
                        else
                        {
                            _logger.LogWarning("Session {SessionId} - No seats found in parsed string", sessionId);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Session {SessionId} - Failed to parse seats string: '{SeatsString}'", sessionId, selectedSeatsString);
                    }
                }
                
                // If no seats found in selectedSeats, try to extract from ticketDetails
                if (bookedSeats.Count == 0)
                {
                    _logger.LogInformation("Session {SessionId} - No seats found in selectedSeats, trying ticketDetails", sessionId);
                    session.Metadata.TryGetValue("ticketDetails", out var ticketDetailsString);
                    
                    if (!string.IsNullOrEmpty(ticketDetailsString))
                    {
                        try
                        {
                            var ticketDetails = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(ticketDetailsString);
                            if (ticketDetails != null)
                            {
                                foreach (var ticket in ticketDetails)
                                {
                                    if (ticket.TryGetValue("Type", out var typeObj) && typeObj != null)
                                    {
                                        var type = typeObj.ToString();
                                        _logger.LogInformation("Session {SessionId} - Processing ticket type: {Type}", sessionId, type);
                                        
                                        // Check if this is a seat ticket (multiple possible formats)
                                        if (type?.Contains("Seat") == true)
                                        {
                                            // Handle format: "Seat (K1)" - single seat
                                            if (type.StartsWith("Seat (") && type.EndsWith(")"))
                                            {
                                                var seatNumber = type.Substring(6, type.Length - 7); // Remove "Seat (" and ")"
                                                bookedSeats.Add(seatNumber);
                                                _logger.LogInformation("Session {SessionId} - Extracted single seat {SeatNumber} from ticketDetails", sessionId, seatNumber);
                                            }
                                            // Handle format: "Seats(M10,M13)" - multiple seats
                                            else if (type.StartsWith("Seats(") && type.EndsWith(")"))
                                            {
                                                var seatsString = type.Substring(6, type.Length - 7); // Remove "Seats(" and ")"
                                                var seatNumbers = seatsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                    .Select(s => s.Trim())
                                                    .ToList();
                                                bookedSeats.AddRange(seatNumbers);
                                                _logger.LogInformation("Session {SessionId} - Extracted multiple seats {SeatNumbers} from ticketDetails", sessionId, string.Join(", ", seatNumbers));
                                            }
                                            // Handle format: "Seats (M10, M13)" - multiple seats with space
                                            else if (type.StartsWith("Seats (") && type.EndsWith(")"))
                                            {
                                                var seatsString = type.Substring(7, type.Length - 8); // Remove "Seats (" and ")"
                                                var seatNumbers = seatsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                                    .Select(s => s.Trim())
                                                    .ToList();
                                                bookedSeats.AddRange(seatNumbers);
                                                _logger.LogInformation("Session {SessionId} - Extracted multiple seats with space {SeatNumbers} from ticketDetails", sessionId, string.Join(", ", seatNumbers));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Session {SessionId} - Failed to parse ticketDetails: '{TicketDetails}'", sessionId, ticketDetailsString);
                        }
                    }
                }
                
                // Log final result
                _logger.LogInformation("Session {SessionId} - Final bookedSeats count: {Count}, seats: [{Seats}]", 
                    sessionId, bookedSeats.Count, string.Join(", ", bookedSeats));

                return Ok(new CheckoutSessionStatusResponse
                {
                    Status = session.Status,
                    PaymentStatus = session.PaymentStatus,
                    IsSuccessful = session.PaymentStatus == "paid",
                    CustomerEmail = session.CustomerEmail,
                    AmountTotal = session.AmountTotal,
                    PaymentId = displayPaymentId,  // Add the payment ID for display
                    EventTitle = eventTitle,       // Add the event title
                    BookedSeats = bookedSeats,
                    CustomerName = customerName,
                    TicketReference = displayPaymentId?.Substring(0, Math.Min(displayPaymentId?.Length ?? 0, 8)) ?? string.Empty
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying session {SessionId}: {Message}", sessionId, ex.Message);
                return StatusCode(500, $"Error verifying session: {ex.Message}");
            }
        }

        // Add this endpoint to check event status from frontend
        [HttpGet("check-event-status/{eventId}")]
        [AllowAnonymous]
        public async Task<ActionResult> CheckEventStatus(int eventId)
        {
            try
            {
                var eventItem = await _context.Events.FindAsync(eventId);
                if (eventItem == null)
                {
                    return NotFound("Event not found");
                }

                return Ok(new 
                {
                    EventId = eventId,
                    IsActive = _eventStatusService.IsEventActive(eventItem.Date),
                    IsExpired = _eventStatusService.IsEventExpired(eventItem.Date),
                    EventDate = eventItem.Date,
                    CurrentNZTime = _eventStatusService.GetCurrentNZTime()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking event status for event {EventId}: {Message}", eventId, ex.Message);
                return StatusCode(500, $"Error checking event status: {ex.Message}");
            }
        }

        [HttpGet("debug-session/{sessionId}")]
        [AllowAnonymous]
        public async Task<ActionResult> DebugSession(string sessionId)
        {
            try
            {
                var service = new SessionService();
                var session = await service.GetAsync(sessionId);

                var debugInfo = new
                {
                    SessionId = sessionId,
                    Status = session.Status,
                    PaymentStatus = session.PaymentStatus,
                    CustomerEmail = session.CustomerEmail,
                    AmountTotal = session.AmountTotal,
                    Metadata = session.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>(),
                    SelectedSeatsFromMetadata = session.Metadata?.GetValueOrDefault("selectedSeats", "NOT_FOUND"),
                    EventIdFromMetadata = session.Metadata?.GetValueOrDefault("eventId", "NOT_FOUND")
                };

                _logger.LogInformation("Debug session {SessionId}: {@DebugInfo}", sessionId, debugInfo);
                
                return Ok(debugInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error debugging session {SessionId}: {Message}", sessionId, ex.Message);
                return StatusCode(500, $"Error debugging session: {ex.Message}");
            }
        }
    }
}
