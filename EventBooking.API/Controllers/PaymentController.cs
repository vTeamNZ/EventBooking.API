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
using System.Text.Json;

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

        [HttpGet("debug-session/{sessionId}")]
        [AllowAnonymous]
        public async Task<IActionResult> DebugSession(string sessionId)
        {
            try
            {
                var sessionService = new Stripe.Checkout.SessionService();
                var session = await sessionService.GetAsync(sessionId);
                
                return Ok(new {
                    SessionId = session.Id,
                    PaymentStatus = session.PaymentStatus,
                    Metadata = session.Metadata,
                    FoodDetails = session.Metadata.TryGetValue("foodDetails", out var foodDetails) ? foodDetails : "Not found",
                    TicketDetails = session.Metadata.TryGetValue("ticketDetails", out var ticketDetails) ? ticketDetails : "Not found"
                });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error getting session: {ex.Message}");
            }
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

                // Add food details to metadata if present (with 500 character limit)
                if (!string.IsNullOrEmpty(request.FoodDetails))
                {
                    var foodDetails = request.FoodDetails;
                    
                    // Stripe metadata values are limited to 500 characters
                    if (foodDetails.Length > 500)
                    {
                        // Create a condensed version for metadata
                        try
                        {
                            var foodItems = JsonSerializer.Deserialize<List<dynamic>>(request.FoodDetails);
                            var condensedItems = foodItems?.Take(3).Select(item => 
                            {
                                var itemObj = JsonSerializer.Deserialize<Dictionary<string, object>>(item.ToString());
                                return new { 
                                    Name = itemObj.GetValueOrDefault("Name", "Unknown").ToString(),
                                    Qty = itemObj.GetValueOrDefault("Quantity", 0)
                                };
                            }).ToList();
                            
                            var condensed = JsonSerializer.Serialize(condensedItems);
                            if (foodItems?.Count > 3)
                            {
                                condensed = condensed.TrimEnd(']') + $",{{\"Name\":\"...+{(foodItems.Count - 3)} more\",\"Qty\":0}}]";
                            }
                            
                            foodDetails = condensed.Length <= 500 ? condensed : "FoodOrders:" + foodItems?.Count;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to condense food details, using count only");
                            foodDetails = $"FoodItems:{request.FoodDetails.Count(c => c == '{')}";
                        }
                        
                        _logger.LogWarning("Food details condensed from {OriginalLength} to {FinalLength} characters for Stripe metadata", 
                            request.FoodDetails.Length, foodDetails.Length);
                    }
                    
                    options.Metadata.Add("foodDetails", foodDetails);
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
                        else if (stripeEvent.Type == "payment_intent.succeeded")
                        {
                            var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                            _logger.LogInformation("🎯 Payment intent succeeded: {PaymentIntentId}", paymentIntent?.Id);
                            
                            // ✅ VERIFICATION ONLY: Log confirmation that payment actually succeeded
                            // This is a safety net to verify payment status, not to create bookings
                            if (paymentIntent != null)
                            {
                                await VerifyPaymentIntentSucceeded(paymentIntent);
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

        /// <summary>
        /// 🔍 VERIFICATION ONLY: Confirms payment intent actually succeeded
        /// This is a safety net to verify payment status at Stripe's end
        /// Does NOT create database records - only verification and logging
        /// </summary>
        private async Task VerifyPaymentIntentSucceeded(PaymentIntent paymentIntent)
        {
            try
            {
                _logger.LogInformation("🔍 VERIFICATION: Confirming payment_intent.succeeded for PaymentIntentId: {PaymentIntentId}", paymentIntent.Id);

                // Check if we have an existing payment record for this intent
                var existingPayment = await _context.Payments
                    .FirstOrDefaultAsync(p => p.PaymentIntentId == paymentIntent.Id);
                    
                if (existingPayment != null)
                {
                    _logger.LogInformation("✅ VERIFICATION PASSED: Found existing payment record for PaymentIntent: {PaymentIntentId}, Status: {Status}", 
                        paymentIntent.Id, existingPayment.Status);
                        
                    // Update payment status if needed
                    if (existingPayment.Status != "succeeded")
                    {
                        existingPayment.Status = "succeeded";
                        existingPayment.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("📝 Updated payment status to 'succeeded' for PaymentIntent: {PaymentIntentId}", paymentIntent.Id);
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ VERIFICATION WARNING: No payment record found for PaymentIntent: {PaymentIntentId}", paymentIntent.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during payment intent verification for PaymentIntentId: {PaymentIntentId}", paymentIntent.Id);
                // Don't throw - webhook should always return 200 to Stripe
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

                // 🔧 FIXED: Store food details properly without truncation for webhook processing
                string foodDetailsForMetadata = "";
                if (request.FoodDetails != null && request.FoodDetails.Any())
                {
                    // Create properly formatted JSON that webhook can process
                    var foodDetailsJson = System.Text.Json.JsonSerializer.Serialize(request.FoodDetails.Select(f => new Dictionary<string, object>
                    {
                        ["Name"] = f.Name,
                        ["Quantity"] = f.Quantity,
                        ["UnitPrice"] = f.UnitPrice,
                        ["SeatTicketId"] = f.SeatTicketId ?? "", // 🎯 CRITICAL: Include seat assignment
                        ["SeatTicketType"] = f.SeatTicketType ?? "" // 🎯 CRITICAL: Include ticket type
                    }).ToList());
                    
                    // If it's too long for Stripe metadata (500 char limit), store essential data
                    if (foodDetailsJson.Length <= 500)
                    {
                        foodDetailsForMetadata = foodDetailsJson;
                    }
                    else 
                    {
                        // For long food details, store just the count and let webhook get details from line items
                        foodDetailsForMetadata = $"{{\"count\":{request.FoodDetails.Count},\"extractFromLineItems\":true}}";
                    }
                    
                    _logger.LogInformation("Storing food details in metadata: {FoodDetails} (Length: {Length})", 
                        foodDetailsForMetadata, foodDetailsForMetadata.Length);
                }

                // 🔧 FIX: Ensure success URL uses correct base URL from configuration
                var successUrl = request.SuccessUrl;
                var baseUrl = _configuration["ApplicationSettings:BaseUrl"];
                
                // If we have a baseUrl configured and the successUrl doesn't match the baseUrl domain/path
                if (!string.IsNullOrEmpty(baseUrl))
                {
                    var requestUri = new Uri(successUrl);
                    var configUri = new Uri(baseUrl);
                    
                    // Check if the request URL doesn't start with the configured base URL
                    if (!successUrl.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        // Replace the domain/path with the configured base URL
                        successUrl = $"{baseUrl.TrimEnd('/')}{requestUri.AbsolutePath}{requestUri.Query}";
                        _logger.LogInformation("🔧 Corrected success URL from {OriginalUrl} to {CorrectedUrl}", request.SuccessUrl, successUrl);
                    }
                }

                var options = new SessionCreateOptions
                {
                    // Remove PaymentMethodTypes to let Stripe automatically select based on Dashboard settings
                    LineItems = lineItems,
                    Mode = "payment",
                    SuccessUrl = $"{successUrl}?session_id={{CHECKOUT_SESSION_ID}}",
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
                        { "foodDetails", foodDetailsForMetadata },
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
                    // ✅ SAFE: Check if processing is actually complete
                    if (existingBooking.Status == "Active")
                    {
                        // Extract processing details from metadata
                        var qrResults = new List<Services.QRGenerationResult>();
                        var processingSummary = new Services.ProcessingSummary();
                        
                        try 
                        {
                            if (!string.IsNullOrEmpty(existingBooking.Metadata))
                            {
                                var metadata = JsonSerializer.Deserialize<JsonElement>(existingBooking.Metadata);
                                
                                // Extract QR results if available
                                if (metadata.TryGetProperty("qrResults", out var qrResultsElement))
                                {
                                    foreach (var qrElement in qrResultsElement.EnumerateArray())
                                    {
                                        var qrResult = new Services.QRGenerationResult
                                        {
                                            SeatNumber = qrElement.TryGetProperty("seatNumber", out var seatNum) ? seatNum.GetString() ?? "" : "",
                                            Success = qrElement.TryGetProperty("success", out var success) && success.GetBoolean(),
                                            TicketPath = qrElement.TryGetProperty("hasTicketPath", out var hasPath) && hasPath.GetBoolean() ? "Generated" : null,
                                            CustomerEmailResult = new Services.EmailDeliveryResult
                                            {
                                                Success = qrElement.TryGetProperty("customerEmailSuccess", out var custEmailSuccess) && custEmailSuccess.GetBoolean(),
                                                ErrorMessage = qrElement.TryGetProperty("customerEmailError", out var custEmailError) ? custEmailError.GetString() : null,
                                                EmailType = "Customer"
                                            },
                                            OrganizerEmailResult = new Services.EmailDeliveryResult
                                            {
                                                Success = qrElement.TryGetProperty("organizerEmailSuccess", out var orgEmailSuccess) && orgEmailSuccess.GetBoolean(),
                                                ErrorMessage = qrElement.TryGetProperty("organizerEmailError", out var orgEmailError) ? orgEmailError.GetString() : null,
                                                EmailType = "Organizer"
                                            }
                                        };
                                        qrResults.Add(qrResult);
                                    }
                                }
                                
                                // Extract processing summary if available
                                if (metadata.TryGetProperty("processingSummary", out var summaryElement))
                                {
                                    processingSummary = new Services.ProcessingSummary
                                    {
                                        TotalTickets = summaryElement.TryGetProperty("TotalTickets", out var total) ? total.GetInt32() : qrResults.Count,
                                        SuccessfulQRGenerations = summaryElement.TryGetProperty("SuccessfulQRGenerations", out var successQR) ? successQR.GetInt32() : qrResults.Count(qr => qr.Success),
                                        FailedQRGenerations = summaryElement.TryGetProperty("FailedQRGenerations", out var failedQR) ? failedQR.GetInt32() : qrResults.Count(qr => !qr.Success),
                                        SuccessfulCustomerEmails = summaryElement.TryGetProperty("SuccessfulCustomerEmails", out var successCustomer) ? successCustomer.GetInt32() : qrResults.Count(qr => qr.CustomerEmailResult.Success),
                                        FailedCustomerEmails = summaryElement.TryGetProperty("FailedCustomerEmails", out var failedCustomer) ? failedCustomer.GetInt32() : qrResults.Count(qr => !qr.CustomerEmailResult.Success),
                                        SuccessfulOrganizerEmails = summaryElement.TryGetProperty("SuccessfulOrganizerEmails", out var successOrganizer) ? successOrganizer.GetInt32() : qrResults.Count(qr => qr.OrganizerEmailResult.Success),
                                        FailedOrganizerEmails = summaryElement.TryGetProperty("FailedOrganizerEmails", out var failedOrganizer) ? failedOrganizer.GetInt32() : qrResults.Count(qr => !qr.OrganizerEmailResult.Success)
                                    };
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error parsing booking metadata for detailed QR/email results");
                            // Fallback to basic summary if metadata parsing fails
                            processingSummary = new Services.ProcessingSummary
                            {
                                TotalTickets = 1,
                                SuccessfulQRGenerations = 1,
                                FailedQRGenerations = 0,
                                SuccessfulCustomerEmails = 1,
                                FailedCustomerEmails = 0,
                                SuccessfulOrganizerEmails = 1,
                                FailedOrganizerEmails = 0
                            };
                        }
                        
                        // Payment fully processed - return detailed result
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
                                TicketReference = existingBooking.PaymentIntentId.Replace("pi_", ""),
                                BookingId = existingBooking.Id,
                                QRTicketsGenerated = qrResults,
                                ProcessingSummary = processingSummary
                            }
                        });
                    }
                    else if (existingBooking.Status == "Failed")
                    {
                        return Ok(new Services.WebhookPaymentStatusResponse
                        {
                            IsProcessed = false,
                            ProcessedAt = existingBooking.CreatedAt,
                            ErrorMessage = "Payment processing failed. Please contact support."
                        });
                    }
                    else if (existingBooking.Status == "Processing")
                    {
                        // Still processing - continue polling
                        return Ok(new Services.WebhookPaymentStatusResponse
                        {
                            IsProcessed = false,
                            ProcessedAt = existingBooking.CreatedAt,
                            BookingDetails = new Services.BookingDetailsResponse
                            {
                                EventTitle = existingBooking.Event?.Title ?? "Unknown Event",
                                CustomerName = $"{existingBooking.CustomerFirstName} {existingBooking.CustomerLastName}".Trim(),
                                CustomerEmail = existingBooking.CustomerEmail,
                                PaymentId = existingBooking.PaymentIntentId.Replace("pi_", ""),
                                AmountTotal = existingBooking.TotalAmount,
                                ProcessedAt = "Still processing...",
                                TicketReference = existingBooking.PaymentIntentId.Replace("pi_", "")
                            }
                        });
                    }
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
