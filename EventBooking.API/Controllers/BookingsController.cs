using EventBooking.API.Data;
using EventBooking.API.Models;
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

        public BookingsController(AppDbContext context, ILogger<BookingsController> logger)
        {
            _context = context;
            _logger = logger;
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
    #endregion
}
