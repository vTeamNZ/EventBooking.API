using EventBooking.API.Data;
using EventBooking.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Stripe;
using Stripe.Checkout;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        private readonly IProcessingFeeService _processingFeeService;

        public BookingConfirmationService(
            AppDbContext context,
            ILogger<BookingConfirmationService> logger,
            IConfiguration configuration,
            IQRTicketService qrTicketService,
            IEmailService emailService,
            IProcessingFeeService processingFeeService)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _qrTicketService = qrTicketService;
            _emailService = emailService;
            _processingFeeService = processingFeeService;
        }

        public async Task<BookingConfirmationResult> ProcessPaymentSuccessAsync(string sessionId, string paymentIntentId)
        {
            var result = new BookingConfirmationResult();
            Booking? booking = null; // ✅ Declare booking outside try block for exception handling
            
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
                        _logger.LogInformation("🍕 DEBUG: Raw foodDetailsJson from Stripe metadata: '{FoodDetailsJson}' (Length: {Length})", foodDetailsJson, foodDetailsJson.Length);
                        
                        // Try to deserialize as JSON first
                        try 
                        {
                            foodDetails = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(foodDetailsJson);
                            _logger.LogInformation("✅ Successfully parsed {Count} food details from JSON metadata", foodDetails?.Count ?? 0);
                            
                            if (foodDetails != null && foodDetails.Any())
                            {
                                foreach (var food in foodDetails)
                                {
                                    _logger.LogInformation("🍕 Parsed food item: Name={Name}, Quantity={Quantity}, UnitPrice={UnitPrice}", 
                                        food.GetValueOrDefault("Name", "Unknown"),
                                        food.GetValueOrDefault("Quantity", 0),
                                        food.GetValueOrDefault("UnitPrice", 0));
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            // If JSON parsing fails, it might be truncated data - log and continue without food
                            _logger.LogError(ex, "❌ Food details JSON parsing failed: {FoodDetails}", foodDetailsJson);
                            foodDetails = new List<Dictionary<string, object>>();
                        }
                    }
                    else
                    {
                        _logger.LogInformation("ℹ️ No food details found in metadata");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing food details: {FoodDetails}", foodDetailsJson);
                    foodDetails = new List<Dictionary<string, object>>();
                }

                // 🎯 CREATE BOOKING WITH NEW ARCHITECTURE
                var totalAmount = (decimal)(session.AmountTotal ?? 0) / 100; // Convert from cents
                var processingFee = _processingFeeService.CalculateProcessingFee(totalAmount, eventEntity);
                
                booking = new Booking
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
                    // 🎯 ALLOCATED SEATING FIX: For allocated seating events, ticket details contain seat info like "Seat (F7)"
                    // We need to extract the actual ticket type from the selected seats data
                    
                    if (eventEntity.SeatSelectionMode == SeatSelectionMode.EventHall && selectedSeats.Any())
                    {
                        _logger.LogInformation("🎫 ALLOCATED SEATING - Processing ticket creation for {Count} seats: {Seats}", selectedSeats.Count, string.Join(", ", selectedSeats));
                        
                        // Get seat information from database to find ticket types
                        var seats = await _context.Seats
                            .Include(s => s.TicketType)
                            .Where(s => s.EventId == eventId && selectedSeats.Contains(s.SeatNumber))
                            .ToListAsync();
                        
                        _logger.LogInformation("Found {Count} seat records in database", seats.Count);
                        
                        // Group seats by ticket type to create one BookingLineItem per ticket type
                        var seatsByTicketType = seats.GroupBy(s => s.TicketTypeId).ToList();
                        
                        foreach (var ticketTypeGroup in seatsByTicketType)
                        {
                            var ticketTypeId = ticketTypeGroup.Key;
                            var seatsInGroup = ticketTypeGroup.ToList();
                            var ticketType = seatsInGroup.First().TicketType;
                            
                            if (ticketType != null)
                            {
                                var quantity = seatsInGroup.Count;
                                var seatNumbers = seatsInGroup.Select(s => s.SeatNumber).ToList();
                                
                                _logger.LogInformation("🎫 ALLOCATED SEATING - Creating BookingLineItem for TicketType '{Type}' (ID: {Id}), Quantity: {Quantity}, Seats: {Seats}", 
                                    ticketType.Type, ticketType.Id, quantity, string.Join(", ", seatNumbers));

                                // Process food items for this ticket type (seat-specific approach)
                                var processedFoodItems = new List<object>();
                                if (foodDetails != null && foodDetails.Any())
                                {
                                    // 🎯 SEAT-SPECIFIC FOOD PROCESSING: Only include food items assigned to seats in this ticket type
                                    var relevantFoodItems = new List<Dictionary<string, object>>();
                                    
                                    foreach (var food in foodDetails)
                                    {
                                        var seatTicketIdObj = food.TryGetValue("SeatTicketId", out var seatTicketIdCapital) ? seatTicketIdCapital :
                                                            food.TryGetValue("seatTicketId", out var seatTicketIdLower) ? seatTicketIdLower : null;
                                        
                                        if (seatTicketIdObj != null)
                                        {
                                            var seatTicketId = seatTicketIdObj.ToString();
                                            
                                            // Check if this food item is assigned to any seat in this ticket type group
                                            var isAssignedToThisTicketType = false;
                                            foreach (var seatNumber in seatNumbers)
                                            {
                                                var expectedSeatTicketId = $"seat-{seatNumber[0]}-{seatNumber.Substring(1)}"; // Convert F8 to seat-F-8
                                                if (seatTicketId.Equals(expectedSeatTicketId, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    isAssignedToThisTicketType = true;
                                                    break;
                                                }
                                            }
                                            
                                            if (isAssignedToThisTicketType)
                                            {
                                                relevantFoodItems.Add(food);
                                            }
                                        }
                                    }
                                    
                                    _logger.LogInformation("🍔 ALLOCATED SEATING - Found {Count} food items assigned to ticket type '{Type}' (seats: {Seats})", 
                                        relevantFoodItems.Count, ticketType.Type, string.Join(", ", seatNumbers));
                                    
                                    foreach (var food in relevantFoodItems)
                                    {
                                        var nameObj = food.TryGetValue("Name", out var nameCapital) ? nameCapital :
                                                    food.TryGetValue("name", out var nameLower) ? nameLower : null;
                                        var foodQuantityObj = food.TryGetValue("Quantity", out var foodQuantityCapital) ? foodQuantityCapital :
                                                            food.TryGetValue("quantity", out var foodQuantityLower) ? foodQuantityLower : null;
                                        var priceObj = food.TryGetValue("UnitPrice", out var unitPriceCapital) ? unitPriceCapital :
                                                     food.TryGetValue("unitPrice", out var unitPriceLower) ? unitPriceLower :
                                                     food.TryGetValue("Price", out var priceCapital) ? priceCapital :
                                                     food.TryGetValue("price", out var priceLower) ? priceLower : null;
                                        var seatTicketIdObj = food.TryGetValue("SeatTicketId", out var seatTicketIdCapital) ? seatTicketIdCapital :
                                                            food.TryGetValue("seatTicketId", out var seatTicketIdLower) ? seatTicketIdLower : null;

                                        if (nameObj != null && foodQuantityObj != null && priceObj != null &&
                                            int.TryParse(foodQuantityObj.ToString(), out int foodQuantity) && 
                                            decimal.TryParse(priceObj.ToString(), out decimal foodPrice))
                                        {
                                            var foodName = nameObj.ToString();
                                            var assignedSeat = seatTicketIdObj?.ToString() ?? "";
                                            
                                            // Find the food item in database for reference
                                            var foodItem = await _context.FoodItems
                                                .FirstOrDefaultAsync(fi => fi.EventId == eventId && fi.Name == foodName);
                                            
                                            var processedFood = new
                                            {
                                                name = foodName,
                                                quantity = foodQuantity,
                                                unitPrice = foodPrice,
                                                totalPrice = foodQuantity * foodPrice,
                                                databaseItemId = foodItem?.Id ?? 0,
                                                description = foodItem?.Description ?? "",
                                                category = "concession",
                                                assignedSeat = assignedSeat // 🎯 CRITICAL: Track seat assignment
                                            };
                                            
                                            processedFoodItems.Add(processedFood);
                                            _logger.LogInformation("🍔 SEAT-SPECIFIC - Added food '{FoodName}' (Qty: {Quantity}, Price: ${Price}) assigned to seat '{AssignedSeat}' for ticket type '{Type}'", 
                                                foodName, foodQuantity, foodPrice, assignedSeat, ticketType.Type);
                                        }
                                    }
                                }

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
                                        eventSeatMode = eventEntity.SeatSelectionMode.ToString(),
                                        allocatedSeats = seatNumbers // 🎯 CRITICAL: Include specific seat assignments
                                    }),
                                    ItemDetails = JsonSerializer.Serialize(new 
                                    {
                                        description = ticketType.Description,
                                        originalTicketData = new { Type = ticketType.Type, Quantity = quantity, UnitPrice = ticketType.Price },
                                        maxTickets = ticketType.MaxTickets,
                                        allocatedSeats = seatNumbers, // 🎯 CRITICAL: Seat assignments for PDF generation
                                        // ✅ SIMPLIFIED: Food items embedded in ticket record
                                        foodItems = processedFoodItems,
                                        foodItemsCount = processedFoodItems.Count
                                    }),
                                    QRCode = "", // Will be populated by QR service
                                    Status = "Active",
                                    CreatedAt = DateTime.UtcNow
                                };

                                bookingLineItems.Add(bookingLineItem);
                                _context.BookingLineItems.Add(bookingLineItem);
                            }
                        }
                    }
                    else
                    {
                        // 🎫 GENERAL ADMISSION - Original logic for general admission events
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

                                    // Process food items for this ticket (embedded approach)
                                    var processedFoodItems = new List<object>();
                                    if (foodDetails != null && foodDetails.Any())
                                    {
                                        _logger.LogInformation("🍔 SIMPLIFIED ARCHITECTURE - Embedding {Count} food items into ticket", foodDetails.Count);
                                        
                                        foreach (var food in foodDetails)
                                        {
                                            var nameObj = food.TryGetValue("Name", out var nameCapital) ? nameCapital :
                                                        food.TryGetValue("name", out var nameLower) ? nameLower : null;
                                            var foodQuantityObj = food.TryGetValue("Quantity", out var foodQuantityCapital) ? foodQuantityCapital :
                                                                food.TryGetValue("quantity", out var foodQuantityLower) ? foodQuantityLower : null;
                                            var priceObj = food.TryGetValue("UnitPrice", out var unitPriceCapital) ? unitPriceCapital :
                                                         food.TryGetValue("unitPrice", out var unitPriceLower) ? unitPriceLower :
                                                         food.TryGetValue("Price", out var priceCapital) ? priceCapital :
                                                         food.TryGetValue("price", out var priceLower) ? priceLower : null;

                                            if (nameObj != null && foodQuantityObj != null && priceObj != null &&
                                                int.TryParse(foodQuantityObj.ToString(), out int foodQuantity) && 
                                                decimal.TryParse(priceObj.ToString(), out decimal foodPrice))
                                            {
                                                var foodName = nameObj.ToString();
                                                
                                                // Find the food item in database for reference
                                                var foodItem = await _context.FoodItems
                                                    .FirstOrDefaultAsync(fi => fi.EventId == eventId && fi.Name == foodName);
                                                
                                                var processedFood = new
                                                {
                                                    name = foodName,
                                                    quantity = foodQuantity,
                                                    unitPrice = foodPrice,
                                                    totalPrice = foodQuantity * foodPrice,
                                                    databaseItemId = foodItem?.Id ?? 0,
                                                    description = foodItem?.Description ?? "",
                                                    category = "concession"
                                                };
                                                
                                                processedFoodItems.Add(processedFood);
                                                _logger.LogInformation("🍔 SIMPLIFIED ARCHITECTURE - Added food '{FoodName}' (Qty: {Quantity}, Price: ${Price}) to ticket", 
                                                    foodName, foodQuantity, foodPrice);
                                            }
                                        }
                                    }

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
                                            maxTickets = ticketType.MaxTickets,
                                            // ✅ SIMPLIFIED: Food items embedded in ticket record
                                            foodItems = processedFoodItems,
                                            foodItemsCount = processedFoodItems.Count
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
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ SIMPLIFIED ARCHITECTURE - Created {Count} BookingLineItems (tickets with embedded food)", bookingLineItems.Count);

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
                        seat.ReservedBy = null; // ✅ CLEAR reservation info when booked
                        seat.ReservedUntil = null; // ✅ CLEAR reservation timer when booked
                        
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
                            // 🎯 GET TICKET TYPE FOR THIS SEAT
                            var seatEntity = seats.FirstOrDefault(s => s.SeatNumber == seatNumber);
                            string? ticketTypeName = null;
                            
                            if (seatEntity != null)
                            {
                                // Get ticket type from the seat's TicketTypeId
                                var ticketType = await _context.TicketTypes
                                    .FirstOrDefaultAsync(tt => tt.Id == seatEntity.TicketTypeId);
                                ticketTypeName = ticketType?.Name ?? ticketType?.Type;
                                _logger.LogInformation("🎫 Found ticket type for seat {SeatNumber}: {TicketType}", 
                                    seatNumber, ticketTypeName ?? "Unknown");
                            }
                            
                            // 🎯 CRITICAL FIX: Get only food items assigned to THIS SPECIFIC SEAT
                            var seatSpecificFoodOrders = new List<FoodOrderInfo>();
                            if (foodOrders != null && foodOrders.Any())
                            {
                                var expectedSeatTicketId = $"seat-{seatNumber[0]}-{seatNumber.Substring(1)}"; // Convert F8 to seat-F-8
                                
                                foreach (var food in foodOrders)
                                {
                                    // Check if this food item is assigned to this specific seat
                                    if (!string.IsNullOrEmpty(food.SeatAssignment) && 
                                        food.SeatAssignment.Equals(expectedSeatTicketId, StringComparison.OrdinalIgnoreCase))
                                    {
                                        seatSpecificFoodOrders.Add(food);
                                        _logger.LogInformation("🍔 SEAT-SPECIFIC FOOD: Assigned '{FoodName}' (Qty: {Quantity}) to seat {SeatNumber}", 
                                            food.Name, food.Quantity, seatNumber);
                                    }
                                }
                                
                                _logger.LogInformation("🍔 SEAT-SPECIFIC SUMMARY: Seat {SeatNumber} has {FoodCount} food items assigned", 
                                    seatNumber, seatSpecificFoodOrders.Count);
                            }
                            
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
                                FoodOrders = seatSpecificFoodOrders, // 🎯 CRITICAL FIX: Pass only seat-specific food orders
                                EventImageUrl = eventEntity.ImageUrl, // ✅ Pass event flyer for professional appearance
                                TicketType = ticketTypeName, // ✅ Pass ticket type for PDF display
                                BookingReference = $"BK-{booking.Id:D6}" // ✅ Pass booking reference for PDF display
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
                                ErrorMessage = qrResult.ErrorMessage,
                                QRCodeImage = qrResult.QRCodeImage, // Store QR code image for email
                                SeatSpecificFoodOrders = seatSpecificFoodOrders // Store seat-specific food orders
                            });
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
                                
                                // 🎯 CRITICAL FIX: Get only food items assigned to THIS SPECIFIC TICKET
                                var ticketSpecificFoodOrders = new List<FoodOrderInfo>();
                                if (foodOrders != null && foodOrders.Any())
                                {
                                    var expectedTicketId = $"ticket-{lineItem.ItemName}-{i}"; // Format: ticket-Standard01-1
                                    
                                    foreach (var food in foodOrders)
                                    {
                                        // Check if this food item is assigned to this specific ticket
                                        if (!string.IsNullOrEmpty(food.SeatAssignment) && 
                                            food.SeatAssignment.Equals(expectedTicketId, StringComparison.OrdinalIgnoreCase))
                                        {
                                            ticketSpecificFoodOrders.Add(food);
                                            _logger.LogInformation("🍔 TICKET-SPECIFIC FOOD: Assigned '{FoodName}' (Qty: {Quantity}) to ticket {TicketId}", 
                                                food.Name, food.Quantity, expectedTicketId);
                                        }
                                    }
                                    
                                    _logger.LogInformation("🍔 TICKET-SPECIFIC SUMMARY: Ticket {TicketId} has {FoodCount} food items assigned", 
                                        expectedTicketId, ticketSpecificFoodOrders.Count);
                                }
                                
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
                                    FoodOrders = ticketSpecificFoodOrders, // 🎯 CRITICAL FIX: Pass only ticket-specific food orders
                                    EventImageUrl = eventEntity.ImageUrl, // ✅ Pass event flyer for professional appearance
                                    TicketType = lineItem.ItemName, // ✅ Pass ticket type name for general admission
                                    BookingReference = $"BK-{booking.Id:D6}" // ✅ Pass booking reference for PDF display
                                };

                                var qrResult = await _qrTicketService.GenerateQRTicketAsync(qrRequest);
                                
                                // 🎯 UPDATE THE BOOKING LINE ITEM WITH QR CODE (first ticket gets the QR)
                                if (i == 1 && qrResult.Success)
                                {
                                    lineItem.QRCode = qrResult.BookingId ?? paymentIntentId;
                                    _logger.LogInformation("🎯 Updated BookingLineItem {Id} with QR code for {ItemName}", 
                                        lineItem.Id, lineItem.ItemName);
                                }
                                
                                // 🔄 CRITICAL FIX: Always add every generated ticket to the results list
                                qrResults.Add(new QRGenerationResult 
                                {
                                    SeatNumber = ticketIdentifier, // Will contain ticket type info
                                    Success = qrResult.Success,
                                    TicketPath = qrResult.TicketPath,
                                    BookingId = qrResult.BookingId,
                                    ErrorMessage = qrResult.ErrorMessage,
                                    QRCodeImage = qrResult.QRCodeImage, // Store QR code image for email
                                    SeatSpecificFoodOrders = ticketSpecificFoodOrders // 🎯 CRITICAL FIX: Store ticket-specific food orders
                                });
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
                
                // 🎯 ENHANCED: Send individual emails with beautiful template per ticket (USER PREFERRED APPROACH)
                await SendEnhancedIndividualTicketEmailsAsync(
                    qrResults,
                    session.CustomerEmail ?? "",
                    eventTitle ?? eventEntity.Title,
                    firstName ?? "Guest",
                    eventEntity.Organizer?.ContactEmail ?? "",
                    eventEntity.ImageUrl,
                    booking.Id
                );
                
                // ✅ Calculate processing summary for user feedback (ENHANCED APPROACH - Individual emails)
                result.ProcessingSummary = new ProcessingSummary
                {
                    TotalTickets = qrResults.Count,
                    SuccessfulQRGenerations = qrResults.Count(qr => qr.Success),
                    FailedQRGenerations = qrResults.Count(qr => !qr.Success),
                    SuccessfulCustomerEmails = qrResults.Count(qr => qr.CustomerEmailResult.Success), // ✅ Count individual emails per ticket
                    FailedCustomerEmails = qrResults.Count(qr => !qr.CustomerEmailResult.Success), // ✅ Count individual emails per ticket
                    SuccessfulOrganizerEmails = qrResults.Count(qr => qr.OrganizerEmailResult.Success), // ✅ Count individual emails per ticket
                    FailedOrganizerEmails = qrResults.Count(qr => !qr.OrganizerEmailResult.Success) // ✅ Count individual emails per ticket
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
                
                _logger.LogInformation("🎉 ENHANCED EMAIL SUCCESS - Processed payment for session: {SessionId}, booking ID: {BookingId}, line items: {LineItemCount}, event type: {EventType}, summary: {Summary}", 
                    sessionId, booking.Id, bookingLineItems.Count, eventEntity.SeatSelectionMode, result.ProcessingSummary.GetStatusMessage());
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment success for session: {SessionId}", sessionId);
                
                // ✅ Mark booking as failed if any error occurs and booking was created
                if (booking != null)
                {
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
                }
                
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Extract food order information from BookingLineItems for display in PDFs and emails
        /// NEW: Extracts food items from embedded JSON in ticket ItemDetails
        /// </summary>
        private List<FoodOrderInfo> ExtractFoodOrdersFromLineItems(List<BookingLineItem> bookingLineItems)
        {
            try
            {
                var foodOrders = new List<FoodOrderInfo>();
                
                // NEW: Look for food items embedded in ticket records instead of separate Food records
                var ticketLineItems = bookingLineItems.Where(bli => bli.ItemType == "Ticket").ToList();
                
                foreach (var ticketItem in ticketLineItems)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(ticketItem.ItemDetails))
                        {
                            var itemDetails = JsonSerializer.Deserialize<Dictionary<string, object>>(ticketItem.ItemDetails);
                            
                            if (itemDetails.TryGetValue("foodItems", out var foodItemsObj))
                            {
                                var foodItemsJson = JsonSerializer.Serialize(foodItemsObj);
                                var foodItems = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(foodItemsJson);
                                
                                if (foodItems != null)
                                {
                                    foreach (var food in foodItems)
                                    {
                                        var name = food.TryGetValue("name", out var nameObj) ? nameObj.ToString() : "";
                                        var quantity = food.TryGetValue("quantity", out var qtyObj) && int.TryParse(qtyObj.ToString(), out int qty) ? qty : 0;
                                        var unitPrice = food.TryGetValue("unitPrice", out var priceObj) && decimal.TryParse(priceObj.ToString(), out decimal price) ? price : 0;
                                        var totalPrice = food.TryGetValue("totalPrice", out var totalObj) && decimal.TryParse(totalObj.ToString(), out decimal total) ? total : (quantity * unitPrice);
                                        var description = food.TryGetValue("description", out var descObj) ? descObj.ToString() : "";
                                        
                                        // 🎯 CRITICAL: Extract seat assignment information
                                        var seatAssignment = food.TryGetValue("assignedSeat", out var seatObj) ? seatObj.ToString() : "";
                                        
                                        if (!string.IsNullOrEmpty(name) && quantity > 0)
                                        {
                                            foodOrders.Add(new FoodOrderInfo
                                            {
                                                Name = name,
                                                Quantity = quantity,
                                                UnitPrice = unitPrice,
                                                TotalPrice = totalPrice,
                                                Description = description,
                                                SeatAssignment = seatAssignment // 🎯 CRITICAL: Store seat assignment
                                            });
                                            
                                            _logger.LogInformation("🍔 EXTRACTED FOOD ITEM: '{Name}' (Qty: {Quantity}) assigned to seat '{SeatAssignment}'", 
                                                name, quantity, seatAssignment ?? "No assignment");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse ItemDetails JSON for ticket {TicketId}", ticketItem.Id);
                    }
                }
                
                _logger.LogInformation("✅ SIMPLIFIED ARCHITECTURE - Extracted {Count} food orders from embedded ticket data", foodOrders.Count);
                return foodOrders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting food orders from BookingLineItems");
                return new List<FoodOrderInfo>(); // Return empty list if extraction fails
            }
        }

        /// <summary>
        /// [DEPRECATED] Send confirmation emails for successful QR ticket generation and track results
        /// This method is kept for backward compatibility but is no longer used directly.
        /// Use SendConsolidatedBookingEmailAsync instead.
        /// </summary>
        private async Task SendConfirmationEmailsAsync(
            QRGenerationResult qrResult, 
            string customerEmail, 
            string eventTitle, 
            string firstName, 
            string organizerEmail, 
            List<FoodOrderInfo> foodOrders,
            string? eventImageUrl = null,
            byte[]? qrCodeImage = null,
            string? bookingId = null)
        {
            try
            {
                // Log deprecation warning
                _logger.LogWarning("⚠️ DEPRECATED: SendConfirmationEmailsAsync was called directly. This method is deprecated and will be removed in a future version. Use SendConsolidatedBookingEmailAsync instead.");
                
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
                    // 🔧 Convert relative URL to full URL for email embedding
                    var fullEventImageUrl = GetFullImageUrl(eventImageUrl);
                    
                    // ✅ Send enhanced customer email with embedded QR code and track result
                    bool customerEmailSuccess = await _emailService.SendEnhancedTicketEmailAsync(
                        customerEmail,
                        eventTitle,
                        firstName,
                        ticketPdf,
                        foodOrders,
                        fullEventImageUrl, // ✅ Include event flyer with full URL for proper embedding
                        qrCodeImage, // Include QR code bytes for embedded display
                        bookingId // Include booking ID for tracking
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
                    // 🔧 Use the same full URL for organizer email (consistent image embedding)
                    var fullEventImageUrl = GetFullImageUrl(eventImageUrl);
                    
                    bool organizerEmailSuccess = await _emailService.SendOrganizerNotificationAsync(
                        organizerEmail,
                        eventTitle,
                        firstName,
                        customerEmail,
                        ticketPdf,
                        foodOrders,
                        fullEventImageUrl // ✅ Include event flyer with full URL for proper embedding
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

        /// <summary>
        /// 🎯 NEW: Send one consolidated email per booking with all seats and their specific food items
        /// </summary>
        private async Task SendConsolidatedBookingEmailAsync(
            List<QRGenerationResult> qrResults,
            string customerEmail,
            string eventTitle,
            string firstName,
            string organizerEmail,
            string? eventImageUrl,
            int bookingId)
        {
            try
            {
                // Only send emails if we have successful QR generations
                var successfulQRs = qrResults.Where(qr => qr.Success && !string.IsNullOrEmpty(qr.TicketPath)).ToList();
                
                if (!successfulQRs.Any())
                {
                    _logger.LogWarning("🎯 No successful QR generations found, skipping email sending for booking {BookingId}", bookingId);
                    
                    // Mark all QR results as email failed due to no successful tickets
                    foreach (var qr in qrResults)
                    {
                        qr.CustomerEmailResult = new EmailDeliveryResult
                        {
                            Success = false,
                            ErrorMessage = "No successful tickets generated",
                            RecipientEmail = customerEmail,
                            EmailType = "Customer"
                        };
                        qr.OrganizerEmailResult = new EmailDeliveryResult
                        {
                            Success = false,
                            ErrorMessage = "No successful tickets generated",
                            RecipientEmail = organizerEmail,
                            EmailType = "Organizer"
                        };
                    }
                    return;
                }

                // Prepare ticket PDFs as attachments
                var ticketAttachments = new List<(byte[] PdfData, string FileName)>();
                var allFoodOrders = new List<FoodOrderInfo>();
                
                foreach (var qr in successfulQRs)
                {
                    // Read ticket PDF for attachment
                    if (!string.IsNullOrEmpty(qr.TicketPath) && System.IO.File.Exists(qr.TicketPath))
                    {
                        byte[] ticketPdf = await System.IO.File.ReadAllBytesAsync(qr.TicketPath);
                        string fileName = $"Ticket_{qr.SeatNumber}_{eventTitle.Replace(" ", "_")}.pdf";
                        ticketAttachments.Add((ticketPdf, fileName));
                    }
                    
                    // Collect all seat-specific food orders
                    if (qr.SeatSpecificFoodOrders != null && qr.SeatSpecificFoodOrders.Any())
                    {
                        // Add seat information to food orders for email display
                        foreach (var food in qr.SeatSpecificFoodOrders)
                        {
                            var foodWithSeat = new FoodOrderInfo
                            {
                                Name = food.Name,
                                Quantity = food.Quantity,
                                UnitPrice = food.UnitPrice,
                                SeatAssignment = qr.SeatNumber // 🎯 Include seat info for email display
                            };
                            allFoodOrders.Add(foodWithSeat);
                        }
                    }
                }

                _logger.LogInformation("🎯 CONSOLIDATED EMAIL: Sending 1 email with {TicketCount} tickets and {FoodCount} food items to {CustomerEmail}", 
                    successfulQRs.Count, allFoodOrders.Count, customerEmail);

                // 🔧 Convert relative URL to full URL for email embedding
                var fullEventImageUrl = GetFullImageUrl(eventImageUrl);

                // Send customer email with all tickets and seat-specific food
                bool customerEmailSuccess = false;
                try
                {
                    customerEmailSuccess = await _emailService.SendConsolidatedBookingEmailAsync(
                        customerEmail,
                        eventTitle,
                        firstName,
                        ticketAttachments,
                        allFoodOrders,
                        fullEventImageUrl,
                        $"BK-{bookingId:D6}"
                    );

                    _logger.LogInformation("📧 CONSOLIDATED Customer email {Status} for {EventTitle} to {CustomerEmail}", 
                        customerEmailSuccess ? "sent successfully" : "failed", eventTitle, customerEmail);
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Failed to send consolidated customer email for {EventTitle} to {CustomerEmail}", eventTitle, customerEmail);
                    customerEmailSuccess = false;
                }

                // Send organizer email with all tickets and seat-specific food
                bool organizerEmailSuccess = false;
                try
                {
                    organizerEmailSuccess = await _emailService.SendConsolidatedOrganizerNotificationAsync(
                        organizerEmail,
                        eventTitle,
                        firstName,
                        customerEmail,
                        ticketAttachments,
                        allFoodOrders,
                        fullEventImageUrl,
                        $"BK-{bookingId:D6}"
                    );

                    _logger.LogInformation("📧 CONSOLIDATED Organizer email {Status} for {EventTitle} to {OrganizerEmail}", 
                        organizerEmailSuccess ? "sent successfully" : "failed", eventTitle, organizerEmail);
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Failed to send consolidated organizer email for {EventTitle} to {OrganizerEmail}", eventTitle, organizerEmail);
                    organizerEmailSuccess = false;
                }

                // Update all QR results with the consolidated email status (same result for all)
                foreach (var qr in qrResults)
                {
                    qr.CustomerEmailResult = new EmailDeliveryResult
                    {
                        Success = customerEmailSuccess,
                        SentAt = customerEmailSuccess ? DateTime.UtcNow : null,
                        RecipientEmail = customerEmail,
                        EmailType = "Customer",
                        ErrorMessage = customerEmailSuccess ? null : "Consolidated email delivery failed"
                    };
                    
                    qr.OrganizerEmailResult = new EmailDeliveryResult
                    {
                        Success = organizerEmailSuccess,
                        SentAt = organizerEmailSuccess ? DateTime.UtcNow : null,
                        RecipientEmail = organizerEmail,
                        EmailType = "Organizer",
                        ErrorMessage = organizerEmailSuccess ? null : "Consolidated email delivery failed"
                    };
                }

                _logger.LogInformation("📧 CONSOLIDATED Email summary for {EventTitle}: Customer={CustomerSuccess}, Organizer={OrganizerSuccess}, Tickets={TicketCount}", 
                    eventTitle, customerEmailSuccess, organizerEmailSuccess, successfulQRs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in SendConsolidatedBookingEmailAsync for {EventTitle}, booking {BookingId}", eventTitle, bookingId);
                
                // Mark all QR results as failed for unexpected errors
                foreach (var qr in qrResults)
                {
                    qr.CustomerEmailResult = new EmailDeliveryResult
                    {
                        Success = false,
                        ErrorMessage = $"Unexpected error: {ex.Message}",
                        RecipientEmail = customerEmail,
                        EmailType = "Customer"
                    };
                    
                    qr.OrganizerEmailResult = new EmailDeliveryResult
                    {
                        Success = false,
                        ErrorMessage = $"Unexpected error: {ex.Message}",
                        RecipientEmail = organizerEmail,
                        EmailType = "Organizer"
                    };
                }
            }
        }

        /// <summary>
        /// Convert relative image URLs to full URLs for email embedding (Test env: thelankanspace.co.nz/kw)
        /// </summary>
        private string? GetFullImageUrl(string? relativeUrl)
        {
            if (string.IsNullOrEmpty(relativeUrl))
                return null;

            // If it's already a full URL, return as is
            if (relativeUrl.StartsWith("http://") || relativeUrl.StartsWith("https://"))
                return relativeUrl;

            // Use configured base URL for environment-specific URLs (Test: thelankanspace.co.nz/kw)
            var baseUrl = _configuration["ApplicationSettings:BaseUrl"] 
                         ?? _configuration["QRTickets:BaseUrl"] 
                         ?? "https://kiwilanka.co.nz"; // Fallback

            var fullUrl = $"{baseUrl.TrimEnd('/')}{(relativeUrl.StartsWith("/") ? relativeUrl : "/" + relativeUrl)}";
            
            _logger.LogDebug("📧 Converted relative URL '{RelativeUrl}' to full URL '{FullUrl}' using base '{BaseUrl}'", 
                relativeUrl, fullUrl, baseUrl);
            
            return fullUrl;
        }

        /// <summary>
        /// 🎯 ENHANCED: Send individual emails with beautiful template per ticket (USER PREFERRED APPROACH)
        /// Combines the best of both worlds: Multiple tickets with the enhanced email template
        /// </summary>
        private async Task SendEnhancedIndividualTicketEmailsAsync(
            List<QRGenerationResult> qrResults,
            string customerEmail,
            string eventTitle,
            string firstName,
            string organizerEmail,
            string? eventImageUrl,
            int bookingId)
        {
            try
            {
                // Only send emails if we have successful QR generations
                var successfulQRs = qrResults.Where(qr => qr.Success && !string.IsNullOrEmpty(qr.TicketPath)).ToList();
                
                if (!successfulQRs.Any())
                {
                    _logger.LogWarning("🎯 ENHANCED - No successful QR generations found, skipping email sending for booking {BookingId}", bookingId);
                    
                    // Mark all QR results as email failed due to no successful tickets
                    foreach (var qr in qrResults)
                    {
                        qr.CustomerEmailResult = new EmailDeliveryResult
                        {
                            Success = false,
                            ErrorMessage = "No successful tickets generated",
                            RecipientEmail = customerEmail,
                            EmailType = "Customer"
                        };
                        qr.OrganizerEmailResult = new EmailDeliveryResult
                        {
                            Success = false,
                            ErrorMessage = "No successful tickets generated",
                            RecipientEmail = organizerEmail,
                            EmailType = "Organizer"
                        };
                    }
                    return;
                }

                _logger.LogInformation("🎯 ENHANCED EMAIL: Sending {TicketCount} individual emails with beautiful template to {CustomerEmail}", 
                    successfulQRs.Count, customerEmail);

                // 🔧 Convert relative URL to full URL for email embedding
                var fullEventImageUrl = GetFullImageUrl(eventImageUrl);

                // Send individual enhanced email for each ticket
                int successfulCustomerEmails = 0;
                int successfulOrganizerEmails = 0;

                foreach (var qr in successfulQRs)
                {
                    try
                    {
                        // Read ticket PDF for attachment
                        byte[] ticketPdf = null;
                        if (!string.IsNullOrEmpty(qr.TicketPath) && System.IO.File.Exists(qr.TicketPath))
                        {
                            ticketPdf = await System.IO.File.ReadAllBytesAsync(qr.TicketPath);
                        }

                        if (ticketPdf == null)
                        {
                            _logger.LogWarning("🎯 ENHANCED - Ticket PDF not found for seat {SeatNumber}", qr.SeatNumber);
                            qr.CustomerEmailResult = new EmailDeliveryResult
                            {
                                Success = false,
                                ErrorMessage = "Ticket PDF not found",
                                RecipientEmail = customerEmail,
                                EmailType = "Customer"
                            };
                            qr.OrganizerEmailResult = new EmailDeliveryResult
                            {
                                Success = false,
                                ErrorMessage = "Ticket PDF not found",
                                RecipientEmail = organizerEmail,
                                EmailType = "Organizer"
                            };
                            continue;
                        }

                        // Send customer email with enhanced template
                        bool customerEmailSuccess = false;
                        try
                        {
                            customerEmailSuccess = await _emailService.SendEnhancedTicketEmailAsync(
                                customerEmail,
                                eventTitle,
                                firstName,
                                ticketPdf,
                                qr.SeatSpecificFoodOrders, // Seat-specific food orders
                                fullEventImageUrl,
                                qr.QRCodeImage,
                                $"BK-{bookingId:D6}-{qr.SeatNumber}"
                            );

                            if (customerEmailSuccess) successfulCustomerEmails++;
                            
                            _logger.LogInformation("📧 ENHANCED Customer email for seat {SeatNumber} {Status} for {EventTitle} to {CustomerEmail}", 
                                qr.SeatNumber, customerEmailSuccess ? "sent successfully" : "failed", eventTitle, customerEmail);
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogError(emailEx, "Failed to send enhanced customer email for seat {SeatNumber} to {CustomerEmail}", qr.SeatNumber, customerEmail);
                            customerEmailSuccess = false;
                        }

                        // Send organizer notification
                        bool organizerEmailSuccess = false;
                        try
                        {
                            organizerEmailSuccess = await _emailService.SendOrganizerNotificationAsync(
                                organizerEmail,
                                eventTitle,
                                firstName,
                                customerEmail,
                                ticketPdf,
                                qr.SeatSpecificFoodOrders,
                                fullEventImageUrl
                            );

                            if (organizerEmailSuccess) successfulOrganizerEmails++;
                            
                            _logger.LogInformation("📧 ENHANCED Organizer notification for seat {SeatNumber} {Status} for {EventTitle} to {OrganizerEmail}", 
                                qr.SeatNumber, organizerEmailSuccess ? "sent successfully" : "failed", eventTitle, organizerEmail);
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogError(emailEx, "Failed to send enhanced organizer notification for seat {SeatNumber} to {OrganizerEmail}", qr.SeatNumber, organizerEmail);
                            organizerEmailSuccess = false;
                        }

                        // Update QR result with email status
                        qr.CustomerEmailResult = new EmailDeliveryResult
                        {
                            Success = customerEmailSuccess,
                            SentAt = customerEmailSuccess ? DateTime.UtcNow : null,
                            RecipientEmail = customerEmail,
                            EmailType = "Customer",
                            ErrorMessage = customerEmailSuccess ? null : "Enhanced email delivery failed"
                        };
                        
                        qr.OrganizerEmailResult = new EmailDeliveryResult
                        {
                            Success = organizerEmailSuccess,
                            SentAt = organizerEmailSuccess ? DateTime.UtcNow : null,
                            RecipientEmail = organizerEmail,
                            EmailType = "Organizer",
                            ErrorMessage = organizerEmailSuccess ? null : "Enhanced email delivery failed"
                        };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unexpected error processing enhanced email for seat {SeatNumber}", qr.SeatNumber);
                        
                        // Mark as failed
                        qr.CustomerEmailResult = new EmailDeliveryResult
                        {
                            Success = false,
                            ErrorMessage = $"Unexpected error: {ex.Message}",
                            RecipientEmail = customerEmail,
                            EmailType = "Customer"
                        };
                        qr.OrganizerEmailResult = new EmailDeliveryResult
                        {
                            Success = false,
                            ErrorMessage = $"Unexpected error: {ex.Message}",
                            RecipientEmail = organizerEmail,
                            EmailType = "Organizer"
                        };
                    }
                }

                _logger.LogInformation("📧 ENHANCED Email summary for {EventTitle}: Customer={CustomerSuccess}/{CustomerTotal}, Organizer={OrganizerSuccess}/{OrganizerTotal}, Tickets={TicketCount}", 
                    eventTitle, successfulCustomerEmails, successfulQRs.Count, successfulOrganizerEmails, successfulQRs.Count, successfulQRs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in SendEnhancedIndividualTicketEmailsAsync for {EventTitle}, booking {BookingId}", eventTitle, bookingId);
                
                // Mark all QR results as failed for unexpected errors
                foreach (var qr in qrResults)
                {
                    qr.CustomerEmailResult = new EmailDeliveryResult
                    {
                        Success = false,
                        ErrorMessage = $"Unexpected system error: {ex.Message}",
                        RecipientEmail = customerEmail,
                        EmailType = "Customer"
                    };
                    qr.OrganizerEmailResult = new EmailDeliveryResult
                    {
                        Success = false,
                        ErrorMessage = $"Unexpected system error: {ex.Message}",
                        RecipientEmail = organizerEmail,
                        EmailType = "Organizer"
                    };
                }
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
