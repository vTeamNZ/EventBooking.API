using EventBooking.API.Data;
using EventBooking.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Stripe;
using Stripe.Checkout;

namespace EventBooking.API.Services
{
    public interface IBookingConfirmationService
    {
        Task<BookingConfirmationResult> ProcessPaymentSuccessAsync(string sessionId, string paymentIntentId);
    }

    public class BookingConfirmationService : IBookingConfirmationService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BookingConfirmationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IQRTicketService _qrTicketService;
        private readonly IEmailService _emailService;

        public BookingConfirmationService(
            AppDbContext context,
            ILogger<BookingConfirmationService> logger,
            IConfiguration configuration,
            IQRTicketService qrTicketService,
            IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _qrTicketService = qrTicketService;
            _emailService = emailService;
        }

        public async Task<BookingConfirmationResult> ProcessPaymentSuccessAsync(string sessionId, string paymentIntentId)
        {
            var result = new BookingConfirmationResult();
            
            try
            {
                _logger.LogInformation("🏗️ Processing payment success using NEW BOOKINGLINEITEMS ARCHITECTURE for session: {SessionId}", sessionId);

                // ✅ IDEMPOTENCY CHECK: Check if this payment has already been processed
                var existingBooking = await _context.Bookings
                    .Include(b => b.Event)
                    .FirstOrDefaultAsync(b => b.PaymentIntentId == paymentIntentId);

                if (existingBooking != null)
                {
                    _logger.LogInformation("🔄 IDEMPOTENCY: Payment {PaymentIntentId} already processed as booking {BookingId}, returning existing result", 
                        paymentIntentId, existingBooking.Id);
                    
                    // Return the existing booking result instead of creating a new one
                    return new BookingConfirmationResult
                    {
                        Success = true,
                        EventTitle = existingBooking.Event?.Title ?? "Unknown Event",
                        CustomerName = $"{existingBooking.CustomerFirstName} {existingBooking.CustomerLastName}".Trim(),
                        CustomerEmail = existingBooking.CustomerEmail,
                        BookedSeats = new List<string>(), // Could extract from metadata if needed
                        AmountTotal = existingBooking.TotalAmount,
                        TicketReference = paymentIntentId.Replace("pi_", ""),
                        QRResults = new List<QRGenerationResult>(), // Already generated
                        BookingId = existingBooking.Id
                    };
                }

                // Get session from Stripe to extract metadata
                var sessionService = new Stripe.Checkout.SessionService();
                var session = await sessionService.GetAsync(sessionId);

                if (session.PaymentStatus != "paid")
                {
                    _logger.LogWarning("Payment not successful for session: {SessionId}", sessionId);
                    result.Success = false;
                    result.ErrorMessage = "Payment status is not paid";
                    return result;
                }

                // Extract metadata
                session.Metadata.TryGetValue("eventId", out var eventIdStr);
                session.Metadata.TryGetValue("eventTitle", out var eventTitle);
                session.Metadata.TryGetValue("ticketDetails", out var ticketDetailsJson);
                session.Metadata.TryGetValue("foodDetails", out var foodDetailsJson);
                session.Metadata.TryGetValue("customerFirstName", out var firstName);
                session.Metadata.TryGetValue("customerLastName", out var lastName);
                session.Metadata.TryGetValue("customerMobile", out var mobile);

                if (!int.TryParse(eventIdStr, out var eventId))
                {
                    _logger.LogError("Invalid event ID in session metadata: {EventId}", eventIdStr);
                    result.Success = false;
                    result.ErrorMessage = "Invalid event ID in metadata";
                    return result;
                }

                // Get event details
                var eventEntity = await _context.Events
                    .Include(e => e.Organizer)
                    .FirstOrDefaultAsync(e => e.Id == eventId);

                if (eventEntity == null)
                {
                    _logger.LogError("Event not found: {EventId}", eventId);
                    result.Success = false;
                    result.ErrorMessage = "Event not found";
                    return result;
                }

                // Handle seat processing based on event type
                List<string> selectedSeats = new List<string>();
                
                if (eventEntity.SeatSelectionMode == SeatSelectionMode.EventHall)
                {
                    // For allocated seating events, get selected seats from metadata
                    session.Metadata.TryGetValue("selectedSeats", out var selectedSeatsString);
                    
                    _logger.LogInformation("ALLOCATED SEATING - Selected seats string from metadata: {SelectedSeats}", selectedSeatsString);
                    
                    try
                    {
                        if (!string.IsNullOrEmpty(selectedSeatsString))
                        {
                            // Split by semicolon since seats are stored using string.Join(";", request.SelectedSeats)
                            var seatsArray = selectedSeatsString.Split(';', StringSplitOptions.RemoveEmptyEntries);
                            selectedSeats = seatsArray.ToList();
                        }
                            
                        if (selectedSeats.Any())
                        {
                            _logger.LogInformation("ALLOCATED SEATING - Successfully parsed {Count} selected seats: {Seats}", 
                                selectedSeats.Count, string.Join(", ", selectedSeats));
                        }
                        else
                        {
                            _logger.LogError("ALLOCATED SEATING - No seats found in metadata for allocated seating event");
                            result.Success = false;
                            result.ErrorMessage = "No seats selected for allocated seating event";
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "ALLOCATED SEATING - Error parsing selected seats: {SeatsString}", selectedSeatsString);
                        result.Success = false;
                        result.ErrorMessage = "Error processing seat selection";
                        return result;
                    }
                }
                else if (eventEntity.SeatSelectionMode == SeatSelectionMode.GeneralAdmission)
                {
                    // For general admission events, no specific seats are needed
                    _logger.LogInformation("GENERAL ADMISSION - Processing general admission booking (no specific seats)");
                    selectedSeats = new List<string>(); // Empty list is fine for general admission
                }
                else
                {
                    _logger.LogError("Unknown seat selection mode: {Mode}", eventEntity.SeatSelectionMode);
                    result.Success = false;
                    result.ErrorMessage = "Unknown event seating configuration";
                    return result;
                }

                // Parse ticket details
                var ticketDetails = new List<Dictionary<string, object>>();
                
                _logger.LogInformation("Raw ticket details JSON from metadata: {TicketDetailsJson}", ticketDetailsJson);
                
                try 
                {
                    if (!string.IsNullOrEmpty(ticketDetailsJson))
                    {
                        ticketDetails = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(ticketDetailsJson);
                        _logger.LogInformation("Successfully parsed {Count} ticket details from JSON", ticketDetails?.Count ?? 0);
                        
                        if (ticketDetails != null)
                        {
                            for (int i = 0; i < ticketDetails.Count; i++)
                            {
                                var ticket = ticketDetails[i];
                                _logger.LogInformation("Ticket {Index}: {TicketData}", i, 
                                    string.Join(", ", ticket.Select(kvp => $"{kvp.Key}={kvp.Value}")));
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No ticket details JSON found in metadata");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing ticket details JSON: {TicketDetails}", ticketDetailsJson);
                }

                // Parse food details if present
                var foodDetails = new List<Dictionary<string, object>>();
                try 
                {
                    if (!string.IsNullOrEmpty(foodDetailsJson))
                    {
                        foodDetails = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(foodDetailsJson);
                        _logger.LogInformation("Successfully parsed {Count} food details from JSON", foodDetails?.Count ?? 0);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing food details JSON: {FoodDetails}", foodDetailsJson);
                }

                // 🎯 CREATE BOOKING WITH NEW ARCHITECTURE
                var totalAmount = (decimal)(session.AmountTotal ?? 0) / 100; // Convert from cents
                var processingFee = CalculateProcessingFee(totalAmount);
                
                var booking = new Booking
                {
                    EventId = eventId,
                    CustomerEmail = session.CustomerEmail ?? "",
                    CustomerFirstName = firstName ?? "Guest",
                    CustomerLastName = lastName ?? "",
                    CustomerMobile = mobile ?? "",
                    PaymentIntentId = paymentIntentId,
                    PaymentStatus = "Completed",
                    TotalAmount = totalAmount,
                    ProcessingFee = processingFee,
                    Currency = "NZD",
                    CreatedAt = DateTime.UtcNow,
                    Status = "Processing", // ✅ Start as Processing, will update to Active when complete
                    Metadata = JsonSerializer.Serialize(new 
                    {
                        sessionId = sessionId,
                        paymentMethod = "stripe",
                        eventType = eventEntity.SeatSelectionMode.ToString(),
                        selectedSeats = selectedSeats,
                        source = "stripe_checkout"
                    })
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                _logger.LogInformation("✅ NEW ARCHITECTURE - Created booking with ID: {BookingId}", booking.Id);

                // 🎯 CREATE BOOKING LINE ITEMS - UNIFIED ARCHITECTURE
                var bookingLineItems = new List<BookingLineItem>();

                // Process TICKET line items
                if (ticketDetails != null && ticketDetails.Any())
                {
                    foreach (var ticket in ticketDetails)
                    {
                        // Try both "Type" and "type" for case-insensitive matching
                        var typeObj = ticket.TryGetValue("Type", out var typeCapital) ? typeCapital :
                                    ticket.TryGetValue("type", out var typeLower) ? typeLower : null;

                        if (typeObj != null)
                        {
                            var type = typeObj.ToString();
                            
                            // Find ticket type by matching Type or Name field
                            var ticketType = await _context.TicketTypes
                                .FirstOrDefaultAsync(tt => tt.EventId == eventId && 
                                    (tt.Type == type || tt.Name == type));

                            // Try quantity with both cases
                            var quantityObj = ticket.TryGetValue("Quantity", out var quantityCapital) ? quantityCapital :
                                            ticket.TryGetValue("quantity", out var quantityLower) ? quantityLower : null;

                            if (ticketType != null && quantityObj != null)
                            {
                                if (int.TryParse(quantityObj.ToString(), out int quantity) && quantity > 0)
                                {
                                    _logger.LogInformation("🎫 NEW ARCHITECTURE - Creating BookingLineItem (Ticket): BookingId={BookingId}, TicketTypeId={TicketTypeId}, Quantity={Quantity}", 
                                        booking.Id, ticketType.Id, quantity);

                                    var bookingLineItem = new BookingLineItem
                                    {
                                        BookingId = booking.Id,
                                        ItemType = "Ticket",
                                        ItemId = ticketType.Id,
                                        ItemName = ticketType.Name ?? ticketType.Type,
                                        Quantity = quantity,
                                        UnitPrice = ticketType.Price,
                                        TotalPrice = quantity * ticketType.Price,
                                        SeatDetails = JsonSerializer.Serialize(new 
                                        {
                                            ticketTypeId = ticketType.Id,
                                            type = ticketType.Type,
                                            color = ticketType.Color,
                                            eventSeatMode = eventEntity.SeatSelectionMode.ToString()
                                        }),
                                        ItemDetails = JsonSerializer.Serialize(new 
                                        {
                                            description = ticketType.Description,
                                            originalTicketData = ticket,
                                            maxTickets = ticketType.MaxTickets
                                        }),
                                        QRCode = "", // Will be populated by QR service
                                        Status = "Active",
                                        CreatedAt = DateTime.UtcNow
                                    };

                                    bookingLineItems.Add(bookingLineItem);
                                    _context.BookingLineItems.Add(bookingLineItem);
                                }
                            }
                            else
                            {
                                _logger.LogWarning("Could not find ticket type for event {EventId} with type '{Type}'", eventId, type);
                            }
                        }
                    }
                }

                // Process FOOD line items (if present) - NEW: Individual per-seat/ticket processing
                if (foodDetails != null && foodDetails.Any())
                {
                    foreach (var food in foodDetails)
                    {
                        var nameObj = food.TryGetValue("Name", out var nameCapital) ? nameCapital :
                                    food.TryGetValue("name", out var nameLower) ? nameLower : null;

                        var quantityObj = food.TryGetValue("Quantity", out var quantityCapital) ? quantityCapital :
                                        food.TryGetValue("quantity", out var quantityLower) ? quantityLower : null;

                        var priceObj = food.TryGetValue("Price", out var priceCapital) ? priceCapital :
                                     food.TryGetValue("price", out var priceLower) ? priceLower : null;

                        // NEW: Get seat/ticket association for individual tracking
                        var seatTicketIdObj = food.TryGetValue("seatTicketId", out var seatTicketId) ? seatTicketId : null;
                        var seatTicketTypeObj = food.TryGetValue("seatTicketType", out var seatTicketType) ? seatTicketType : null;

                        if (nameObj != null && quantityObj != null && priceObj != null)
                        {
                            if (int.TryParse(quantityObj.ToString(), out int quantity) && 
                                decimal.TryParse(priceObj.ToString(), out decimal price))
                            {
                                var foodName = nameObj.ToString();
                                
                                // Try to find the food item in the database
                                var foodItem = await _context.FoodItems
                                    .FirstOrDefaultAsync(fi => fi.EventId == eventId && fi.Name == foodName);
                                
                                var foodItemId = foodItem?.Id ?? 0; // Use 0 if not found (legacy compatibility)
                                
                                _logger.LogInformation("🍕 NEW ARCHITECTURE - Creating Individual BookingLineItem (Food): {FoodName}, Quantity={Quantity}, ItemId={ItemId}, AssociatedWith={SeatTicketId}", 
                                    foodName, quantity, foodItemId, seatTicketIdObj?.ToString() ?? "None");

                                var bookingLineItem = new BookingLineItem
                                {
                                    BookingId = booking.Id,
                                    ItemType = "Food",
                                    ItemId = foodItemId, // ✅ Properly linked to FoodItems table
                                    ItemName = foodName,
                                    Quantity = quantity, // Individual quantity per seat/ticket
                                    UnitPrice = price,
                                    TotalPrice = quantity * price,
                                    SeatDetails = JsonSerializer.Serialize(new 
                                    {
                                        associatedSeatTicket = seatTicketIdObj?.ToString(),
                                        seatTicketType = seatTicketTypeObj?.ToString(),
                                        individualSelection = true
                                    }),
                                    ItemDetails = JsonSerializer.Serialize(new 
                                    {
                                        originalFoodData = food,
                                        category = "concession",
                                        requiresPreparation = true,
                                        databaseItemId = foodItemId,
                                        description = foodItem?.Description,
                                        associatedWith = seatTicketIdObj?.ToString(),
                                        selectionType = "individual"
                                    }),
                                    QRCode = "", // Food items typically don't need QR codes
                                    Status = "Active",
                                    CreatedAt = DateTime.UtcNow
                                };

                                bookingLineItems.Add(bookingLineItem);
                                _context.BookingLineItems.Add(bookingLineItem);
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ NEW ARCHITECTURE - Created {Count} BookingLineItems", bookingLineItems.Count);

                // Handle QR generation based on event type
                var qrResults = new List<QRGenerationResult>();
                
                if (eventEntity.SeatSelectionMode == SeatSelectionMode.EventHall && selectedSeats.Any())
                {
                    // ALLOCATED SEATING: Update seat status and generate QR per seat
                    _logger.LogInformation("ALLOCATED SEATING - Processing {Count} specific seats", selectedSeats.Count);
                    
                    // Get all selected seats in one query for efficiency
                    var seats = await _context.Seats
                        .Where(s => s.EventId == eventId && selectedSeats.Contains(s.SeatNumber))
                        .ToListAsync();

                    _logger.LogInformation("Found {SeatsCount} seats matching selected seat numbers", seats.Count);

                    if (seats.Count != selectedSeats.Count)
                    {
                        _logger.LogWarning(
                            "Not all selected seats were found. Selected: {SelectedCount}, Found: {FoundCount}",
                            selectedSeats.Count, seats.Count);
                    }

                    // Update seat statuses to booked
                    foreach (var seat in seats)
                    {
                        if (seat.Status != SeatStatus.Reserved && seat.Status != SeatStatus.Available)
                        {
                            _logger.LogWarning("Seat {SeatNumber} is not available or reserved, status: {Status}",
                                seat.SeatNumber, seat.Status);
                            continue;
                        }

                        // Update seat status to booked
                        seat.Status = SeatStatus.Booked;
                        seat.ReservedBy = session.CustomerEmail;
                        seat.ReservedUntil = DateTime.UtcNow.AddDays(1); // Set expiry for cleanup if needed
                        
                        _logger.LogInformation("Updated seat {SeatNumber} to Booked status", seat.SeatNumber);
                    }
                    
                    // Save all seat updates
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Updated {Count} seats to Booked status", seats.Count);
                    
                    // 🍕 Extract food order data for enhanced display
                    var foodOrders = ExtractFoodOrdersFromLineItems(bookingLineItems);
                    
                    // Generate QR code for each seat using consolidated service
                    foreach (var seatNumber in selectedSeats)
                    {
                        try 
                        {
                            var qrRequest = new QRTicketRequest
                            {
                                EventId = eventId.ToString(),
                                EventName = eventTitle ?? eventEntity.Title,
                                SeatNumber = seatNumber,
                                FirstName = firstName ?? "Guest",
                                PaymentGuid = paymentIntentId,
                                BuyerEmail = session.CustomerEmail ?? "",
                                OrganizerEmail = eventEntity.Organizer?.ContactEmail ?? "",
                                BookingId = booking.Id, // ✅ Pass the booking ID
                                FoodOrders = foodOrders // ✅ Pass food orders for PDF display
                            };

                            var qrResult = await _qrTicketService.GenerateQRTicketAsync(qrRequest);
                            
                            // 🎯 UPDATE THE CORRESPONDING BOOKING LINE ITEM WITH QR CODE
                            var ticketLineItem = bookingLineItems.FirstOrDefault(bli => 
                                bli.ItemType == "Ticket" && bli.SeatDetails.Contains(seatNumber));
                            
                            if (ticketLineItem != null && qrResult.Success)
                            {
                                ticketLineItem.QRCode = qrResult.BookingId ?? paymentIntentId;
                                _logger.LogInformation("🎯 Updated BookingLineItem {Id} with QR code for seat {Seat}", 
                                    ticketLineItem.Id, seatNumber);
                            }
                            
                            qrResults.Add(new QRGenerationResult 
                            {
                                SeatNumber = seatNumber,
                                Success = qrResult.Success,
                                TicketPath = qrResult.TicketPath,
                                BookingId = qrResult.BookingId,
                                ErrorMessage = qrResult.ErrorMessage
                            });

                            // Send confirmation emails if QR generation was successful
                            await SendConfirmationEmailsAsync(
                                qrResults.Last(), // Pass the QR result we just added
                                session.CustomerEmail ?? "",
                                eventTitle ?? eventEntity.Title,
                                firstName ?? "Guest",
                                eventEntity.Organizer?.ContactEmail ?? "",
                                foodOrders
                            );
                        }
                        catch (Exception qrEx)
                        {
                            _logger.LogError(qrEx, "QR generation failed for seat {Seat}", seatNumber);
                            qrResults.Add(new QRGenerationResult 
                            {
                                SeatNumber = seatNumber,
                                Success = false,
                                ErrorMessage = qrEx.Message
                            });
                        }
                    }
                }
                else if (eventEntity.SeatSelectionMode == SeatSelectionMode.GeneralAdmission)
                {
                    // GENERAL ADMISSION: Generate QR tickets based on BookingLineItems quantity
                    _logger.LogInformation("GENERAL ADMISSION - Processing tickets from BookingLineItems");
                    
                    // 🍕 Extract food order data for enhanced display
                    var foodOrders = ExtractFoodOrdersFromLineItems(bookingLineItems);
                    
                    var ticketLineItems = bookingLineItems.Where(bli => bli.ItemType == "Ticket").ToList();
                    
                    foreach (var lineItem in ticketLineItems)
                    {
                        _logger.LogInformation("GENERAL ADMISSION - Generating {Quantity} tickets for {ItemName}", 
                            lineItem.Quantity, lineItem.ItemName);
                        
                        // Generate QR code for each ticket quantity
                        for (int i = 1; i <= lineItem.Quantity; i++)
                        {
                            try 
                            {
                                var ticketIdentifier = $"{lineItem.ItemName}-{i}";
                                var qrRequest = new QRTicketRequest
                                {
                                    EventId = eventId.ToString(),
                                    EventName = eventTitle ?? eventEntity.Title,
                                    SeatNumber = ticketIdentifier, // Use ticket type + number instead of seat
                                    FirstName = firstName ?? "Guest",
                                    PaymentGuid = paymentIntentId,
                                    BuyerEmail = session.CustomerEmail ?? "",
                                    OrganizerEmail = eventEntity.Organizer?.ContactEmail ?? "",
                                    BookingId = booking.Id, // ✅ Pass the booking ID
                                    FoodOrders = foodOrders // ✅ Pass food orders for PDF display
                                };

                                var qrResult = await _qrTicketService.GenerateQRTicketAsync(qrRequest);
                                
                                // 🎯 UPDATE THE BOOKING LINE ITEM WITH QR CODE (first ticket gets the QR)
                                if (i == 1 && qrResult.Success)
                                {
                                    lineItem.QRCode = qrResult.BookingId ?? paymentIntentId;
                                    _logger.LogInformation("🎯 Updated BookingLineItem {Id} with QR code for {ItemName}", 
                                        lineItem.Id, lineItem.ItemName);
                                }
                                
                                qrResults.Add(new QRGenerationResult 
                                {
                                    SeatNumber = ticketIdentifier, // Will contain ticket type info
                                    Success = qrResult.Success,
                                    TicketPath = qrResult.TicketPath,
                                    BookingId = qrResult.BookingId,
                                    ErrorMessage = qrResult.ErrorMessage
                                });

                                // Send confirmation emails if QR generation was successful
                                await SendConfirmationEmailsAsync(
                                    qrResults.Last(), // Pass the QR result we just added
                                    session.CustomerEmail ?? "",
                                    eventTitle ?? eventEntity.Title,
                                    firstName ?? "Guest",
                                    eventEntity.Organizer?.ContactEmail ?? "",
                                    foodOrders
                                );
                            }
                            catch (Exception qrEx)
                            {
                                _logger.LogError(qrEx, "QR generation failed for general admission ticket {Type}-{Number}", lineItem.ItemName, i);
                                qrResults.Add(new QRGenerationResult 
                                {
                                    SeatNumber = $"{lineItem.ItemName}-{i}",
                                    Success = false,
                                    ErrorMessage = qrEx.Message
                                });
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();
                
                // ✅ Build successful result with all data
                result.Success = true;
                result.EventTitle = eventTitle ?? eventEntity.Title;
                result.CustomerName = $"{firstName} {lastName}".Trim();
                result.CustomerEmail = session.CustomerEmail ?? "";
                
                // Set booked seats based on event type
                if (eventEntity.SeatSelectionMode == SeatSelectionMode.EventHall)
                {
                    result.BookedSeats = selectedSeats; // Actual seat numbers for allocated seating
                }
                else if (eventEntity.SeatSelectionMode == SeatSelectionMode.GeneralAdmission)
                {
                    // For general admission, list the ticket identifiers
                    result.BookedSeats = qrResults.Select(qr => qr.SeatNumber).ToList();
                }
                else
                {
                    result.BookedSeats = new List<string>();
                }
                
                result.AmountTotal = (decimal)(session.AmountTotal ?? 0) / 100;
                result.TicketReference = paymentIntentId?.Replace("pi_", "") ?? "";
                result.QRResults = qrResults;
                result.BookingId = booking.Id;
                
                // ✅ Calculate processing summary for user feedback
                result.ProcessingSummary = new ProcessingSummary
                {
                    TotalTickets = qrResults.Count,
                    SuccessfulQRGenerations = qrResults.Count(qr => qr.Success),
                    FailedQRGenerations = qrResults.Count(qr => !qr.Success),
                    SuccessfulCustomerEmails = qrResults.Count(qr => qr.CustomerEmailResult.Success),
                    FailedCustomerEmails = qrResults.Count(qr => !qr.CustomerEmailResult.Success),
                    SuccessfulOrganizerEmails = qrResults.Count(qr => qr.OrganizerEmailResult.Success),
                    FailedOrganizerEmails = qrResults.Count(qr => !qr.OrganizerEmailResult.Success)
                };
                
                // ✅ Mark booking as complete now that all processing is done
                booking.Status = "Active";
                booking.UpdatedAt = DateTime.UtcNow;
                
                // ✅ Store processing summary in metadata for frontend access
                var enhancedMetadata = new 
                {
                    sessionId = sessionId,
                    paymentMethod = "stripe",
                    eventType = eventEntity.SeatSelectionMode.ToString(),
                    selectedSeats = selectedSeats,
                    source = "stripe_checkout",
                    processingSummary = result.ProcessingSummary,
                    qrResults = qrResults.Select(qr => new {
                        seatNumber = qr.SeatNumber,
                        success = qr.Success,
                        hasTicketPath = !string.IsNullOrEmpty(qr.TicketPath),
                        customerEmailSuccess = qr.CustomerEmailResult.Success,
                        organizerEmailSuccess = qr.OrganizerEmailResult.Success,
                        customerEmailError = qr.CustomerEmailResult.ErrorMessage,
                        organizerEmailError = qr.OrganizerEmailResult.ErrorMessage
                    }).ToList(),
                    processedAt = DateTime.UtcNow
                };
                
                booking.Metadata = JsonSerializer.Serialize(enhancedMetadata);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("🎉 NEW ARCHITECTURE SUCCESS - Processed payment for session: {SessionId}, booking ID: {BookingId}, line items: {LineItemCount}, event type: {EventType}, summary: {Summary}", 
                    sessionId, booking.Id, bookingLineItems.Count, eventEntity.SeatSelectionMode, result.ProcessingSummary.GetStatusMessage());
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment success for session: {SessionId}", sessionId);
                
                // ✅ Mark booking as failed if any error occurs
                try 
                {
                    booking.Status = "Failed";
                    booking.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Failed to update booking status to Failed for session: {SessionId}", sessionId);
                }
                
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Calculate processing fee based on configuration
        /// </summary>
        private decimal CalculateProcessingFee(decimal totalAmount)
        {
            try
            {
                // Get processing fee configuration from appsettings
                var processingFeeEnabled = _configuration.GetValue<bool>("ProcessingFee:Enabled", false);
                if (!processingFeeEnabled)
                {
                    return 0;
                }

                var feeType = _configuration.GetValue<string>("ProcessingFee:Type", "fixed");
                
                if (feeType.ToLower() == "percentage")
                {
                    var feePercentage = _configuration.GetValue<decimal>("ProcessingFee:Percentage", 0);
                    var maxFee = _configuration.GetValue<decimal>("ProcessingFee:MaxFee", 999);
                    
                    var calculatedFee = totalAmount * (feePercentage / 100);
                    return Math.Min(calculatedFee, maxFee);
                }
                else
                {
                    // Fixed fee
                    var fixedFee = _configuration.GetValue<decimal>("ProcessingFee:FixedAmount", 0);
                    return fixedFee;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating processing fee for amount {Amount}", totalAmount);
                return 0; // Return 0 if calculation fails
            }
        }

        /// <summary>
        /// Extract food order information from BookingLineItems for display in PDFs and emails
        /// </summary>
        private List<FoodOrderInfo> ExtractFoodOrdersFromLineItems(List<BookingLineItem> bookingLineItems)
        {
            try
            {
                var foodOrders = new List<FoodOrderInfo>();
                
                var foodLineItems = bookingLineItems.Where(bli => bli.ItemType == "Food").ToList();
                
                foreach (var foodItem in foodLineItems)
                {
                    foodOrders.Add(new FoodOrderInfo
                    {
                        Name = foodItem.ItemName,
                        Quantity = foodItem.Quantity,
                        UnitPrice = foodItem.UnitPrice,
                        TotalPrice = foodItem.TotalPrice,
                        Description = foodItem.ItemDetails ?? ""
                    });
                }
                
                _logger.LogInformation("Extracted {Count} food orders from BookingLineItems", foodOrders.Count);
                return foodOrders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting food orders from BookingLineItems");
                return new List<FoodOrderInfo>(); // Return empty list if extraction fails
            }
        }

        /// <summary>
        /// Send confirmation emails for successful QR ticket generation and track results
        /// </summary>
        private async Task SendConfirmationEmailsAsync(
            QRGenerationResult qrResult, 
            string customerEmail, 
            string eventTitle, 
            string firstName, 
            string organizerEmail, 
            List<FoodOrderInfo> foodOrders)
        {
            try
            {
                if (!qrResult.Success || string.IsNullOrEmpty(qrResult.TicketPath))
                {
                    // Set email results as skipped for failed QR generation
                    qrResult.CustomerEmailResult = new EmailDeliveryResult
                    {
                        Success = false,
                        ErrorMessage = "Skipped - QR generation failed",
                        RecipientEmail = customerEmail,
                        EmailType = "Customer"
                    };
                    
                    qrResult.OrganizerEmailResult = new EmailDeliveryResult
                    {
                        Success = false,
                        ErrorMessage = "Skipped - QR generation failed",
                        RecipientEmail = organizerEmail,
                        EmailType = "Organizer"
                    };
                    
                    return;
                }

                // Read the generated PDF for email attachment
                byte[] ticketPdf = System.IO.File.ReadAllBytes(qrResult.TicketPath);

                // ✅ Send customer email and track result
                try
                {
                    bool customerEmailSuccess = await _emailService.SendTicketEmailAsync(
                        customerEmail,
                        eventTitle,
                        firstName,
                        ticketPdf,
                        foodOrders
                    );

                    qrResult.CustomerEmailResult = new EmailDeliveryResult
                    {
                        Success = customerEmailSuccess,
                        SentAt = customerEmailSuccess ? DateTime.UtcNow : null,
                        RecipientEmail = customerEmail,
                        EmailType = "Customer",
                        ErrorMessage = customerEmailSuccess ? null : "Email delivery failed"
                    };

                    _logger.LogInformation("📧 Customer email {Status} for {EventTitle} to {CustomerEmail}", 
                        customerEmailSuccess ? "sent successfully" : "failed", eventTitle, customerEmail);
                }
                catch (Exception emailEx)
                {
                    qrResult.CustomerEmailResult = new EmailDeliveryResult
                    {
                        Success = false,
                        ErrorMessage = emailEx.Message,
                        RecipientEmail = customerEmail,
                        EmailType = "Customer"
                    };
                    
                    _logger.LogError(emailEx, "Failed to send customer email for {EventTitle} to {CustomerEmail}", eventTitle, customerEmail);
                }

                // ✅ Send organizer email and track result
                try
                {
                    bool organizerEmailSuccess = await _emailService.SendOrganizerNotificationAsync(
                        organizerEmail,
                        eventTitle,
                        firstName,
                        customerEmail,
                        ticketPdf,
                        foodOrders
                    );

                    qrResult.OrganizerEmailResult = new EmailDeliveryResult
                    {
                        Success = organizerEmailSuccess,
                        SentAt = organizerEmailSuccess ? DateTime.UtcNow : null,
                        RecipientEmail = organizerEmail,
                        EmailType = "Organizer",
                        ErrorMessage = organizerEmailSuccess ? null : "Email delivery failed"
                    };

                    _logger.LogInformation("📧 Organizer email {Status} for {EventTitle} to {OrganizerEmail}", 
                        organizerEmailSuccess ? "sent successfully" : "failed", eventTitle, organizerEmail);
                }
                catch (Exception emailEx)
                {
                    qrResult.OrganizerEmailResult = new EmailDeliveryResult
                    {
                        Success = false,
                        ErrorMessage = emailEx.Message,
                        RecipientEmail = organizerEmail,
                        EmailType = "Organizer"
                    };
                    
                    _logger.LogError(emailEx, "Failed to send organizer email for {EventTitle} to {OrganizerEmail}", eventTitle, organizerEmail);
                }

                // ✅ Overall success logging
                bool allEmailsSuccessful = qrResult.CustomerEmailResult.Success && qrResult.OrganizerEmailResult.Success;
                _logger.LogInformation("📧 Email summary for {EventTitle}: Customer={CustomerSuccess}, Organizer={OrganizerSuccess}", 
                    eventTitle, qrResult.CustomerEmailResult.Success, qrResult.OrganizerEmailResult.Success);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in SendConfirmationEmailsAsync for {EventTitle}", eventTitle);
                
                // Set both email results as failed for unexpected errors
                qrResult.CustomerEmailResult = new EmailDeliveryResult
                {
                    Success = false,
                    ErrorMessage = $"Unexpected error: {ex.Message}",
                    RecipientEmail = customerEmail,
                    EmailType = "Customer"
                };
                
                qrResult.OrganizerEmailResult = new EmailDeliveryResult
                {
                    Success = false,
                    ErrorMessage = $"Unexpected error: {ex.Message}",
                    RecipientEmail = organizerEmail,
                    EmailType = "Organizer"
                };
            }
        }
    }

    public class TicketLineItem
    {
        public string Type { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
