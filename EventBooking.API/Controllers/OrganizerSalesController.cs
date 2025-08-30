using EventBooking.API.Data;
using EventBooking.API.DTOs;
using EventBooking.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EventBooking.API.Controllers
{
    [Authorize(Roles = "Organizer")]
    [Route("api/organizer")]
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

        // GET: api/organizer/dashboard/summary
        [HttpGet("dashboard/summary")]
        public async Task<ActionResult<OrganizerDashboardSummaryDTO>> GetDashboardSummary()
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
                    return BadRequest(new { message = "No organizer profile found. Please complete your organizer registration." });
                }

                // Get all events for this organizer
                var events = await _context.Events
                    .Where(e => e.OrganizerId == organizer.Id)
                    .Select(e => new
                    {
                        e.Id,
                        e.Title,
                        e.Date,
                        e.Status,
                        e.IsActive
                    })
                    .ToListAsync();

                var eventSummaries = new List<EventSalesSummaryDTO>();
                int totalTicketsSold = 0;
                decimal totalNetRevenue = 0;

                foreach (var eventInfo in events)
                {
                    var eventSales = await GetEventSalesData(eventInfo.Id);
                    
                    var eventSummary = new EventSalesSummaryDTO
                    {
                        EventId = eventInfo.Id,
                        EventTitle = eventInfo.Title,
                        EventDate = eventInfo.Date,
                        Status = GetEventStatusString(eventInfo.Status, eventInfo.IsActive),
                        TotalTicketsSold = eventSales.Sum(ts => ts.TicketsSold),
                        TotalNetRevenue = eventSales.Sum(ts => ts.NetRevenue),
                        TicketSales = eventSales
                    };

                    eventSummaries.Add(eventSummary);
                    totalTicketsSold += eventSummary.TotalTicketsSold;
                    totalNetRevenue += eventSummary.TotalNetRevenue;
                }

                var summary = new OrganizerDashboardSummaryDTO
                {
                    TotalEvents = events.Count,
                    TotalTicketsSold = totalTicketsSold,
                    TotalNetRevenue = totalNetRevenue,
                    Events = eventSummaries.OrderByDescending(e => e.EventDate).ToList()
                };

                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving organizer dashboard summary for user {UserId}", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                return StatusCode(500, new { message = "An error occurred while retrieving dashboard data" });
            }
        }

        // GET: api/organizer/events/{eventId}/sales-detail
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
                var totalProcessingFees = totalGrossRevenue - ticketSales.Sum(ts => ts.NetRevenue);

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

        private async Task<List<TicketTypeSalesDTO>> GetEventSalesData(int eventId)
        {
            // Get ticket sales data with net revenue calculation
            var ticketSales = await _context.BookingLineItems
                .Include(bli => bli.Booking)
                .Where(bli => bli.Booking.EventId == eventId 
                    && bli.ItemType == "Ticket" 
                    && bli.Booking.PaymentStatus == "succeeded")
                .GroupBy(bli => new { bli.ItemId, bli.ItemName })
                .Select(g => new TicketTypeSalesDTO
                {
                    TicketTypeId = g.Key.ItemId,
                    TicketTypeName = g.Key.ItemName,
                    TicketPrice = g.Average(bli => bli.UnitPrice),
                    TicketsSold = g.Sum(bli => bli.Quantity),
                    GrossRevenue = g.Sum(bli => bli.TotalPrice),
                    NetRevenue = g.Sum(bli => bli.TotalPrice) - g.Sum(bli => 
                        bli.Booking.ProcessingFee * (bli.TotalPrice / bli.Booking.TotalAmount))
                })
                .ToListAsync();

            return ticketSales;
        }

        private string GetEventStatusString(EventStatus? status, bool isActive)
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
