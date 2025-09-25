using EventBooking.API.Data;
using EventBooking.API.Models;
using EventBooking.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EventBooking.API.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class BookingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BookingsController> _logger;
        private readonly IQRTicketService _qrTicketService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public BookingsController(
            AppDbContext context, 
            ILogger<BookingsController> logger,
            IQRTicketService qrTicketService,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _qrTicketService = qrTicketService;
            _emailService = emailService;
            _configuration = configuration;
        }

        // GET: api/Bookings
        [HttpGet]
        [Authorize(Roles = "Admin,Organizer")]
        public async Task<ActionResult<IEnumerable<BookingListDTO>>> GetBookings(
            [FromQuery] int? eventId = null,
            [FromQuery] string? status = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            try
            {
                var query = _context.Bookings
                    .Include(b => b.Event)
                    .Include(b => b.BookingLineItems)
                    .AsQueryable();

                // Apply filters
                if (eventId.HasValue)
                    query = query.Where(b => b.EventId == eventId.Value);

                if (!string.IsNullOrEmpty(status))
                    query = query.Where(b => b.Status == status);

                if (fromDate.HasValue)
                    query = query.Where(b => b.CreatedAt >= fromDate.Value);

                if (toDate.HasValue)
                    query = query.Where(b => b.CreatedAt <= toDate.Value);

                // Get total count for pagination
                var totalCount = await query.CountAsync();

                // Apply pagination and get results
                var bookings = await query
                    .OrderByDescending(b => b.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(b => new BookingListDTO
                    {
                        Id = b.Id,
                        EventId = b.EventId,
                        EventTitle = b.Event.Title,
                        CustomerEmail = b.CustomerEmail,
                        CustomerName = $"{b.CustomerFirstName} {b.CustomerLastName}".Trim(),
                        TotalAmount = b.TotalAmount,
                        ProcessingFee = b.ProcessingFee,
                        Currency = b.Currency,
                        PaymentStatus = b.PaymentStatus,
                        Status = b.Status,
                        CreatedAt = b.CreatedAt,
                        ItemCount = b.BookingLineItems.Count(),
                        TicketCount = b.BookingLineItems.Where(bli => bli.ItemType == "Ticket").Sum(bli => bli.Quantity),
                        FoodCount = b.BookingLineItems.Where(bli => bli.ItemType == "Food").Sum(bli => bli.Quantity)
                    })
                    .ToListAsync();

                Response.Headers.Add("X-Total-Count", totalCount.ToString());
                Response.Headers.Add("X-Page", page.ToString());
                Response.Headers.Add("X-Page-Size", pageSize.ToString());

                return Ok(bookings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving bookings");
                return StatusCode(500, new { message = "Error retrieving bookings" });
            }
        }

        // GET: api/Bookings/5
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<BookingDetailDTO>> GetBooking(int id)
        {
            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.Event)
                        .ThenInclude(e => e.Organizer)
                    .Include(b => b.BookingLineItems)
                    .FirstOrDefaultAsync(b => b.Id == id);

                if (booking == null)
                {
                    return NotFound(new { message = "Booking not found" });
                }

                // Check authorization - users can only view their own bookings unless admin/organizer
                var userEmail = User.Identity?.Name;
                var userRoles = User.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToList();
                var isAdminOrOrganizer = userRoles.Contains("Admin") || userRoles.Contains("Organizer");
                
                if (!isAdminOrOrganizer && booking.CustomerEmail != userEmail)
                {
                    return Forbid();
                }

                var bookingDetail = new BookingDetailDTO
                {
                    Id = booking.Id,
                    EventId = booking.EventId,
                    EventTitle = booking.Event.Title,
                    EventDate = booking.Event.Date,
                    EventLocation = booking.Event.Location,
                    OrganizerName = booking.Event.Organizer?.Name,
                    CustomerEmail = booking.CustomerEmail,
                    CustomerFirstName = booking.CustomerFirstName,
                    CustomerLastName = booking.CustomerLastName,
                    CustomerMobile = booking.CustomerMobile,
                    TotalAmount = booking.TotalAmount,
                    ProcessingFee = booking.ProcessingFee,
                    Currency = booking.Currency,
                    PaymentIntentId = booking.PaymentIntentId,
                    PaymentStatus = booking.PaymentStatus,
                    Status = booking.Status,
                    CreatedAt = booking.CreatedAt,
                    Metadata = booking.Metadata,
                    LineItems = booking.BookingLineItems.Select(bli => new BookingLineItemDTO
                    {
                        Id = bli.Id,
                        ItemType = bli.ItemType,
                        ItemId = bli.ItemId,
                        ItemName = bli.ItemName,
                        Quantity = bli.Quantity,
                        UnitPrice = bli.UnitPrice,
                        TotalPrice = bli.TotalPrice,
                        SeatDetails = bli.SeatDetails,
                        ItemDetails = bli.ItemDetails,
                        QRCode = bli.QRCode,
                        Status = bli.Status,
                        CreatedAt = bli.CreatedAt
                    }).ToList()
                };

                return Ok(bookingDetail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving booking {BookingId}", id);
                return StatusCode(500, new { message = "Error retrieving booking" });
            }
        }

        // GET: api/Bookings/{id}/line-items
        [HttpGet("{id}/line-items")]
        [Authorize]
        public async Task<ActionResult<List<BookingLineItemDTO>>> GetBookingLineItems(int id)
        {
            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.BookingLineItems)
                    .FirstOrDefaultAsync(b => b.Id == id);

                if (booking == null)
                {
                    return NotFound(new { message = "Booking not found" });
                }

                // Check authorization
                var userEmail = User.Identity?.Name;
                var userRoles = User.Claims.Where(c => c.Type == "role").Select(c => c.Value).ToList();
                var isAdminOrOrganizer = userRoles.Contains("Admin") || userRoles.Contains("Organizer");
                
                if (!isAdminOrOrganizer && booking.CustomerEmail != userEmail)
                {
                    return Forbid();
                }

                var lineItems = booking.BookingLineItems.Select(bli => new BookingLineItemDTO
                {
                    Id = bli.Id,
                    ItemType = bli.ItemType,
                    ItemId = bli.ItemId,
                    ItemName = bli.ItemName,
                    Quantity = bli.Quantity,
                    UnitPrice = bli.UnitPrice,
                    TotalPrice = bli.TotalPrice,
                    SeatDetails = bli.SeatDetails,
                    ItemDetails = bli.ItemDetails,
                    QRCode = bli.QRCode,
                    Status = bli.Status,
                    CreatedAt = bli.CreatedAt
                }).ToList();

                return Ok(lineItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving line items for booking {BookingId}", id);
                return StatusCode(500, new { message = "Error retrieving booking line items" });
            }
        }

        // GET: api/Bookings/my-bookings
        [HttpGet("my-bookings")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<BookingListDTO>>> GetMyBookings()
        {
            try
            {
                var userEmail = User.Identity?.Name;
                if (string.IsNullOrEmpty(userEmail))
                {
                    return Unauthorized();
                }

                var bookings = await _context.Bookings
                    .Include(b => b.Event)
                    .Include(b => b.BookingLineItems)
                    .Where(b => b.CustomerEmail == userEmail)
                    .OrderByDescending(b => b.CreatedAt)
                    .Select(b => new BookingListDTO
                    {
                        Id = b.Id,
                        EventId = b.EventId,
                        EventTitle = b.Event.Title,
                        CustomerEmail = b.CustomerEmail,
                        CustomerName = $"{b.CustomerFirstName} {b.CustomerLastName}".Trim(),
                        TotalAmount = b.TotalAmount,
                        ProcessingFee = b.ProcessingFee,
                        Currency = b.Currency,
                        PaymentStatus = b.PaymentStatus,
                        Status = b.Status,
                        CreatedAt = b.CreatedAt,
                        ItemCount = b.BookingLineItems.Count(),
                        TicketCount = b.BookingLineItems.Where(bli => bli.ItemType == "Ticket").Sum(bli => bli.Quantity),
                        FoodCount = b.BookingLineItems.Where(bli => bli.ItemType == "Food").Sum(bli => bli.Quantity)
                    })
                    .ToListAsync();

                return Ok(bookings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user bookings for {UserEmail}", User.Identity?.Name);
                return StatusCode(500, new { message = "Error retrieving your bookings" });
            }
        }

        // PUT: api/Bookings/{id}/status
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin,Organizer")]
        public async Task<IActionResult> UpdateBookingStatus(int id, [FromBody] UpdateBookingStatusRequest request)
        {
            try
            {
                var booking = await _context.Bookings.FindAsync(id);
                if (booking == null)
                {
                    return NotFound(new { message = "Booking not found" });
                }

                var oldStatus = booking.Status;
                booking.Status = request.Status;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Booking {BookingId} status updated from {OldStatus} to {NewStatus} by {User}", 
                    id, oldStatus, request.Status, User.Identity?.Name);

                return Ok(new { message = "Booking status updated successfully", oldStatus, newStatus = request.Status });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating booking status for booking {BookingId}", id);
                return StatusCode(500, new { message = "Error updating booking status" });
            }
        }

        // POST: api/Bookings/{id}/refund
        [HttpPost("{id}/refund")]
        [Authorize(Roles = "Admin,Organizer")]
        public async Task<ActionResult> ProcessRefund(int id, [FromBody] RefundRequest request)
        {
            try
            {
                var booking = await _context.Bookings.FindAsync(id);
                if (booking == null)
                {
                    return NotFound(new { message = "Booking not found" });
                }

                if (booking.PaymentStatus != "Completed")
                {
                    return BadRequest(new { message = "Can only refund completed payments" });
                }

                // TODO: Implement actual Stripe refund processing
                // For now, just update the booking status
                booking.Status = "Refunded";
                booking.PaymentStatus = "Refunded";

                await _context.SaveChangesAsync();

                _logger.LogInformation("Refund processed for booking {BookingId} by {User}. Reason: {Reason}", 
                    id, User.Identity?.Name, request.Reason);

                return Ok(new { 
                    message = "Refund processed successfully", 
                    bookingId = id, 
                    status = "Refunded",
                    note = "Stripe refund processing not yet implemented" 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing refund for booking {BookingId}", id);
                return StatusCode(500, new { message = "Error processing refund" });
            }
        }

        // POST: api/Bookings/organizer-direct
        [HttpPost("organizer-direct")]
        [Authorize(Roles = "Organizer")] // Re-enabled authorization for production security
        public async Task<ActionResult<OrganizerBookingResponse>> CreateOrganizerDirectBooking([FromBody] OrganizerBookingRequest request)
        {
            try
            {
                _logger.LogInformation("🎯 ORGANIZER BOOKING - Starting validation for event {EventId} by {User}", request?.EventId ?? 0, User.Identity?.Name);
                
                // Debug log the full request
                _logger.LogInformation("🎯 REQUEST DEBUG - EventId: {EventId}, BuyerEmail: {Email}, FirstName: {FirstName}, TicketRequestsCount: {Count}", 
                    request?.EventId, request?.BuyerEmail, request?.FirstName, request?.TicketRequests?.Count ?? 0);

                // Validate request object
                if (request == null)
                {
                    _logger.LogWarning("🎯 VALIDATION FAILED - Request object is null");
                    return BadRequest("Request data is required");
                }

                // Validate request
                if (string.IsNullOrWhiteSpace(request.BuyerEmail))
                {
                    _logger.LogWarning("🎯 VALIDATION FAILED - BuyerEmail is empty: '{Email}'", request.BuyerEmail);
                    return BadRequest("BuyerEmail is required and cannot be empty");
                }

                if (string.IsNullOrWhiteSpace(request.FirstName))
                {
                    _logger.LogWarning("🎯 VALIDATION FAILED - FirstName is empty: '{FirstName}'", request.FirstName);
                    return BadRequest("FirstName is required and cannot be empty");
                }

                // Validate ticket requests
                if (request.TicketRequests == null || !request.TicketRequests.Any())
                {
                    _logger.LogWarning("🎯 VALIDATION FAILED - No ticket requests provided. TicketRequests is null: {IsNull}, Count: {Count}", 
                        request.TicketRequests == null, request.TicketRequests?.Count ?? 0);
                    return BadRequest("At least one ticket type must be specified");
                }

                // Validate that all ticket types exist for this event
                var ticketTypeIds = request.TicketRequests.Select(tr => tr.TicketTypeId).ToList();
                _logger.LogInformation("🎯 TICKET VALIDATION - Checking ticket types {TicketTypeIds} for event {EventId}", 
                    string.Join(",", ticketTypeIds), request.EventId);
                
                var validTicketTypes = await _context.TicketTypes
                    .Where(tt => tt.EventId == request.EventId && ticketTypeIds.Contains(tt.Id))
                    .ToListAsync();

                _logger.LogInformation("🎯 TICKET VALIDATION - Found {ValidCount} out of {RequestedCount} ticket types. Valid IDs: [{ValidIds}]", 
                    validTicketTypes.Count, ticketTypeIds.Count, string.Join(",", validTicketTypes.Select(tt => tt.Id)));

                if (validTicketTypes.Count != ticketTypeIds.Count)
                {
                    var invalidIds = ticketTypeIds.Except(validTicketTypes.Select(tt => tt.Id)).ToList();
                    _logger.LogWarning("🎯 VALIDATION FAILED - Invalid ticket type IDs: [{InvalidIds}] for event {EventId}", 
                        string.Join(",", invalidIds), request.EventId);
                    return BadRequest($"One or more ticket types are invalid for this event. Invalid IDs: {string.Join(",", invalidIds)}");
                }

                // Calculate total tickets requested
                var totalTicketsRequested = request.TicketRequests.Sum(tr => tr.Quantity);
                
                // For backward compatibility, if seat numbers are provided, ensure they match ticket quantity
                if (request.SeatNumbers.Any() && request.SeatNumbers.Count != totalTicketsRequested)
                {
                    return BadRequest($"Seat numbers count ({request.SeatNumbers.Count}) must match total ticket quantity ({totalTicketsRequested})");
                }

                // Get the event with organizer information
                var eventItem = await _context.Events
                    .Include(e => e.Organizer)
                    .FirstOrDefaultAsync(e => e.Id == request.EventId);
                if (eventItem == null)
                {
                    return BadRequest("Event not found");
                }

                // Generate a dummy payment GUID for organizer bookings
                var paymentGuid = $"ORG_{Guid.NewGuid():N}";

                // Create the main booking record
                var booking = new Booking
                {
                    EventId = request.EventId,
                    CustomerEmail = request.BuyerEmail,
                    CustomerFirstName = request.FirstName,
                    CustomerLastName = request.LastName ?? "",
                    CustomerMobile = request.Mobile ?? "",
                    PaymentIntentId = paymentGuid,
                    TotalAmount = 0, // Organizer bookings are free
                    ProcessingFee = 0,
                    Currency = "NZD",
                    PaymentStatus = "OrganizerDirect", // Special status for organizer bookings
                    Status = "Active",
                    CreatedAt = DateTime.UtcNow,
                    Metadata = JsonSerializer.Serialize(new { 
                        OrganizerBooking = true,
                        CreatedBy = User.Identity?.Name,
                        Seats = request.SeatNumbers 
                    })
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync(); // Save to get the booking ID

                // Mark the seats as booked in the Seats table (if seat numbers provided)
                if (request.SeatNumbers.Any())
                {
                    var seats = await _context.Seats
                        .Where(s => s.EventId == request.EventId && request.SeatNumbers.Contains(s.SeatNumber))
                        .ToListAsync();

                    foreach (var seat in seats)
                    {
                        seat.Status = SeatStatus.Booked;
                        seat.ReservedBy = request.BuyerEmail;
                        seat.ReservedUntil = DateTime.UtcNow.AddDays(365); // Long expiry for organizer bookings
                    }

                    await _context.SaveChangesAsync(); // Save seat updates
                }

                // Update booking metadata to include ticket type breakdown
                booking.Metadata = JsonSerializer.Serialize(new { 
                    OrganizerBooking = true,
                    CreatedBy = User.Identity?.Name,
                    TicketBreakdown = request.TicketRequests.Select(tr => new {
                        TicketTypeId = tr.TicketTypeId,
                        TicketTypeName = tr.TicketTypeName,
                        Quantity = tr.Quantity
                    }).ToList(),
                    SeatNumbers = request.SeatNumbers 
                });

                // Create consolidated booking line items per ticket type (matching user booking architecture)
                var lineItems = new List<BookingLineItem>();

                foreach (var ticketRequest in request.TicketRequests)
                {
                    var ticketType = validTicketTypes.First(tt => tt.Id == ticketRequest.TicketTypeId);
                    
                    // Collect allocated seats/tickets for this ticket type
                    var allocatedSeats = new List<string>();
                    var allocatedTickets = new List<string>();
                    
                    // Determine seat allocation based on event type and available seat numbers
                    var seatsForThisType = request.SeatNumbers.Skip(allocatedSeats.Count + allocatedTickets.Count).Take(ticketRequest.Quantity).ToList();
                    
                    for (int i = 0; i < ticketRequest.Quantity; i++)
                    {
                        string identifier;
                        if (seatsForThisType.Any() && i < seatsForThisType.Count)
                        {
                            var seatNumber = seatsForThisType[i];
                            // Check if this is a hardcoded general admission seat (A1, A2, A3, etc.)
                            if (seatNumber.StartsWith("A") && seatNumber.Length <= 3 && 
                                int.TryParse(seatNumber.Substring(1), out var sequentialNumber))
                            {
                                // Use ticket type prefix for general admission
                                identifier = $"{ticketType.Type}-{i + 1}";
                            }
                            else
                            {
                                identifier = seatNumber;
                            }
                        }
                        else
                        {
                            // Generate ticket-type based identifier
                            identifier = $"{ticketType.Type}-{i + 1}";
                        }

                        // Add to appropriate collection based on event type
                        if (eventItem.SeatSelectionMode == SeatSelectionMode.EventHall || 
                            (eventItem.SeatSelectionMode == SeatSelectionMode.Hybrid && !identifier.StartsWith(ticketType.Type)))
                        {
                            allocatedSeats.Add(identifier);
                        }
                        else
                        {
                            allocatedTickets.Add(identifier);
                        }
                    }

                    // Create single consolidated line item per ticket type
                    var lineItem = new BookingLineItem
                    {
                        BookingId = booking.Id,
                        ItemType = "Ticket",
                        ItemId = ticketType.Id,
                        ItemName = $"{ticketType.Name ?? ticketType.Type}",
                        Quantity = ticketRequest.Quantity, // Consolidated quantity
                        UnitPrice = 0, // Organizer tickets are free
                        TotalPrice = 0,
                        SeatDetails = JsonSerializer.Serialize(new 
                        {
                            ticketTypeId = ticketType.Id,
                            type = ticketType.Type,
                            color = ticketType.Color,
                            eventSeatMode = eventItem.SeatSelectionMode.ToString(),
                            organizerBooking = true,
                            // Unified JSON structure with conditional arrays
                            allocatedSeats = allocatedSeats.Any() ? allocatedSeats.ToArray() : null,
                            allocatedTickets = allocatedTickets.Any() ? allocatedTickets.ToArray() : null
                        }),
                        ItemDetails = $"Organizer issued {ticketRequest.Quantity}x {ticketType.Name ?? ticketType.Type} for {eventItem.Title}",
                        QRCode = "", // Will be generated by frontend
                        Status = "Active",
                        CreatedAt = DateTime.UtcNow
                    };
                    lineItems.Add(lineItem);
                }

                _context.BookingLineItems.AddRange(lineItems);
                await _context.SaveChangesAsync();

                // Convert relative image URL to full URL for external services (before loop)
                var fullImageUrl = GetFullImageUrl(eventItem.ImageUrl);

                // Generate QR codes and tickets for each individual seat/ticket from consolidated line items
                var ticketDetails = new List<TicketDetail>();
                var ticketPaths = new List<string>();

                foreach (var lineItem in lineItems)
                {
                    try
                    {
                        // Parse the SeatDetails JSON to extract seat/ticket information
                        var seatDetailsObj = JsonSerializer.Deserialize<JsonElement>(lineItem.SeatDetails);
                        var ticketType = seatDetailsObj.GetProperty("type").GetString() ?? "Organizer";
                        
                        // Get allocated seats or tickets
                        var allocatedItems = new List<string>();
                        
                        if (seatDetailsObj.TryGetProperty("allocatedSeats", out var allocatedSeatsProp) && 
                            allocatedSeatsProp.ValueKind == JsonValueKind.Array)
                        {
                            allocatedItems.AddRange(allocatedSeatsProp.EnumerateArray().Select(s => s.GetString() ?? "General"));
                        }
                        
                        if (seatDetailsObj.TryGetProperty("allocatedTickets", out var allocatedTicketsProp) && 
                            allocatedTicketsProp.ValueKind == JsonValueKind.Array)
                        {
                            allocatedItems.AddRange(allocatedTicketsProp.EnumerateArray().Select(t => t.GetString() ?? "General"));
                        }
                        
                        // Generate individual tickets for each allocated seat/ticket
                        foreach (var allocatedItem in allocatedItems)
                        {
                            // Generate QR code for this individual seat/ticket
                            var qrCode = _qrTicketService.GenerateQrCode(
                                eventItem.Id.ToString(),
                                eventItem.Title,
                                allocatedItem,
                                request.FirstName,
                                paymentGuid
                            );

                            // Generate ticket PDF using professional concert template
                            var ticketPdf = await _qrTicketService.GenerateProfessionalConcertTicketAsync(
                                eventItem.Id.ToString(),
                                eventItem.Title,
                                allocatedItem,
                                request.FirstName,
                                qrCode,
                                new List<FoodOrderInfo>(), // Empty food orders for organizer bookings
                                fullImageUrl, // Add event flyer with full URL
                                ticketType,
                                $"B{booking.Id}", // Include booking ID in the ticket reference
                                true // ✅ This IS an organizer booking - show "ORGANIZER GUEST"
                            );

                            // Save ticket locally and get the path
                            var ticketPath = _qrTicketService.SaveTicketLocally(
                                ticketPdf,
                                eventItem.Id.ToString(),
                                eventItem.Title,
                                request.FirstName,
                                paymentGuid,
                                allocatedItem
                            );

                            ticketDetails.Add(new TicketDetail
                            {
                                SeatNumber = allocatedItem,
                                TicketPath = ticketPath,
                                LineItemId = lineItem.Id
                            });

                            ticketPaths.Add(ticketPath);
                        }

                        // Update the line item with QR identifier (consolidated identifier for the group)
                        var qrIdentifier = $"QR_{booking.Id}_{lineItem.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}";
                        lineItem.QRCode = qrIdentifier;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to generate QR codes for consolidated line item {LineItemId}", lineItem.Id);
                        // Continue with other line items even if one fails
                        ticketDetails.Add(new TicketDetail
                        {
                            SeatNumber = "Error",
                            TicketPath = "",
                            LineItemId = lineItem.Id
                        });
                    }
                }

                // Update line items with QR codes
                await _context.SaveChangesAsync();

                // Send email with tickets to the buyer
                try
                {
                    if (ticketPaths.Any())
                    {
                        var emailsSent = 0;
                        var emailsFailed = 0;
                        
                        // Send separate email for each individual ticket
                        for (int i = 0; i < ticketPaths.Count; i++)
                        {
                            var ticketPath = ticketPaths[i];
                            var ticketDetail = ticketDetails[i];
                            
                            if (System.IO.File.Exists(ticketPath))
                            {
                                var ticketPdf = await System.IO.File.ReadAllBytesAsync(ticketPath);
                                
                                // Generate QR code for this specific ticket
                                var qrCodeImage = _qrTicketService.GenerateQrCode(
                                    eventItem.Id.ToString(),
                                    eventItem.Title,
                                    ticketDetail.SeatNumber,
                                    request.FirstName,
                                    paymentGuid
                                );
                                
                                var emailSent = await _emailService.SendEnhancedTicketEmailAsync(
                                    request.BuyerEmail,
                                    eventItem.Title,
                                    request.FirstName,
                                    ticketPdf, // Single ticket PDF per email
                                    new List<FoodOrderInfo>(), // Empty food orders for organizer bookings
                                    fullImageUrl, // Include event flyer in email with full URL
                                    qrCodeImage, // QR code for this specific ticket
                                    booking.Id.ToString(), // Booking ID for reference
                                    ticketDetail.SeatNumber, // seat or ticket number
                                    null, // ticket type (not available in controller)
                                    eventItem.Date, // event date
                                    eventItem.Location // event location
                                );

                                if (emailSent)
                                {
                                    emailsSent++;
                                    _logger.LogInformation("Successfully sent ticket email for booking {BookingId} seat {SeatNumber} to {Email}", booking.Id, ticketDetail.SeatNumber, request.BuyerEmail);
                                }
                                else
                                {
                                    emailsFailed++;
                                    _logger.LogWarning("Failed to send ticket email for booking {BookingId} seat {SeatNumber} to {Email}", booking.Id, ticketDetail.SeatNumber, request.BuyerEmail);
                                }
                            }
                            else
                            {
                                emailsFailed++;
                                _logger.LogWarning("Ticket file not found for booking {BookingId} at path: {Path}", booking.Id, ticketPath);
                            }
                        }
                        
                        _logger.LogInformation("Completed buyer email sending for booking {BookingId}: {Sent} sent, {Failed} failed out of {Total} tickets", 
                            booking.Id, emailsSent, emailsFailed, ticketPaths.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send ticket email for organizer booking {BookingId}", booking.Id);
                    // Don't fail the entire request if email fails
                }

                // Send organizer notification email
                try
                {
                    if (eventItem.Organizer != null && !string.IsNullOrWhiteSpace(eventItem.Organizer.ContactEmail))
                    {
                        if (ticketPaths.Any())
                        {
                            var emailsSent = 0;
                            var emailsFailed = 0;
                            
                            // Send separate notification for each individual ticket
                            for (int i = 0; i < ticketPaths.Count; i++)
                            {
                                var ticketPath = ticketPaths[i];
                                var ticketDetail = ticketDetails[i];
                                
                                if (System.IO.File.Exists(ticketPath))
                                {
                                    var ticketPdf = await System.IO.File.ReadAllBytesAsync(ticketPath);
                                    
                                    var organizerEmailSent = await _emailService.SendOrganizerNotificationAsync(
                                        eventItem.Organizer.ContactEmail,
                                        eventItem.Title,
                                        request.FirstName,
                                        request.BuyerEmail,
                                        ticketPdf, // Single ticket PDF per email
                                        new List<FoodOrderInfo>(), // Empty food orders for organizer bookings
                                        fullImageUrl // Include event flyer
                                    );

                                    if (organizerEmailSent)
                                    {
                                        emailsSent++;
                                        _logger.LogInformation("Successfully sent organizer notification for booking {BookingId} seat {SeatNumber} to {OrganizerEmail}", booking.Id, ticketDetail.SeatNumber, eventItem.Organizer.ContactEmail);
                                    }
                                    else
                                    {
                                        emailsFailed++;
                                        _logger.LogWarning("Failed to send organizer notification for booking {BookingId} seat {SeatNumber} to {OrganizerEmail}", booking.Id, ticketDetail.SeatNumber, eventItem.Organizer.ContactEmail);
                                    }
                                }
                                else
                                {
                                    emailsFailed++;
                                    _logger.LogWarning("Ticket file not found for organizer notification (booking {BookingId}): {Path}", booking.Id, ticketPath);
                                }
                            }
                            
                            _logger.LogInformation("Completed organizer email sending for booking {BookingId}: {Sent} sent, {Failed} failed out of {Total} tickets", 
                                booking.Id, emailsSent, emailsFailed, ticketPaths.Count);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No organizer contact email found for event {EventId}", eventItem.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send organizer notification email for organizer booking {BookingId}", booking.Id);
                    // Don't fail the entire request if organizer email fails
                }

                _logger.LogInformation("Created organizer booking {BookingId} with {SeatCount} seats and generated QR codes", booking.Id, request.SeatNumbers.Count);

                return Ok(new OrganizerBookingResponse
                {
                    BookingId = booking.Id,
                    PaymentGUID = paymentGuid,
                    Message = $"Organizer booking created successfully with {ticketDetails.Count} tickets across {request.TicketRequests.Count} ticket types. Email sent to buyer and organizer notification sent.",
                    EventName = eventItem.Title,
                    SeatNumbers = ticketDetails.Select(td => td.SeatNumber).ToList(),
                    TicketDetails = ticketDetails,
                    TicketBreakdown = request.TicketRequests.Select(tr => new {
                        TicketTypeId = tr.TicketTypeId,
                        TicketTypeName = tr.TicketTypeName,
                        Quantity = tr.Quantity
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating organizer booking for event {EventId}", request.EventId);
                return StatusCode(500, new { message = "Error creating organizer booking" });
            }
        }

        // Helper method to convert relative image URLs to full URLs using configuration
        private string? GetFullImageUrl(string? relativeUrl)
        {
            if (string.IsNullOrEmpty(relativeUrl))
                return null;

            // If it's already a full URL, return as is
            if (relativeUrl.StartsWith("http://") || relativeUrl.StartsWith("https://"))
                return relativeUrl;

            // Use configured base URL for consistent environment-specific URLs
            var baseUrl = _configuration["ApplicationSettings:BaseUrl"] 
                         ?? _configuration["QRTickets:BaseUrl"] 
                         ?? $"{Request.Scheme}://{Request.Host}"; // Fallback to request-based

            var fullUrl = $"{baseUrl.TrimEnd('/')}{(relativeUrl.StartsWith("/") ? relativeUrl : "/" + relativeUrl)}";
            
            _logger.LogDebug("Converted relative URL '{RelativeUrl}' to full URL '{FullUrl}' using base '{BaseUrl}'", 
                relativeUrl, fullUrl, baseUrl);
            return fullUrl;
        }
    }

    #region DTOs
    public class BookingListDTO
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal ProcessingFee { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int ItemCount { get; set; }
        public int TicketCount { get; set; }
        public int FoodCount { get; set; }
    }

    public class BookingDetailDTO
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public DateTime? EventDate { get; set; }
        public string EventLocation { get; set; } = string.Empty;
        public string? OrganizerName { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerFirstName { get; set; } = string.Empty;
        public string CustomerLastName { get; set; } = string.Empty;
        public string CustomerMobile { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal ProcessingFee { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string PaymentIntentId { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Metadata { get; set; } = string.Empty;
        public List<BookingLineItemDTO> LineItems { get; set; } = new();
    }

    public class BookingLineItemDTO
    {
        public int Id { get; set; }
        public string ItemType { get; set; } = string.Empty;
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string SeatDetails { get; set; } = string.Empty;
        public string ItemDetails { get; set; } = string.Empty;
        public string QRCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class UpdateBookingStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class RefundRequest
    {
        public string Reason { get; set; } = string.Empty;
        public decimal? Amount { get; set; } // Partial refund amount, null for full refund
    }

    public class OrganizerBookingRequest
    {
        public int EventId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string? LastName { get; set; }
        public string BuyerEmail { get; set; } = string.Empty;
        public string? Mobile { get; set; }
        public List<string> SeatNumbers { get; set; } = new List<string>();
        public List<OrganizerTicketRequest> TicketRequests { get; set; } = new List<OrganizerTicketRequest>();
    }

    public class OrganizerTicketRequest
    {
        public int TicketTypeId { get; set; }
        public int Quantity { get; set; }
        public string? TicketTypeName { get; set; } // For display purposes
    }

    public class OrganizerBookingResponse
    {
        public int BookingId { get; set; }
        public string PaymentGUID { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        public List<string> SeatNumbers { get; set; } = new List<string>();
        public List<TicketDetail> TicketDetails { get; set; } = new List<TicketDetail>();
        public object TicketBreakdown { get; set; } = new();
    }

    public class TicketDetail
    {
        public string SeatNumber { get; set; } = string.Empty;
        public string TicketPath { get; set; } = string.Empty;
        public int LineItemId { get; set; }
    }
    #endregion
}
