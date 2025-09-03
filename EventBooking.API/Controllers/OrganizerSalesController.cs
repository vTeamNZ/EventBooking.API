using EventBooking.API.Data;
using EventBooking.API.DTOs;
using EventBooking.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace EventBooking.API.Controllers
{
    [Route("organizer")]
    [ApiController]
    public class OrganizerSalesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrganizerSalesController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrganizerSalesController(
            AppDbContext context,
            ILogger<OrganizerSalesController> logger,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        // GET: organizer/test - Simple test endpoint without authorization
        [HttpGet("test")]
        public ActionResult<string> Test()
        {
            return Ok("OrganizerSalesController is working!");
        }

        // GET: organizer/events - Get all events for the organizer
        [HttpGet("events")]
        [Authorize(Roles = "Organizer")]
        public async Task<ActionResult<List<object>>> GetOrganizerEvents()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId == null)
                {
                    return BadRequest(new { message = "Authentication error. Please try logging in again." });
                }

                var organizer = await _context.Organizers
                    .FirstOrDefaultAsync(o => o.UserId == userId);

                if (organizer == null)
                {
                    return BadRequest(new { message = "No organizer profile found." });
                }

                var events = await _context.Events
                    .Where(e => e.OrganizerId == organizer.Id)
                    .Select(e => new
                    {
                        id = e.Id,
                        title = e.Title,
                        description = e.Description,
                        date = e.Date,
                        location = e.Location,
                        price = e.Price,
                        capacity = e.Capacity,
                        isActive = e.IsActive,
                        imageUrl = e.ImageUrl,
                        createdAt = e.Date // Using event date as created date for sorting
                    })
                    .OrderByDescending(e => e.createdAt)
                    .ToListAsync();

                return Ok(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving organizer events");
                return StatusCode(500, new { message = "An error occurred while retrieving events" });
            }
        }

        // GET: organizer/events/{eventId}/sales-detail
        [HttpGet("events/{eventId}/sales-detail")]
        public async Task<ActionResult<EventSalesDetailDTO>> GetEventSalesDetail(int eventId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId == null)
                {
                    return BadRequest(new { message = "Authentication error. Please try logging in again." });
                }

                var organizer = await _context.Organizers
                    .FirstOrDefaultAsync(o => o.UserId == userId);

                if (organizer == null)
                {
                    return BadRequest(new { message = "No organizer profile found." });
                }

                // Verify this event belongs to the organizer
                var eventInfo = await _context.Events
                    .Where(e => e.Id == eventId && e.OrganizerId == organizer.Id)
                    .Select(e => new
                    {
                        e.Id,
                        e.Title,
                        e.Date,
                        e.Location,
                        e.Status,
                        e.IsActive,
                        e.Capacity
                    })
                    .FirstOrDefaultAsync();

                if (eventInfo == null)
                {
                    return NotFound(new { message = "Event not found or you don't have permission to view it." });
                }

                var ticketSales = await GetEventSalesData(eventId);
                
                var totalGrossRevenue = ticketSales.Sum(ts => ts.GrossRevenue);
                var totalProcessingFees = 0m; // No longer calculating processing fees separately since TotalPrice includes everything

                var detail = new EventSalesDetailDTO
                {
                    EventId = eventInfo.Id,
                    EventTitle = eventInfo.Title,
                    EventDate = eventInfo.Date,
                    EventLocation = eventInfo.Location ?? string.Empty,
                    Status = GetEventStatusString(eventInfo.Status, eventInfo.IsActive),
                    TotalCapacity = eventInfo.Capacity ?? 0,
                    TotalTicketsSold = ticketSales.Sum(ts => ts.TicketsSold),
                    TotalGrossRevenue = totalGrossRevenue,
                    TotalProcessingFees = totalProcessingFees,
                    TotalNetRevenue = ticketSales.Sum(ts => ts.NetRevenue),
                    TicketSales = ticketSales
                };

                return Ok(detail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving event sales detail for event {EventId}", eventId);
                return StatusCode(500, new { message = "An error occurred while retrieving event sales data" });
            }
        }

        // GET: organizer/events/{eventId}/daily-analytics
        [Authorize(Roles = "Organizer")]
        [HttpGet("events/{eventId}/daily-analytics")]
        public async Task<ActionResult<List<DailyAnalyticsDTO>>> GetDailyAnalytics(int eventId, [FromQuery] int days = 30)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId == null)
                {
                    return BadRequest(new { message = "Authentication error. Please try logging in again." });
                }

                var organizer = await _context.Organizers
                    .FirstOrDefaultAsync(o => o.UserId == userId);

                if (organizer == null)
                {
                    return BadRequest(new { message = "No organizer profile found." });
                }

                // Verify this event belongs to the organizer
                var eventExists = await _context.Events
                    .AnyAsync(e => e.Id == eventId && e.OrganizerId == organizer.Id);

                if (!eventExists)
                {
                    return NotFound(new { message = "Event not found or you don't have permission to view it." });
                }

                var cutoffDate = DateTime.UtcNow.AddDays(-days);

                // Get daily analytics with separate paid and organizer booking counts
                var dailyAnalytics = await _context.Bookings
                    .Include(b => b.BookingLineItems)
                    .Where(b => b.EventId == eventId && b.CreatedAt >= cutoffDate)
                    .GroupBy(b => b.CreatedAt.Date)
                    .Select(g => new DailyAnalyticsDTO
                    {
                        Date = g.Key,
                        PaidTickets = g.Where(b => b.PaymentStatus == "succeeded" || b.PaymentStatus == "Completed")
                                       .SelectMany(b => b.BookingLineItems)
                                       .Where(bli => bli.ItemType == "Ticket")
                                       .Sum(bli => bli.Quantity),
                        OrganizerTickets = g.Where(b => b.PaymentStatus == "OrganizerDirect")
                                            .SelectMany(b => b.BookingLineItems)
                                            .Where(bli => bli.ItemType == "Ticket")
                                            .Sum(bli => bli.Quantity),
                        TotalRevenue = g.Where(b => b.PaymentStatus == "succeeded" || b.PaymentStatus == "Completed")
                                       .Sum(b => b.TotalAmount),
                        TotalAttendance = g.SelectMany(b => b.BookingLineItems)
                                          .Where(bli => bli.ItemType == "Ticket")
                                          .Sum(bli => bli.Quantity)
                    })
                    .OrderBy(da => da.Date)
                    .ToListAsync();

                return Ok(dailyAnalytics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving daily analytics for event {EventId}", eventId);
                return StatusCode(500, new { message = "An error occurred while retrieving daily analytics" });
            }
        }

        // GET: organizer/events/{eventId}/bookings
        [Authorize(Roles = "Organizer")]
        [HttpGet("events/{eventId}/bookings")]
        public async Task<ActionResult<List<BookingDetailViewDTO>>> GetEventBookings(
            int eventId, 
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null,
            [FromQuery] string? paymentStatus = null)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId == null)
                {
                    return BadRequest(new { message = "Authentication error. Please try logging in again." });
                }

                var organizer = await _context.Organizers
                    .FirstOrDefaultAsync(o => o.UserId == userId);

                if (organizer == null)
                {
                    return BadRequest(new { message = "No organizer profile found." });
                }

                // Verify this event belongs to the organizer
                var eventExists = await _context.Events
                    .AnyAsync(e => e.Id == eventId && e.OrganizerId == organizer.Id);

                if (!eventExists)
                {
                    return NotFound(new { message = "Event not found or you don't have permission to view it." });
                }

                var query = _context.Bookings
                    .Include(b => b.BookingLineItems)
                    .Where(b => b.EventId == eventId);

                _logger.LogInformation("GetEventBookings called with eventId: {EventId}, page: {Page}, pageSize: {PageSize}, search: '{Search}', paymentStatus: '{PaymentStatus}'", 
                    eventId, page, pageSize, search ?? "null", paymentStatus ?? "null");

                // Get initial count before filters
                var initialCount = await _context.Bookings.Where(b => b.EventId == eventId).CountAsync();
                _logger.LogInformation("Initial booking count for event {EventId}: {InitialCount}", eventId, initialCount);

                // Log distinct payment statuses for debugging
                var distinctPaymentStatuses = await _context.Bookings
                    .Where(b => b.EventId == eventId)
                    .Select(b => b.PaymentStatus)
                    .Distinct()
                    .ToListAsync();
                _logger.LogInformation("Distinct payment statuses for event {EventId}: {PaymentStatuses}", 
                    eventId, string.Join(", ", distinctPaymentStatuses));

                // Apply search filter
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(b => 
                        b.CustomerFirstName.Contains(search) ||
                        b.CustomerLastName.Contains(search) ||
                        b.CustomerEmail.Contains(search) ||
                        b.PaymentIntentId.Contains(search));
                    _logger.LogInformation("Applied search filter for: {Search}", search);
                }

                // Apply payment status filter
                if (!string.IsNullOrEmpty(paymentStatus))
                {
                    _logger.LogInformation("Filtering by payment status: {PaymentStatus}", paymentStatus);
                    
                    // Map frontend filter values to database values
                    switch (paymentStatus.ToLower().Trim())
                    {
                        case "succeeded":
                        case "paid":
                        case "paid only":
                        case "completed":
                            // Frontend sends various paid statuses - map to database values
                            query = query.Where(b => b.PaymentStatus == "succeeded" || b.PaymentStatus == "Completed");
                            _logger.LogInformation("Filtering for paid bookings (succeeded/Completed)");
                            break;
                        case "organizerdirect":
                        case "organizer direct":
                        case "organizer guests":
                        case "organizer":
                            query = query.Where(b => b.PaymentStatus == "OrganizerDirect");
                            _logger.LogInformation("Filtering for organizer direct bookings");
                            break;
                        case "all":
                        case "":
                            // Show all bookings - don't apply filter
                            _logger.LogInformation("Showing all bookings - no payment filter applied");
                            break;
                        default:
                            // Exact match for any other status
                            query = query.Where(b => b.PaymentStatus == paymentStatus);
                            _logger.LogInformation("Filtering for exact payment status: {PaymentStatus}", paymentStatus);
                            break;
                    }
                }

                var totalCount = await query.CountAsync();
                _logger.LogInformation("Total bookings after filters: {TotalCount}", totalCount);

                // First get the data without the seat info processing
                var bookingData = await query
                    .OrderByDescending(b => b.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(b => new
                    {
                        BookingId = b.Id,
                        PaymentId = b.PaymentIntentId,
                        FirstName = b.CustomerFirstName,
                        LastName = b.CustomerLastName,
                        Email = b.CustomerEmail,
                        Mobile = b.CustomerMobile ?? "",
                        BookedTime = b.CreatedAt,
                        PaymentStatus = b.PaymentStatus,
                        TotalAmount = b.TotalAmount,
                        TotalTickets = b.BookingLineItems.Where(bli => bli.ItemType == "Ticket").Sum(bli => bli.Quantity),
                        TicketDetails = b.BookingLineItems
                            .Where(bli => bli.ItemType == "Ticket")
                            .Select(bli => new
                            {
                                TicketTypeName = bli.ItemName,
                                Quantity = bli.Quantity,
                                UnitPrice = bli.UnitPrice,
                                SeatDetails = bli.SeatDetails
                            }).ToList(),
                        IsPaid = b.PaymentStatus == "succeeded" || b.PaymentStatus == "Completed",
                        IsOrganizerBooking = b.PaymentStatus == "OrganizerDirect"
                    })
                    .ToListAsync();

                // Process the seat info after retrieving from database
                var bookings = bookingData.Select(b => new BookingDetailViewDTO
                {
                    BookingId = b.BookingId,
                    PaymentId = b.PaymentId,
                    FirstName = b.FirstName,
                    LastName = b.LastName,
                    Email = HashEmail(b.Email), // Hash the email for privacy
                    Mobile = b.Mobile,
                    BookedTime = b.BookedTime,
                    PaymentStatus = b.PaymentStatus,
                    TotalAmount = b.TotalAmount,
                    TotalTickets = b.TotalTickets,
                    TicketDetails = b.TicketDetails.Select(td => new TicketTypeDetailDTO
                    {
                        TicketTypeName = td.TicketTypeName,
                        Quantity = td.Quantity,
                        UnitPrice = td.UnitPrice,
                        SeatInfo = ExtractSeatInfo(td.SeatDetails)
                    }).ToList(),
                    IsPaid = b.IsPaid,
                    IsOrganizerBooking = b.IsOrganizerBooking
                }).ToList();

                Response.Headers.Add("X-Total-Count", totalCount.ToString());
                Response.Headers.Add("X-Page", page.ToString());
                Response.Headers.Add("X-Page-Size", pageSize.ToString());

                return Ok(bookings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving bookings for event {EventId}", eventId);
                return StatusCode(500, new { message = "An error occurred while retrieving bookings" });
            }
        }

        // GET: organizer/events/{eventId}/ticket-breakdown
        [Authorize(Roles = "Organizer")]
        [HttpGet("events/{eventId}/ticket-breakdown")]
        public async Task<ActionResult<List<TicketTypeBreakdownDTO>>> GetEventTicketBreakdown(int eventId)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (userId == null)
                {
                    return BadRequest(new { message = "Authentication error. Please try logging in again." });
                }

                var organizer = await _context.Organizers
                    .FirstOrDefaultAsync(o => o.UserId == userId);

                if (organizer == null)
                {
                    return BadRequest(new { message = "No organizer profile found." });
                }

                // Verify this event belongs to the organizer
                var eventExists = await _context.Events
                    .AnyAsync(e => e.Id == eventId && e.OrganizerId == organizer.Id);

                if (!eventExists)
                {
                    return NotFound(new { message = "Event not found or you don't have permission to view it." });
                }

                // Get all ticket types from TicketTypes table first (for complete information)
                var ticketTypes = await _context.TicketTypes
                    .Where(tt => tt.EventId == eventId)
                    .ToListAsync();

                var breakdown = new List<TicketTypeBreakdownDTO>();

                foreach (var ticketType in ticketTypes)
                {
                    int paidTickets = 0;
                    int organizerTickets = 0;
                    decimal paidRevenue = 0;

                    // Check if we have BookingLineItems data (new system)
                    var lineItemTickets = await _context.BookingLineItems
                        .Include(bli => bli.Booking)
                        .Where(bli => bli.Booking.EventId == eventId && 
                               bli.ItemType == "Ticket" && 
                               bli.ItemId == ticketType.Id)
                        .SumAsync(bli => bli.Quantity);

                    if (lineItemTickets > 0)
                    {
                        // Use BookingLineItems data (current system)
                        
                        // Platform tickets (paid customers)
                        paidTickets = await _context.BookingLineItems
                            .Include(bli => bli.Booking)
                            .Where(bli => bli.Booking.EventId == eventId && 
                                   bli.ItemType == "Ticket" && 
                                   bli.ItemId == ticketType.Id &&
                                   (bli.Booking.PaymentStatus == "succeeded" || bli.Booking.PaymentStatus == "Completed"))
                            .SumAsync(bli => bli.Quantity);

                        // Organizer tickets (complimentary)
                        organizerTickets = await _context.BookingLineItems
                            .Include(bli => bli.Booking)
                            .Where(bli => bli.Booking.EventId == eventId && 
                                   bli.ItemType == "Ticket" && 
                                   bli.ItemId == ticketType.Id &&
                                   bli.Booking.PaymentStatus == "OrganizerDirect")
                            .SumAsync(bli => bli.Quantity);
                        
                        // Platform revenue (only from paid tickets)
                        paidRevenue = await _context.BookingLineItems
                            .Include(bli => bli.Booking)
                            .Where(bli => bli.Booking.EventId == eventId && 
                                   bli.ItemType == "Ticket" && 
                                   bli.ItemId == ticketType.Id &&
                                   (bli.Booking.PaymentStatus == "succeeded" || bli.Booking.PaymentStatus == "Completed"))
                            .SumAsync(bli => bli.TotalPrice);
                    }
                    else
                    {
                        // Fallback to PaymentRecords for backward compatibility
                        var allBookings = await _context.PaymentRecords
                            .Where(p => p.EventId == eventId && 
                                   (p.PaymentStatus == "Completed" || p.PaymentStatus == "OrganizerDirect"))
                            .ToListAsync();

                        foreach (var booking in allBookings)
                        {
                            var ticketDetails = booking.TicketDetails;
                            var ticketTypeName = ticketType.Name ?? ticketType.Type;
                            int typeCount = 0;
                            
                            // Handle both old and new ticket detail formats
                            if (ticketDetails.Contains("\"ticketTypeId\""))
                            {
                                // New format with ticketTypeId and quantity
                                try
                                {
                                    var tickets = JsonSerializer.Deserialize<List<dynamic>>(ticketDetails);
                                    foreach (var ticket in tickets)
                                    {
                                        var ticketJson = ticket.ToString();
                                        if (ticketJson?.Contains($"\"ticketTypeId\":{ticketType.Id}") == true)
                                        {
                                            var quantityMatch = System.Text.RegularExpressions.Regex.Match(ticketJson, @"""quantity"":(\d+)");
                                            if (quantityMatch.Success)
                                            {
                                                typeCount = int.Parse(quantityMatch.Groups[1].Value);
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Error parsing new format ticket details for event {EventId}", eventId);
                                }
                            }
                            else
                            {
                                // Old format - count individual ticket entries by ticket type name
                                if (ticketDetails.Contains(ticketTypeName))
                                {
                                    typeCount = ticketDetails.Split(new[] { ticketTypeName }, StringSplitOptions.None).Length - 1;
                                }
                                
                                // Also try with the Type field if Name didn't match
                                if (typeCount == 0 && ticketType.Type != ticketTypeName && ticketDetails.Contains(ticketType.Type))
                                {
                                    typeCount = ticketDetails.Split(new[] { ticketType.Type }, StringSplitOptions.None).Length - 1;
                                }
                            }

                            // Separate paid vs organizer tickets
                            if (booking.PaymentStatus == "Completed")
                            {
                                paidTickets += typeCount;
                                paidRevenue += typeCount * ticketType.Price;
                            }
                            else if (booking.PaymentStatus == "OrganizerDirect")
                            {
                                organizerTickets += typeCount;
                                // Organizer tickets generate no revenue
                            }
                        }
                    }

                    // Calculate totals
                    int totalTickets = paidTickets + organizerTickets;
                    decimal totalRevenue = paidRevenue; // Only paid tickets generate revenue

                    // Only add ticket types that have sales
                    if (totalTickets > 0)
                    {
                        breakdown.Add(new TicketTypeBreakdownDTO
                        {
                            TicketTypeId = ticketType.Id,
                            TicketTypeName = ticketType.Name ?? ticketType.Type,
                            TicketPrice = ticketType.Price,
                            PaidTickets = paidTickets,
                            OrganizerTickets = organizerTickets,
                            TotalTickets = totalTickets,
                            PaidRevenue = paidRevenue,
                            TotalRevenue = totalRevenue
                        });
                    }
                }

                return Ok(breakdown.OrderBy(b => b.TicketTypeName).ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ticket breakdown for event {EventId}", eventId);
                return StatusCode(500, new { message = "An error occurred while retrieving ticket breakdown" });
            }
        }

        private async Task<List<TicketTypeSalesDTO>> GetEventSalesData(int eventId)
        {
            // Get ticket sales data (only paid tickets for revenue)
            // Handle both "succeeded" (Stripe) and "Completed" (legacy) payment statuses
            // Use TotalPrice from BookingLineItems as the net revenue (processing fee is already included in ticket price)
            var ticketSales = await _context.BookingLineItems
                .Include(bli => bli.Booking)
                .Where(bli => bli.Booking.EventId == eventId 
                    && bli.ItemType == "Ticket" 
                    && (bli.Booking.PaymentStatus == "succeeded" || bli.Booking.PaymentStatus == "Completed"))
                .GroupBy(bli => new { bli.ItemId, bli.ItemName })
                .Select(g => new TicketTypeSalesDTO
                {
                    TicketTypeId = g.Key.ItemId,
                    TicketTypeName = g.Key.ItemName,
                    TicketPrice = g.Average(bli => bli.UnitPrice),
                    TicketsSold = g.Sum(bli => bli.Quantity),
                    GrossRevenue = g.Sum(bli => bli.TotalPrice),
                    NetRevenue = g.Sum(bli => bli.TotalPrice) // Simplified: just use total price from line items
                })
                .ToListAsync();

            return ticketSales;
        }

        private async Task<int> GetEventOrganizerTickets(int eventId)
        {
            // Get organizer ticket count (separate from revenue calculation)
            var organizerTickets = await _context.BookingLineItems
                .Include(bli => bli.Booking)
                .Where(bli => bli.Booking.EventId == eventId 
                    && bli.ItemType == "Ticket" 
                    && bli.Booking.PaymentStatus == "OrganizerDirect")
                .SumAsync(bli => bli.Quantity);

            return organizerTickets;
        }

        private static string ExtractSeatInfo(string seatDetails)
        {
            try
            {
                if (string.IsNullOrEmpty(seatDetails))
                    return "General";

                // First, check if it's just a plain seat number (not JSON)
                if (!seatDetails.TrimStart().StartsWith("{") && !seatDetails.TrimStart().StartsWith("["))
                {
                    // Plain seat number like "B342-1", "B342-2", etc.
                    return seatDetails.Trim();
                }

                using var document = System.Text.Json.JsonDocument.Parse(seatDetails);
                var root = document.RootElement;

                // Handle unified JSON structure - try different field patterns
                if (root.TryGetProperty("allocatedSeats", out var allocatedSeats) && 
                    allocatedSeats.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var seats = allocatedSeats.EnumerateArray()
                        .Select(s => s.GetString() ?? "")
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
                    
                    if (seats.Any())
                        return string.Join(", ", seats);
                }

                if (root.TryGetProperty("allocatedTickets", out var allocatedTickets) && 
                    allocatedTickets.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var tickets = allocatedTickets.EnumerateArray()
                        .Select(t => t.GetString() ?? "")
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();
                    
                    if (tickets.Any())
                        return string.Join(", ", tickets);
                }

                // Check for seatNumber field (newer format)
                if (root.TryGetProperty("seatNumber", out var seatNumber))
                {
                    return seatNumber.GetString() ?? "General";
                }

                // Check for type field (when no specific seat, just ticket type)
                if (root.TryGetProperty("type", out var ticketType))
                {
                    return ticketType.GetString() ?? "General";
                }

                // Check for ticketTypeName field (alternative format)
                if (root.TryGetProperty("ticketTypeName", out var ticketTypeName))
                {
                    return ticketTypeName.GetString() ?? "General";
                }

                // If it's a JSON object but doesn't match expected patterns, return "General"
                return "General";
            }
            catch
            {
                // If it's not valid JSON, it might be a plain seat number
                // Return the original string if it looks like a seat number
                if (!string.IsNullOrEmpty(seatDetails) && 
                    (seatDetails.Contains("-") || seatDetails.Length < 50))
                {
                    return seatDetails.Trim();
                }
                return "General";
            }
        }

        private static string HashEmail(string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                    return "";

                // Find the @ symbol
                var atIndex = email.IndexOf('@');
                if (atIndex <= 0)
                    return email; // Return original if no @ found or @ is at the beginning

                var localPart = email.Substring(0, atIndex);
                var domainPart = email.Substring(atIndex);

                // If local part is too short, just return the original
                if (localPart.Length <= 4)
                    return email;

                // Take first 2 and last 2 characters of local part
                var firstTwo = localPart.Substring(0, 2);
                var lastTwo = localPart.Substring(localPart.Length - 2);
                
                // Create asterisks for the middle part (minimum 5 asterisks)
                var middleLength = Math.Max(5, localPart.Length - 4);
                var asterisks = new string('*', middleLength);

                return $"{firstTwo}{asterisks}{lastTwo}{domainPart}";
            }
            catch
            {
                // Return original email if any error occurs
                return email;
            }
        }

        private static string GetEventStatusString(EventStatus? status, bool isActive)
        {
            if (status.HasValue)
            {
                return status.Value switch
                {
                    EventStatus.Draft => "Draft",
                    EventStatus.Pending => "Pending Approval",
                    EventStatus.Active => "Active",
                    EventStatus.Inactive => "Inactive",
                    _ => "Unknown"
                };
            }
            
            return isActive ? "Active" : "Inactive";
        }
    }
}
