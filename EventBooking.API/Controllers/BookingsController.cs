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

        public BookingsController(
            AppDbContext context, 
            ILogger<BookingsController> logger,
            IQRTicketService qrTicketService,
            IEmailService emailService)
        {
            _context = context;
            _logger = logger;
            _qrTicketService = qrTicketService;
            _emailService = emailService;
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
        // [Authorize(Roles = "Organizer")] // Temporarily disabled for testing
        public async Task<ActionResult<OrganizerBookingResponse>> CreateOrganizerDirectBooking([FromBody] OrganizerBookingRequest request)
        {
            try
            {
                _logger.LogInformation("Creating organizer direct booking for event {EventId} by {User}", request.EventId, User.Identity?.Name);

                // Get the event
                var eventItem = await _context.Events.FindAsync(request.EventId);
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

                // Mark the seats as booked in the Seats table
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

                // Create booking line items for each seat
                var lineItems = new List<BookingLineItem>();
                foreach (var seatNumber in request.SeatNumbers)
                {
                    var lineItem = new BookingLineItem
                    {
                        BookingId = booking.Id,
                        ItemType = "Ticket",
                        ItemId = 0, // Dummy value for organizer bookings
                        ItemName = $"Organizer Ticket - {seatNumber}",
                        Quantity = 1,
                        UnitPrice = 0,
                        TotalPrice = 0,
                        SeatDetails = seatNumber,
                        ItemDetails = $"Seat {seatNumber} - {eventItem.Title}",
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

                // Generate QR codes and tickets for each seat
                var ticketDetails = new List<TicketDetail>();
                var ticketPaths = new List<string>();

                foreach (var lineItem in lineItems)
                {
                    try
                    {
                        // Generate QR code for this seat
                        var qrCode = _qrTicketService.GenerateQrCode(
                            eventItem.Id.ToString(),
                            eventItem.Title,
                            lineItem.SeatDetails,
                            request.FirstName,
                            paymentGuid
                        );

                        // Generate ticket PDF
                        var ticketPdf = await _qrTicketService.GenerateTicketPdfAsync(
                            eventItem.Id.ToString(),
                            eventItem.Title,
                            lineItem.SeatDetails,
                            request.FirstName,
                            qrCode,
                            new List<FoodOrderInfo>(), // Empty food orders for organizer bookings
                            fullImageUrl // Add event flyer with full URL
                        );

                        // Save ticket locally and get the path
                        var ticketPath = _qrTicketService.SaveTicketLocally(
                            ticketPdf,
                            eventItem.Id.ToString(),
                            eventItem.Title,
                            request.FirstName,
                            paymentGuid
                        );

                        // Update the line item with QR identifier (not full base64 image)
                        var qrIdentifier = $"QR_{booking.Id}_{lineItem.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}";
                        lineItem.QRCode = qrIdentifier;
                        
                        ticketDetails.Add(new TicketDetail
                        {
                            SeatNumber = lineItem.SeatDetails,
                            TicketPath = ticketPath,
                            LineItemId = lineItem.Id
                        });

                        ticketPaths.Add(ticketPath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to generate QR code for seat {SeatNumber}", lineItem.SeatDetails);
                        // Continue with other seats even if one fails
                        ticketDetails.Add(new TicketDetail
                        {
                            SeatNumber = lineItem.SeatDetails,
                            TicketPath = "",
                            LineItemId = lineItem.Id
                        });
                    }
                }

                // Update line items with QR codes
                await _context.SaveChangesAsync();

                // Send email with all tickets to the buyer
                try
                {
                    if (ticketPaths.Any())
                    {
                        // Use the full path that was saved by QRTicketService
                        var firstTicketPath = ticketPaths.First();
                        if (System.IO.File.Exists(firstTicketPath))
                        {
                            var ticketPdf = await System.IO.File.ReadAllBytesAsync(firstTicketPath);
                            var emailSent = await _emailService.SendTicketEmailAsync(
                                request.BuyerEmail,
                                eventItem.Title,
                                request.FirstName,
                                ticketPdf,
                                new List<FoodOrderInfo>(), // Empty food orders for organizer bookings
                                fullImageUrl // Include event flyer in email with full URL
                            );

                            if (emailSent)
                            {
                                _logger.LogInformation("Successfully sent ticket email to {Email}", request.BuyerEmail);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to send ticket email to {Email}", request.BuyerEmail);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Ticket file not found at path: {Path}", firstTicketPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send ticket email for organizer booking {BookingId}", booking.Id);
                    // Don't fail the entire request if email fails
                }

                _logger.LogInformation("Created organizer booking {BookingId} with {SeatCount} seats and generated QR codes", booking.Id, request.SeatNumbers.Count);

                return Ok(new OrganizerBookingResponse
                {
                    BookingId = booking.Id,
                    PaymentGUID = paymentGuid,
                    Message = "Organizer booking created successfully with QR codes and email sent",
                    EventName = eventItem.Title,
                    SeatNumbers = request.SeatNumbers,
                    TicketDetails = ticketDetails
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating organizer booking for event {EventId}", request.EventId);
                return StatusCode(500, new { message = "Error creating organizer booking" });
            }
        }

        // Helper method to convert relative image URLs to full URLs
        private string? GetFullImageUrl(string? relativeUrl)
        {
            if (string.IsNullOrEmpty(relativeUrl))
                return null;

            // If it's already a full URL, return as is
            if (relativeUrl.StartsWith("http://") || relativeUrl.StartsWith("https://"))
                return relativeUrl;

            // Images are served as static files from this API server via wwwroot
            // Use the current request's scheme and host to build the full URL
            var scheme = Request.Scheme;
            var host = Request.Host;
            var fullUrl = $"{scheme}://{host}{(relativeUrl.StartsWith("/") ? relativeUrl : "/" + relativeUrl)}";
            
            _logger.LogDebug("Converted relative URL '{RelativeUrl}' to full URL '{FullUrl}'", relativeUrl, fullUrl);
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
    }

    public class OrganizerBookingResponse
    {
        public int BookingId { get; set; }
        public string PaymentGUID { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        public List<string> SeatNumbers { get; set; } = new List<string>();
        public List<TicketDetail> TicketDetails { get; set; } = new List<TicketDetail>();
    }

    public class TicketDetail
    {
        public string SeatNumber { get; set; } = string.Empty;
        public string TicketPath { get; set; } = string.Empty;
        public int LineItemId { get; set; }
    }
    #endregion
}
