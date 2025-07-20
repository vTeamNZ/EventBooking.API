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
using EventBooking.API.Services;

namespace EventBooking.API.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger;
        private readonly AppDbContext _context;
        private readonly IEventStatusService _eventStatusService;
        private readonly IBookingConfirmationService _bookingConfirmationService;
        private readonly IProcessingFeeService _processingFeeService;
        private readonly ITicketAvailabilityService _ticketAvailabilityService;
        
        public PaymentController(
            IConfiguration configuration,
            ILogger<PaymentController> logger,
            AppDbContext context,
            IEventStatusService eventStatusService,
            IBookingConfirmationService bookingConfirmationService,
            IProcessingFeeService processingFeeService,
            ITicketAvailabilityService ticketAvailabilityService)
        {
            _configuration = configuration;
            _logger = logger;
            _context = context;
            _eventStatusService = eventStatusService;
            _bookingConfirmationService = bookingConfirmationService;
            _processingFeeService = processingFeeService;
            _ticketAvailabilityService = ticketAvailabilityService;
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
                            webhookSecret,
                            throwOnApiVersionMismatch: false
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
        public async Task<ActionResult<Models.Payment.PaymentStatusResponse>> VerifyPayment(string paymentIntentId)
        {
            try
            {
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(paymentIntentId);

                return Ok(new Models.Payment.PaymentStatusResponse
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

        private async Task<bool> ValidateSelectedSeats(int eventId, List<string> selectedSeats, string? userSessionId = null)
        {
            if (selectedSeats == null || !selectedSeats.Any())
            {
                return false;
            }

            foreach (var seatNumber in selectedSeats)
            {
                var seat = await _context.Seats
                    .FirstOrDefaultAsync(s => s.EventId == eventId && s.SeatNumber == seatNumber);

                if (seat == null)
                {
                    _logger.LogWarning("Seat validation failed: Seat {SeatNumber} not found for event {EventId}", seatNumber, eventId);
                    return false;
                }

                // Seat must be available OR reserved by the current user's session
                if (seat.Status == SeatStatus.Available)
                {
                    continue; // Available seats are fine
                }
                
                if (seat.Status == SeatStatus.Reserved)
                {
                    // For reserved seats, check if it's reserved by the current user's session
                    if (string.IsNullOrEmpty(userSessionId) || seat.ReservedBy != userSessionId)
                    {
                        _logger.LogWarning("Seat validation failed: Seat {SeatNumber} is reserved by another session. Current session: {UserSessionId}, Seat reserved by: {ReservedBy}", 
                            seatNumber, userSessionId, seat.ReservedBy);
                        return false;
                    }
                    
                    // Check if reservation hasn't expired
                    if (seat.ReservedUntil.HasValue && seat.ReservedUntil.Value < DateTime.UtcNow)
                    {
                        _logger.LogWarning("Seat validation failed: Seat {SeatNumber} reservation has expired at {ReservedUntil}", 
                            seatNumber, seat.ReservedUntil.Value);
                        return false;
                    }
                }
                else
                {
                    // Seat is in some other status (booked, unavailable, etc.)
                    _logger.LogWarning("Seat validation failed: Seat {SeatNumber} has invalid status {Status}", seatNumber, seat.Status);
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

                // Only validate seats for EventHall events (allocated seating)
                if (eventItem.SeatSelectionMode == SeatSelectionMode.EventHall)
                {
                    // For allocated seating events, seats must be provided and valid
                    if (request.SelectedSeats == null || !request.SelectedSeats.Any())
                    {
                        return BadRequest("Selected seats are required for this event");
                    }
                    
                    // Validate seats with session ownership check
                    var seatsValid = await ValidateSelectedSeats(request.EventId, request.SelectedSeats, request.UserSessionId);
                    if (!seatsValid)
                    {
                        return BadRequest("One or more selected seats are not available or not reserved by your session");
                    }
                }
                else if (eventItem.SeatSelectionMode == SeatSelectionMode.GeneralAdmission)
                {
                    // For general admission events, seats should not be provided
                    if (request.SelectedSeats != null && request.SelectedSeats.Any())
                    {
                        return BadRequest("This is a general admission event - specific seats cannot be selected");
                    }
                }

                // Validate ticket availability for General Admission events
                if (eventItem.SeatSelectionMode == SeatSelectionMode.GeneralAdmission && 
                    request.TicketDetails != null && request.TicketDetails.Any())
                {
                    foreach (var ticket in request.TicketDetails)
                    {
                        // Find the ticket type to get its ID
                        var ticketType = await _context.TicketTypes
                            .FirstOrDefaultAsync(tt => tt.EventId == request.EventId && tt.Type == ticket.Type);
                        
                        if (ticketType != null)
                        {
                            var isAvailable = await _ticketAvailabilityService.IsTicketTypeAvailableAsync(
                                ticketType.Id, ticket.Quantity);
                            
                            if (!isAvailable)
                            {
                                var available = await _ticketAvailabilityService.GetTicketsAvailableAsync(ticketType.Id);
                                return BadRequest($"Insufficient tickets available for {ticket.Type}. " +
                                                $"Requested: {ticket.Quantity}, Available: {available}");
                            }
                        }
                    }
                }

                var lineItems = new List<SessionLineItemOptions>();
                decimal subtotal = 0;

                // Add ticket line items
                if (request.TicketDetails != null && request.TicketDetails.Any())
                {
                    foreach (var ticket in request.TicketDetails)
                    {
                        var ticketTotal = ticket.UnitPrice * ticket.Quantity;
                        subtotal += ticketTotal;
                        
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
                        var foodTotal = food.UnitPrice * food.Quantity;
                        subtotal += foodTotal;
                        
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

                // Calculate and add processing fee if enabled
                var processingFeeCalculation = _processingFeeService.CalculateTotalWithProcessingFee(subtotal, eventItem);
                
                if (processingFeeCalculation.ProcessingFeeApplied && processingFeeCalculation.ProcessingFeeAmount > 0)
                {
                    lineItems.Add(new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(processingFeeCalculation.ProcessingFeeAmount * 100), // Convert to cents
                            Currency = "nzd",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "Processing Fee",
                                Description = $"Processing fee for {request.EventTitle}",
                            }
                        },
                        Quantity = 1,
                    });
                    
                    _logger.LogInformation("Added processing fee of ${ProcessingFee} to checkout session", 
                        processingFeeCalculation.ProcessingFeeAmount);
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
                string paymentIntentId = session.PaymentIntentId ?? "";
                string displayPaymentId = paymentIntentId.Replace("pi_", "");

                // Extract metadata
                session.Metadata.TryGetValue("eventTitle", out var eventTitle);
                session.Metadata.TryGetValue("customerFirstName", out var firstName);
                session.Metadata.TryGetValue("customerLastName", out var lastName);
                string customerName = $"{firstName} {lastName}".Trim();

                // Get booked seats from metadata
                List<string> bookedSeats = new List<string>();
                session.Metadata.TryGetValue("selectedSeats", out var selectedSeatsString);
                
                if (!string.IsNullOrEmpty(selectedSeatsString))
                {
                    try
                    {
                        var selectedSeats = selectedSeatsString.Split(';', StringSplitOptions.RemoveEmptyEntries);
                        bookedSeats = selectedSeats.ToList();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to parse seats string: '{SeatsString}'", selectedSeatsString);
                    }
                }

                return Ok(new CheckoutSessionStatusResponse
                {
                    Status = session.Status,
                    PaymentStatus = session.PaymentStatus,
                    IsSuccessful = session.PaymentStatus == "paid",
                    CustomerEmail = session.CustomerEmail ?? "",
                    AmountTotal = (session.AmountTotal ?? 0) / 100,  // Convert from cents to dollars
                    PaymentId = displayPaymentId,
                    EventTitle = eventTitle,
                    BookedSeats = bookedSeats,
                    CustomerName = customerName,
                    TicketReference = displayPaymentId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying session: {SessionId}", sessionId);
                return StatusCode(500, "Error verifying session");
            }
        }

        [HttpGet("payment-status/{sessionId}")]
        [AllowAnonymous]
        public async Task<ActionResult<Services.WebhookPaymentStatusResponse>> GetPaymentStatus(string sessionId)
        {
            try
            {
                // OPTIMIZATION: Check database first (much faster than Stripe API call)
                var existingBooking = await _context.Bookings
                    .Include(b => b.Event)
                    .FirstOrDefaultAsync(b => b.PaymentIntentId.Contains(sessionId) || 
                                            b.Metadata.Contains(sessionId));

                if (existingBooking != null)
                {
                    // Payment already processed - return cached result
                    return Ok(new Services.WebhookPaymentStatusResponse
                    {
                        IsProcessed = true,
                        ProcessedAt = existingBooking.CreatedAt,
                        BookingDetails = new Services.BookingDetailsResponse
                        {
                            EventTitle = existingBooking.Event?.Title ?? "Unknown Event",
                            CustomerName = $"{existingBooking.CustomerFirstName} {existingBooking.CustomerLastName}".Trim(),
                            CustomerEmail = existingBooking.CustomerEmail,
                            PaymentId = existingBooking.PaymentIntentId.Replace("pi_", ""),
                            AmountTotal = existingBooking.TotalAmount,
                            ProcessedAt = existingBooking.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                            TicketReference = existingBooking.PaymentIntentId.Replace("pi_", "")
                        }
                    });
                }

                // If not in database, check Stripe (but only if necessary)
                var service = new SessionService();
                var session = await service.GetAsync(sessionId);

                // Check if payment has been processed by looking for existing payment record
                var paymentIntentId = session.PaymentIntentId ?? "";
                var isProcessed = false;
                Services.BookingDetailsResponse? bookingDetails = null;

                if (session.PaymentStatus == "paid")
                {
                    // Extract customer info from metadata
                    session.Metadata.TryGetValue("eventTitle", out var eventTitle);
                    session.Metadata.TryGetValue("customerFirstName", out var firstName);
                    session.Metadata.TryGetValue("customerLastName", out var lastName);
                    string customerName = $"{firstName} {lastName}".Trim();

                    // Check if webhook is still processing
                    isProcessed = false;
                    
                    // Return minimal details for UI to show payment succeeded but booking still processing
                    bookingDetails = new Services.BookingDetailsResponse
                    {
                        EventTitle = eventTitle ?? "Unknown Event",
                        CustomerName = customerName,
                        CustomerEmail = session.CustomerEmail ?? "",
                        PaymentId = paymentIntentId.Replace("pi_", ""),
                        AmountTotal = (session.AmountTotal ?? 0) / 100m,
                        ProcessedAt = "Processing...",
                        TicketReference = paymentIntentId.Replace("pi_", "")
                    };
                }

                return Ok(new Services.WebhookPaymentStatusResponse
                {
                    IsProcessed = isProcessed,
                    ProcessedAt = isProcessed ? DateTime.UtcNow : DateTime.MinValue,
                    BookingDetails = bookingDetails
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking payment status for session {SessionId}: {Message}", sessionId, ex.Message);
                return StatusCode(500, new Services.WebhookPaymentStatusResponse
                {
                    IsProcessed = false,
                    ProcessedAt = DateTime.MinValue,
                    ErrorMessage = "Error checking payment status"
                });
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

        // REMOVED: Debug session endpoint - Security risk in production
        // [HttpGet("debug-session/{sessionId}")]
        // This endpoint exposed sensitive payment session data and should not be available in production

        // GET: Payment/processing-fee/{eventId}?amount={amount}
        [HttpGet("processing-fee/{eventId}")]
        [AllowAnonymous]
        public async Task<ActionResult<ProcessingFeeCalculationResponse>> GetProcessingFeeCalculation(
            int eventId, 
            [FromQuery] decimal amount)
        {
            try
            {
                if (amount <= 0)
                {
                    return BadRequest("Amount must be greater than 0");
                }

                // Get the event from the database
                var eventData = await _context.Events.FindAsync(eventId);
                if (eventData == null)
                {
                    return NotFound("Event not found");
                }

                var calculation = _processingFeeService.CalculateTotalWithProcessingFee(amount, eventData);
                
                return Ok(new ProcessingFeeCalculationResponse
                {
                    OriginalAmount = calculation.OrderAmount,
                    ProcessingFeeAmount = calculation.ProcessingFeeAmount,
                    TotalAmount = calculation.TotalAmount,
                    ProcessingFeePercentage = eventData.ProcessingFeePercentage,
                    ProcessingFeeFixedAmount = eventData.ProcessingFeeFixedAmount,
                    IsProcessingFeeEnabled = calculation.ProcessingFeeApplied,
                    Description = calculation.ProcessingFeeDescription
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating processing fee for event {EventId} with amount {Amount}", eventId, amount);
                return StatusCode(500, "Error calculating processing fee");
            }
        }
    }

    // Response model for processing fee calculation
    public class ProcessingFeeCalculationResponse
    {
        public decimal OriginalAmount { get; set; }
        public decimal ProcessingFeeAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal ProcessingFeePercentage { get; set; }
        public decimal ProcessingFeeFixedAmount { get; set; }
        public bool IsProcessingFeeEnabled { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
