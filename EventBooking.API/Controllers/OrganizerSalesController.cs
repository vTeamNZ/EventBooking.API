using EventBooking.API.Data;
using EventBooking.API.DTOs;
using EventBooking.API.Models;
using EventBooking.API.Services;
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
        private readonly IOrganizerStripeService _organizerStripeService;
        private readonly IOrganizerSalesManagementService _salesManagementService;
        private readonly IEventStatusService _eventStatusService;

        public OrganizerSalesController(
            AppDbContext context,
            ILogger<OrganizerSalesController> logger,
            UserManager<ApplicationUser> userManager,
            IOrganizerStripeService organizerStripeService,
            IOrganizerSalesManagementService salesManagementService,
            IEventStatusService eventStatusService)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
            _organizerStripeService = organizerStripeService;
            _salesManagementService = salesManagementService;
            _eventStatusService = eventStatusService;
        }

        /// <summary>
        /// Helper method to convert UTC DateTime to New Zealand timezone
        /// Automatically handles NZST/NZDT (daylight saving) transitions
        /// </summary>
        private DateTime ConvertToNzTime(DateTime utcDateTime)
        {
            // Convert the provided UTC datetime to NZ timezone using the same logic as EventStatusService
            try
            {
                // Try Windows timezone ID first
                var nzTimeZone = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
                return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, nzTimeZone);
            }
            catch
            {
                try
                {
                    // Fallback for Linux/Mac systems
                    var nzTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");
                    return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, nzTimeZone);
                }
                catch
                {
                    // Ultimate fallback - return UTC if timezone conversion fails
                    return utcDateTime;
                }
            }
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
                var eventItem = await _context.Events
                    .FirstOrDefaultAsync(e => e.Id == eventId && e.OrganizerId == organizer.Id);

                if (eventItem == null)
                {
                    return NotFound(new { message = "Event not found or you don't have permission to view it." });
                }

                _logger.LogInformation("GetEventBookings called with eventId: {EventId}, page: {Page}, pageSize: {PageSize}, search: '{Search}', paymentStatus: '{PaymentStatus}'", 
                    eventId, page, pageSize, search ?? "null", paymentStatus ?? "null");

                List<BookingDetailViewDTO> bookings;
                int totalCount;

                // Route to appropriate data source based on payment status
                if (!string.IsNullOrEmpty(paymentStatus))
                {
                    _logger.LogInformation("Using FILTERED mode for paymentStatus: {PaymentStatus}", paymentStatus);
                    var statusLower = paymentStatus.ToLower().Trim();
                    
                    
                    if (statusLower == "succeeded" || statusLower == "paid" || statusLower == "paid only" || statusLower == "completed")
                    {
                        // PAID ONLY: Fetch from optimized Stripe service (single API call) with correct total count
                        var (stripeBookings, stripeTotalCount) = await _organizerStripeService.GetStripeBookingsWithCountAsync(eventId, page, pageSize, search);
                        bookings = stripeBookings;
                        totalCount = stripeTotalCount;
                    }
                    else if (statusLower == "organizerdirect" || statusLower == "organizer direct" || statusLower == "organizer guests" || statusLower == "organizer")
                    {
                        // ORGANIZER GUESTS: Fetch from OrganizerTicketPayments table
                        _logger.LogInformation("Fetching ORGANIZER GUESTS bookings from OrganizerTicketPayments for event {EventId}", eventId);
                        var organizerResult = await GetOrganizerBasedBookings(eventId, page, pageSize, search);
                        bookings = organizerResult.Bookings;
                        totalCount = organizerResult.TotalCount;
                    }
                    else
                    {
                        // Unknown status - return empty
                        _logger.LogWarning("Unknown payment status filter: {PaymentStatus}", paymentStatus);
                        bookings = new List<BookingDetailViewDTO>();
                        totalCount = 0;
                    }
                }
                else
                {
                    // ALL: Combine both sources using optimized Stripe service + reserved seats
                    _logger.LogInformation("Using COMBINED mode - fetching Stripe, Organizer bookings, and reserved seats for event {EventId}", eventId);
                    var stripeBookings = await _organizerStripeService.GetStripeBookingsAsync(eventId, 1, int.MaxValue, search);
                    _logger.LogInformation("Retrieved {StripeCount} Stripe bookings", stripeBookings.Count);
                    
                    var organizerResult = await GetOrganizerBasedBookings(eventId, 1, int.MaxValue, search);
                    _logger.LogInformation("Retrieved {OrganizerCount} Organizer bookings", organizerResult.Bookings.Count);
                    
                    // Get reserved seats and map them to BookingDetailViewDTO format
                    var reservedSeatsBookings = await GetReservedSeatsAsBookings(eventId, search);
                    _logger.LogInformation("Retrieved {ReservedCount} reserved seats", reservedSeatsBookings.Count);
                    
                    var allBookings = stripeBookings
                        .Concat(organizerResult.Bookings)
                        .Concat(reservedSeatsBookings)
                        .OrderByDescending(b => b.BookedTime)
                        .ToList();
                    
                    _logger.LogInformation("Combined total: {TotalCount} bookings (including reserved seats)", allBookings.Count);
                    
                    // Add breakdown counts to response headers for frontend display
                    Response.Headers.Add("X-Stripe-Count", stripeBookings.Count.ToString());
                    Response.Headers.Add("X-Organizer-Count", organizerResult.Bookings.Count.ToString());
                    Response.Headers.Add("X-Reserved-Count", reservedSeatsBookings.Count.ToString());
                    
                    totalCount = allBookings.Count;
                    bookings = allBookings
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();
                }

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

        /// <summary>
        /// 🚀 REMOVED v2 - GetStripeBasedBookings() - REPLACED WITH OPTIMIZED SERVICE
        /// OLD PROBLEM: Multiple Stripe API calls with pagination (very slow)
        /// NEW SOLUTION: Single optimized API call via IOrganizerStripeService
        /// Performance improvement: 1 API call instead of N paginated calls
        /// </summary>

        /// <summary>
        /// Maps a Stripe Checkout Session to BookingDetailViewDTO
        /// </summary>
        private BookingDetailViewDTO MapStripeSessionToBookingDetail(Stripe.Checkout.Session session)
        {
            // Extract customer information from metadata
            session.Metadata.TryGetValue("customerFirstName", out var firstName);
            session.Metadata.TryGetValue("customerLastName", out var lastName);
            session.Metadata.TryGetValue("customerMobile", out var mobile);
            session.Metadata.TryGetValue("ticketDetails", out var ticketDetailsJson);
            session.Metadata.TryGetValue("selectedSeats", out var selectedSeats);

            var email = session.CustomerEmail ?? session.Metadata.GetValueOrDefault("customerEmail", "");

            // Parse ticket details
            var ticketDetails = new List<TicketTypeDetailDTO>();
            var totalTickets = 0;

            if (!string.IsNullOrEmpty(ticketDetailsJson))
            {
                try
                {
                    using var document = JsonDocument.Parse(ticketDetailsJson);
                    var ticketArray = document.RootElement;

                    if (ticketArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var ticketElement in ticketArray.EnumerateArray())
                        {
                            var typeName = ticketElement.TryGetProperty("Type", out var typeProperty) 
                                ? typeProperty.GetString() ?? "Unknown" 
                                : "Unknown";
                            
                            var quantity = ticketElement.TryGetProperty("Quantity", out var quantityProperty) 
                                ? quantityProperty.GetInt32() 
                                : 0;
                            
                            var unitPrice = ticketElement.TryGetProperty("UnitPrice", out var priceProperty) 
                                ? priceProperty.GetDecimal() 
                                : 0m;

                            totalTickets += quantity;

                            ticketDetails.Add(new TicketTypeDetailDTO
                            {
                                TicketTypeName = typeName,
                                Quantity = quantity,
                                UnitPrice = unitPrice,
                                SeatInfo = "" // Seats will be extracted separately
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse ticket details from Stripe session {SessionId}", session.Id);
                }
            }

            // Extract seat information from selectedSeats metadata
            var seatInfo = "";
            if (!string.IsNullOrEmpty(selectedSeats))
            {
                var seats = selectedSeats.Split(';', StringSplitOptions.RemoveEmptyEntries);
                seatInfo = string.Join(", ", seats);
            }

            // If we have seat info and ticket details, add it to the first ticket type
            if (!string.IsNullOrEmpty(seatInfo) && ticketDetails.Any())
            {
                ticketDetails[0].SeatInfo = seatInfo;
            }

            return new BookingDetailViewDTO
            {
                BookingId = 0, // Stripe sessions don't have booking IDs
                PaymentId = session.PaymentIntentId ?? session.Id,
                FirstName = firstName ?? "",
                LastName = lastName ?? "",
                Email = HashEmail(email), // Hash for privacy
                Mobile = mobile ?? "",
                BookedTime = ConvertToNzTime(session.Created),
                PaymentStatus = "succeeded",
                TotalAmount = (decimal)(session.AmountTotal ?? 0) / 100, // Convert from cents
                TotalTickets = totalTickets,
                TicketDetails = ticketDetails,
                IsPaid = true,
                IsOrganizerBooking = false
            };
        }

        /// <summary>
        /// Fetches organizer guest bookings from OrganizerTicketPayments table
        /// </summary>
        private async Task<(List<BookingDetailViewDTO> Bookings, int TotalCount)> GetOrganizerBasedBookings(
            int eventId, 
            int page, 
            int pageSize, 
            string? search)
        {
            try
            {
                var query = _context.OrganizerTicketPayments
                    .Include(otp => otp.TicketType)
                    .Where(otp => otp.EventId == eventId);

                // Apply search filter
                if (!string.IsNullOrEmpty(search))
                {
                    var searchLower = search.ToLower();
                    query = query.Where(otp => 
                        otp.CustomerFirstName.ToLower().Contains(searchLower) ||
                        (otp.CustomerLastName != null && otp.CustomerLastName.ToLower().Contains(searchLower)) ||
                        otp.CustomerEmail.ToLower().Contains(searchLower));
                }

                // Group by customer and booking line item to consolidate multiple tickets per booking
                var groupedPayments = await query
                    .GroupBy(otp => new { otp.BookingLineItemId, otp.CustomerEmail, otp.CustomerFirstName, otp.CustomerLastName })
                    .Select(g => new
                    {
                        BookingLineItemId = g.Key.BookingLineItemId,
                        CustomerFirstName = g.Key.CustomerFirstName,
                        CustomerLastName = g.Key.CustomerLastName,
                        CustomerEmail = g.Key.CustomerEmail,
                        CustomerMobile = g.Select(otp => otp.CustomerMobile).FirstOrDefault(),
                        CreatedAt = g.Min(otp => otp.CreatedAt),
                        TotalAmount = g.Sum(otp => otp.TicketPrice),
                        TotalTickets = g.Count(),
                        TicketDetails = g.Select(otp => new
                        {
                            TicketTypeName = otp.TicketType != null ? otp.TicketType.Name : "Unknown",
                            TicketPrice = otp.TicketPrice,
                            SeatDetails = otp.SeatDetails
                        }).ToList()
                    })
                    .ToListAsync();

                // Sort by CreatedAt descending (newest first) and then by BookingLineItemId descending for consistency
                groupedPayments = groupedPayments
                    .OrderByDescending(g => g.CreatedAt)
                    .ThenByDescending(g => g.BookingLineItemId)
                    .ToList();

                var totalCount = groupedPayments.Count;

                // Apply pagination
                var paginatedGroups = groupedPayments
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();
                // Map grouped bookings to DTOs with detailed seat information
                // Similar to Stripe bookings, show all tickets and their seats
                var bookings = paginatedGroups.Select(g => new BookingDetailViewDTO
                {
                    BookingId = g.BookingLineItemId,
                    PaymentId = $"ORG-{g.BookingLineItemId}",
                    FirstName = g.CustomerFirstName,
                    LastName = g.CustomerLastName ?? "",
                    Email = HashEmail(g.CustomerEmail),
                    Mobile = g.CustomerMobile ?? "",
                    BookedTime = ConvertToNzTime(g.CreatedAt),
                    PaymentStatus = "OrganizerDirect",
                    TotalAmount = g.TotalAmount,
                    TotalTickets = g.TotalTickets,
                    // Group tickets by type and show all seats for each type
                    TicketDetails = g.TicketDetails
                        .GroupBy(td => new { td.TicketTypeName, td.TicketPrice })
                        .Select(tdg => new TicketTypeDetailDTO
                        {
                            TicketTypeName = tdg.Key.TicketTypeName,
                            Quantity = tdg.Count(),
                            UnitPrice = tdg.Key.TicketPrice,
                            // Collect ALL seat numbers for this ticket type - similar to Stripe display
                            SeatInfo = string.Join(", ", tdg
                                .Where(t => !string.IsNullOrEmpty(t.SeatDetails))
                                .Select(t => ExtractSeatInfo(t.SeatDetails ?? ""))
                                .Where(s => !string.IsNullOrEmpty(s))
                                .OrderBy(s => s))
                        })
                        .ToList(),
                    IsPaid = false,
                    IsOrganizerBooking = true
                }).ToList();

                return (bookings, totalCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching organizer-based bookings for event {EventId}", eventId);
                return (new List<BookingDetailViewDTO>(), 0);
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

                // Handle short local parts differently
                if (localPart.Length <= 4)
                {
                    // For very short emails, mask with first char + asterisks
                    var firstChar = localPart.Substring(0, 1);
                    var maskChars = new string('*', Math.Max(3, localPart.Length - 1));
                    return $"{firstChar}{maskChars}{domainPart}";
                }

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

        // GET: organizer/events/{eventId}/reserved-seats
        [Authorize(Roles = "Organizer")]
        [HttpGet("events/{eventId}/reserved-seats")]
        public async Task<ActionResult<object>> GetEventReservedSeats(
            int eventId, 
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 50)
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

                // Verify the event belongs to this organizer
                var eventExists = await _context.Events
                    .AnyAsync(e => e.Id == eventId && e.OrganizerId == organizer.Id);

                if (!eventExists)
                {
                    return NotFound(new { message = "Event not found or you don't have permission to view it." });
                }

                // Get the event details for Stripe API calls
                var eventItem = await _context.Events.FindAsync(eventId);
                if (eventItem == null)
                {
                    return NotFound(new { message = "Event not found." });
                }

                // Step 1: Get all seat numbers from Stripe bookings (optimized single API call)
                var stripeSeatNumbers = await _organizerStripeService.GetStripeSeatNumbersAsync(eventId);

                // Step 2: Get all seat numbers from Organizer Guests
                var organizerSeatNumbers = await GetOrganizerSeatNumbers(eventId);
                _logger.LogInformation("Found {Count} seats in Organizer bookings", organizerSeatNumbers.Count);

                // Step 3: Combine both lists (remove duplicates)
                var bookedSeatNumbers = stripeSeatNumbers.Union(organizerSeatNumbers, StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
                _logger.LogInformation("Total unique booked seats to exclude: {Count}", bookedSeatNumbers.Count);

                // Step 4: Get all seats with Status = Booked (2)
                var reservedSeatsQuery = await _context.Seats
                    .Include(s => s.TicketType)
                    .Where(s => s.EventId == eventId && s.Status == SeatStatus.Booked)
                    .ToListAsync();

                _logger.LogInformation("Found {Count} seats with Status=Booked before filtering", reservedSeatsQuery.Count);

                // Step 5: Filter out seats that are in Paid or Organizer bookings
                var actualReservedSeats = reservedSeatsQuery
                    .Where(s => !bookedSeatNumbers.Contains(s.SeatNumber))
                    .ToList();

                _logger.LogInformation("Found {Count} truly reserved seats after excluding booked seats", actualReservedSeats.Count);

                // Map to DTO
                var allReservedSeats = actualReservedSeats
                    .Select(seat => new ReservedSeatViewDTO
                    {
                        SeatId = seat.Id,
                        SeatNumber = seat.SeatNumber,
                        Row = seat.Row,
                        Number = seat.Number,
                        TicketTypeName = seat.TicketType?.Name ?? "Unknown",
                        SeatPrice = seat.Price,
                        ReservedUntil = seat.ReservedUntil,
                        ReservedBy = seat.ReservedBy,
                        MarkedAsBookedTime = DateTime.UtcNow, // We don't track this, use current time
                        DaysSinceBooked = 0 // Unknown, default to 0
                    })
                    .OrderByDescending(s => s.ReservedUntil ?? DateTime.MinValue) // Most recently reserved first
                    .ThenBy(s => s.TicketTypeName)
                    .ThenBy(s => s.Row)
                    .ThenBy(s => s.Number)
                    .ToList();

                var totalCount = allReservedSeats.Count;
                _logger.LogInformation("Returning {Count} reserved seats after pagination", totalCount);

                // Apply pagination
                var paginatedSeats = allReservedSeats
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return Ok(new
                {
                    reservedSeats = paginatedSeats,
                    totalCount = totalCount,
                    page = page,
                    pageSize = pageSize,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reserved seats for event {EventId}", eventId);
                return StatusCode(500, new { message = "An error occurred while retrieving reserved seats" });
            }
        }

        /// <summary>
        /// 🚀 REMOVED v2 - GetStripeSeatNumbers() - REPLACED WITH OPTIMIZED SERVICE
        /// OLD PROBLEM: Multiple Stripe API calls with pagination (very slow)
        /// NEW SOLUTION: Single optimized API call via IOrganizerStripeService.GetStripeSeatNumbersAsync()
        /// Performance improvement: 1 API call instead of N paginated calls
        /// </summary>

        /// <summary>
        /// Gets all seat numbers from Organizer Guests bookings
        /// </summary>
        private async Task<List<string>> GetOrganizerSeatNumbers(int eventId)
        {
            var seatNumbers = new List<string>();

            try
            {
                // Query OrganizerTicketPayments for this event
                var organizerPayments = await _context.OrganizerTicketPayments
                    .Where(otp => otp.EventId == eventId && !string.IsNullOrEmpty(otp.SeatDetails))
                    .ToListAsync();

                // Extract seat numbers from SeatDetails JSON
                foreach (var payment in organizerPayments)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(payment.SeatDetails))
                        {
                            // Check if it's a plain seat number (not JSON)
                            if (!payment.SeatDetails.TrimStart().StartsWith("{") && 
                                !payment.SeatDetails.TrimStart().StartsWith("["))
                            {
                                // Plain seat number like "B342-1"
                                seatNumbers.Add(payment.SeatDetails.Trim());
                                continue;
                            }

                            // Parse as JSON
                            using var document = JsonDocument.Parse(payment.SeatDetails);
                            var root = document.RootElement;

                            // Handle allocatedSeats array format (multiple seats in one payment)
                            if (root.TryGetProperty("allocatedSeats", out var allocatedSeats) && 
                                allocatedSeats.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                var seats = allocatedSeats.EnumerateArray()
                                    .Select(s => s.GetString() ?? "")
                                    .Where(s => !string.IsNullOrEmpty(s))
                                    .ToList();
                                
                                seatNumbers.AddRange(seats);
                                continue;
                            }

                            // Handle allocatedTickets array format
                            if (root.TryGetProperty("allocatedTickets", out var allocatedTickets) && 
                                allocatedTickets.ValueKind == System.Text.Json.JsonValueKind.Array)
                            {
                                var tickets = allocatedTickets.EnumerateArray()
                                    .Select(t => t.GetString() ?? "")
                                    .Where(t => !string.IsNullOrEmpty(t))
                                    .ToList();
                                
                                seatNumbers.AddRange(tickets);
                                continue;
                            }

                            // Handle single seatNumber field
                            if (root.TryGetProperty("seatNumber", out var seatNumberProp))
                            {
                                var seatNumber = seatNumberProp.GetString();
                                if (!string.IsNullOrEmpty(seatNumber))
                                {
                                    seatNumbers.Add(seatNumber);
                                    continue;
                                }
                            }

                            // Handle row + number format (legacy)
                            if (root.TryGetProperty("row", out var rowProp) && 
                                root.TryGetProperty("number", out var numberProp))
                            {
                                var row = rowProp.GetString();
                                var number = numberProp.GetInt32();
                                if (!string.IsNullOrEmpty(row))
                                {
                                    seatNumbers.Add($"{row}{number}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse SeatDetails for OrganizerTicketPayment {Id}: {SeatDetails}", 
                            payment.Id, payment.SeatDetails);
                    }
                }

                _logger.LogInformation("Extracted {Count} seat numbers from {PaymentCount} organizer payments", 
                    seatNumbers.Count, organizerPayments.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting seat numbers from OrganizerTicketPayments for event {EventId}", eventId);
            }

            return seatNumbers;
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

        /// <summary>
        /// Maps reserved seats to BookingDetailViewDTO format for inclusion in "All Bookings" view
        /// Reserved seats have empty values for fields that don't apply (payment info, customer details)
        /// </summary>
        private async Task<List<BookingDetailViewDTO>> GetReservedSeatsAsBookings(int eventId, string? search = null)
        {
            try
            {
                // Step 1: Get all seat numbers from Stripe bookings (optimized single API call)
                var stripeSeatNumbers = await _organizerStripeService.GetStripeSeatNumbersAsync(eventId);

                // Step 2: Get all seat numbers from Organizer Guests
                var organizerSeatNumbers = await GetOrganizerSeatNumbers(eventId);

                // Step 3: Combine both lists (remove duplicates)
                var bookedSeatNumbers = stripeSeatNumbers.Union(organizerSeatNumbers, StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Step 4: Get all seats with Status = Booked (2)
                var reservedSeatsQuery = await _context.Seats
                    .Include(s => s.TicketType)
                    .Where(s => s.EventId == eventId && s.Status == SeatStatus.Booked)
                    .ToListAsync();

                // Step 5: Filter out seats that are in Paid or Organizer bookings
                var actualReservedSeats = reservedSeatsQuery
                    .Where(s => !bookedSeatNumbers.Contains(s.SeatNumber))
                    .ToList();

                // Step 6: Apply search filter if provided
                if (!string.IsNullOrEmpty(search))
                {
                    var searchLower = search.ToLower();
                    actualReservedSeats = actualReservedSeats
                        .Where(s => s.SeatNumber.ToLower().Contains(searchLower) ||
                                   (s.ReservedBy?.ToLower().Contains(searchLower) ?? false) ||
                                   (s.TicketType?.Name?.ToLower().Contains(searchLower) ?? false))
                        .ToList();
                }

                // Step 7: Map to BookingDetailViewDTO format
                var reservedBookings = actualReservedSeats.Select(seat => new BookingDetailViewDTO
                {
                    BookingId = -seat.Id, // Use negative seat ID to distinguish from real bookings
                    PaymentId = "RESERVED",
                    FirstName = "Reserved",
                    LastName = "Seat",
                    Email = seat.ReservedBy ?? "system@reserved.seat",
                    Mobile = "",
                    BookedTime = seat.ReservedUntil ?? DateTime.UtcNow.AddDays(-1), // Use ReservedUntil or default to yesterday
                    PaymentStatus = "Reserved",
                    TotalAmount = 0m, // Reserved seats have no payment
                    TotalTickets = 1,
                    TicketDetails = new List<TicketTypeDetailDTO>
                    {
                        new TicketTypeDetailDTO
                        {
                            TicketTypeName = seat.TicketType?.Name ?? "Unknown",
                            Quantity = 1,
                            UnitPrice = 0m, // Reserved seats show $0
                            SeatInfo = seat.SeatNumber
                        }
                    },
                    IsPaid = false,
                    IsOrganizerBooking = false // Reserved seats are neither Stripe nor Organizer bookings
                }).OrderByDescending(booking => booking.BookedTime).ToList();

                _logger.LogInformation("Mapped {Count} reserved seats to booking format", reservedBookings.Count);
                return reservedBookings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mapping reserved seats to booking format for event {EventId}", eventId);
                return new List<BookingDetailViewDTO>();
            }
        }

        // ================================================================
        // SALES MANAGEMENT ENDPOINTS (Simplified Version)
        // ================================================================

        /// <summary>
        /// Get tickets for sales management table
        /// </summary>
        /// <param name="eventId">The event ID to get tickets for</param>
        /// <returns>List of tickets for sales management</returns>
        [HttpGet("events/{eventId}/sales-management")]
        [Authorize(Roles = "Organizer")]
        public async Task<ActionResult<List<OrganizerTicketSalesDTO>>> GetTicketsForSalesManagement(int eventId)
        {
            try
            {
                _logger.LogInformation("Getting sales management tickets for event {EventId}", eventId);
                
                var tickets = await _salesManagementService.GetTicketsForSalesManagementAsync(eventId);
                
                return Ok(tickets);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access to event {EventId}", eventId);
                return Forbid("Access denied");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tickets for event {EventId}", eventId);
                return StatusCode(500, new SimpleOperationResponse 
                { 
                    Success = false, 
                    Message = "An error occurred while retrieving tickets" 
                });
            }
        }
        
        /// <summary>
        /// Update customer details for a ticket
        /// </summary>
        /// <param name="paymentId">The payment ID to update</param>
        /// <param name="request">The customer details to update</param>
        /// <returns>Success response</returns>
        [HttpPut("payments/{paymentId}/customer-details")]
        [Authorize(Roles = "Organizer")]
        public async Task<ActionResult<SimpleOperationResponse>> UpdateCustomerDetails(
            int paymentId, 
            [FromBody] UpdateCustomerDetailsRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage);
                    
                    return BadRequest(new SimpleOperationResponse
                    {
                        Success = false,
                        Message = $"Validation failed: {string.Join(", ", errors)}"
                    });
                }
                
                _logger.LogInformation("Updating customer details for payment {PaymentId}", paymentId);
                
                var success = await _salesManagementService.UpdateCustomerDetailsAsync(paymentId, request);
                
                return Ok(new SimpleOperationResponse
                {
                    Success = success,
                    Message = success ? "Customer details updated successfully" : "Failed to update customer details"
                });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Ticket payment {PaymentId} not found", paymentId);
                return NotFound(new SimpleOperationResponse
                {
                    Success = false,
                    Message = "Ticket not found"
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access to payment {PaymentId}", paymentId);
                return Forbid("Access denied");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer details for payment {PaymentId}", paymentId);
                return StatusCode(500, new SimpleOperationResponse
                {
                    Success = false,
                    Message = "An error occurred while updating customer details"
                });
            }
        }
        
        /// <summary>
        /// Toggle payment status for a ticket
        /// </summary>
        /// <param name="paymentId">The payment ID to update</param>
        /// <param name="request">The payment status toggle request</param>
        /// <returns>Success response</returns>
        [HttpPut("payments/{paymentId}/toggle-payment")]
        [Authorize(Roles = "Organizer")]
        public async Task<ActionResult<SimpleOperationResponse>> TogglePaymentStatus(
            int paymentId, 
            [FromBody] TogglePaymentRequest request)
        {
            try
            {
                _logger.LogInformation("Toggling payment status for payment {PaymentId} to {IsPaid}", paymentId, request.IsPaid);
                
                var success = await _salesManagementService.TogglePaymentStatusAsync(paymentId, request.IsPaid);
                
                return Ok(new SimpleOperationResponse
                {
                    Success = success,
                    Message = success 
                        ? $"Payment status updated to {(request.IsPaid ? "paid" : "unpaid")}" 
                        : "Failed to update payment status"
                });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Ticket payment {PaymentId} not found", paymentId);
                return NotFound(new SimpleOperationResponse
                {
                    Success = false,
                    Message = "Ticket not found"
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access to payment {PaymentId}", paymentId);
                return Forbid("Access denied");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling payment status for payment {PaymentId}", paymentId);
                return StatusCode(500, new SimpleOperationResponse
                {
                    Success = false,
                    Message = "An error occurred while updating payment status"
                });
            }
        }
        
        /// <summary>
        /// Cancel a ticket
        /// </summary>
        /// <param name="paymentId">The payment ID to cancel</param>
        /// <returns>Success response</returns>
        [HttpPut("payments/{paymentId}/cancel")]
        [Authorize(Roles = "Organizer")]
        public async Task<ActionResult<SimpleOperationResponse>> CancelTicket(int paymentId)
        {
            try
            {
                _logger.LogInformation("Cancelling ticket for payment {PaymentId}", paymentId);
                
                var success = await _salesManagementService.CancelTicketAsync(paymentId);
                
                return Ok(new SimpleOperationResponse
                {
                    Success = success,
                    Message = success ? "Ticket cancelled successfully" : "Failed to cancel ticket"
                });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Ticket payment {PaymentId} not found", paymentId);
                return NotFound(new SimpleOperationResponse
                {
                    Success = false,
                    Message = "Ticket not found"
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access to payment {PaymentId}", paymentId);
                return Forbid("Access denied");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling ticket for payment {PaymentId}", paymentId);
                return StatusCode(500, new SimpleOperationResponse
                {
                    Success = false,
                    Message = "An error occurred while cancelling the ticket"
                });
            }
        }
        
        /// <summary>
        /// Restore a cancelled ticket
        /// </summary>
        /// <param name="paymentId">The payment ID to restore</param>
        /// <returns>Success response</returns>
        [HttpPut("payments/{paymentId}/restore")]
        [Authorize(Roles = "Organizer")]
        public async Task<ActionResult<SimpleOperationResponse>> RestoreTicket(int paymentId)
        {
            try
            {
                _logger.LogInformation("Restoring ticket for payment {PaymentId}", paymentId);
                
                var success = await _salesManagementService.RestoreTicketAsync(paymentId);
                
                return Ok(new SimpleOperationResponse
                {
                    Success = success,
                    Message = success ? "Ticket restored successfully" : "Failed to restore ticket"
                });
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, "Ticket payment {PaymentId} not found", paymentId);
                return NotFound(new SimpleOperationResponse
                {
                    Success = false,
                    Message = "Ticket not found"
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Unauthorized access to payment {PaymentId}", paymentId);
                return Forbid("Access denied");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring ticket for payment {PaymentId}", paymentId);
                return StatusCode(500, new SimpleOperationResponse
                {
                    Success = false,
                    Message = "An error occurred while restoring the ticket"
                });
            }
        }
    }
}
