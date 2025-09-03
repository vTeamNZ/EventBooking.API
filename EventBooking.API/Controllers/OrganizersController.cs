using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventBooking.API.Data;
using EventBooking.API.Models;
using Microsoft.AspNetCore.Authorization;
using System.Collections;
using System.Security.Claims;

namespace EventBooking.API.Controllers
{
    [Authorize]
    [Route("[controller]")]
    [ApiController]
    public class OrganizersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OrganizersController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Organizers
        [AllowAnonymous]
        //[Authorize(Roles = "Admin,Organizer")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetOrganizers()
        {
            var list = await _context.Organizers.Include(o => o.User).ToListAsync();
            
            // Transform the list to avoid circular reference issues
            var result = list.Select(organizer => new
            {
                organizer.Id,
                organizer.Name,
                organizer.ContactEmail,
                organizer.PhoneNumber,
                organizer.OrganizationName,
                organizer.Website,
                organizer.FacebookUrl,
                organizer.YoutubeUrl,
                organizer.IsVerified,
                organizer.CreatedAt,
                UserName = organizer.User?.UserName,
                FullName = organizer.User?.FullName
            });
            
            return Ok(result);
        }

        // GET: api/Organizers/5
        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetOrganizer(int id)
        {
            var organizer = await _context.Organizers
                                          .Include(o => o.User)
                                          .FirstOrDefaultAsync(o => o.Id == id);

            if (organizer == null)
            {
                return NotFound();
            }

            // Return an anonymous object with only the needed properties to avoid circular reference issues
            return new
            {
                organizer.Id,
                organizer.Name,
                organizer.ContactEmail,
                organizer.PhoneNumber,
                organizer.OrganizationName,
                organizer.Website,
                organizer.FacebookUrl,
                organizer.YoutubeUrl,
                organizer.IsVerified,
                organizer.CreatedAt,
                UserName = organizer.User?.UserName,
                FullName = organizer.User?.FullName
            };
        }

        // PUT: api/Organizers/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [Authorize(Roles = "Admin,Organizer")] // ? SECURITY FIX: Only admins and organizers can update organizer info
        [HttpPut("{id}")]
        public async Task<IActionResult> PutOrganizer(int id, Organizer organizer)
        {
            if (id != organizer.Id)
            {
                return BadRequest();
            }

            _context.Entry(organizer).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!OrganizerExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Organizers
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [Authorize(Roles = "Admin")] // ? SECURITY FIX: Only admins can create new organizers
        [HttpPost]
        public async Task<ActionResult<Organizer>> PostOrganizer(Organizer organizer)
        {
            _context.Organizers.Add(organizer);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOrganizer), new { id = organizer.Id }, organizer);
        }

        // DELETE: api/Organizers/5
        [Authorize(Roles = "Admin")] // ? SECURITY FIX: Only admins can delete organizers
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrganizer(int id)
        {
            var organizer = await _context.Organizers.FindAsync(id);
            if (organizer == null)
            {
                return NotFound();
            }

            _context.Organizers.Remove(organizer);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool OrganizerExists(int id)
        {
            return _context.Organizers.Any(e => e.Id == id);
        }

        // ORGANIZER SALES DASHBOARD ENDPOINTS
        // GET: api/Organizers/{id}/revenue-stats
        [Authorize(Roles = "Organizer")]
        [HttpGet("{id}/revenue-stats")]
        public async Task<ActionResult<object>> GetOrganizerDashboardSummary(int id)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return BadRequest(new { message = "Authentication error" });
            }

            var organizer = await _context.Organizers
                .FirstOrDefaultAsync(o => o.UserId == userId && o.Id == id);
                
            if (organizer == null)
            {
                return BadRequest(new { message = "No organizer profile found or access denied" });
            }

            var eventIds = await _context.Events
                .Where(e => e.OrganizerId == organizer.Id)
                .Select(e => e.Id)
                .ToListAsync();

            if (!eventIds.Any())
            {
                return Ok(new { totalRevenue = 0, totalTicketsSold = 0, totalEvents = 0, totalBookings = 0 });
            }

            var bookings = await _context.Bookings
                .Where(b => eventIds.Contains(b.EventId))
                .ToListAsync();

            var totalRevenue = bookings.Sum(b => b.TotalAmount);
            var totalBookings = bookings.Count;

            // Get total tickets sold from BookingLineItems table
            var totalTicketsSold = await _context.BookingLineItems
                .Where(bli => bookings.Select(b => b.Id).Contains(bli.BookingId) && bli.ItemType == "Ticket")
                .SumAsync(bli => bli.Quantity);

            return Ok(new
            {
                totalRevenue,
                totalTicketsSold,
                totalEvents = eventIds.Count,
                totalBookings
            });
        }

        // GET: api/Organizers/{id}/daily-analytics
        [Authorize(Roles = "Organizer")]
        [HttpGet("{id}/daily-analytics")]
        public async Task<ActionResult<object>> GetOrganizerDailyAnalytics(
            int id,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return BadRequest(new { message = "Authentication error" });
            }

            var organizer = await _context.Organizers
                .FirstOrDefaultAsync(o => o.UserId == userId && o.Id == id);
                
            if (organizer == null)
            {
                return BadRequest(new { message = "No organizer profile found or access denied" });
            }

            var eventIds = await _context.Events
                .Where(e => e.OrganizerId == organizer.Id)
                .Select(e => e.Id)
                .ToListAsync();

            if (!eventIds.Any())
            {
                return Ok(new { revenue = new List<object>(), tickets = new List<object>() });
            }

            var defaultStartDate = DateTime.Now.AddDays(-30);
            var defaultEndDate = DateTime.Now;
            
            var actualStartDate = startDate ?? defaultStartDate;
            var actualEndDate = endDate ?? defaultEndDate;

            var bookings = await _context.Bookings
                .Where(b => eventIds.Contains(b.EventId) && 
                           b.CreatedAt >= actualStartDate && 
                           b.CreatedAt <= actualEndDate)
                .ToListAsync();

            var bookingIds = bookings.Select(b => b.Id).ToList();
            var bookingLineItems = await _context.BookingLineItems
                .Where(bli => bookingIds.Contains(bli.BookingId) && bli.ItemType == "Ticket")
                .ToListAsync();

            var revenueData = bookings
                .GroupBy(b => b.CreatedAt.Date)
                .Select(g => new { date = g.Key.ToString("yyyy-MM-dd"), revenue = g.Sum(b => b.TotalAmount) })
                .OrderBy(x => x.date)
                .ToList();

            var ticketData = bookingLineItems
                .Join(bookings, bli => bli.BookingId, b => b.Id, (bli, b) => new { bli.Quantity, Date = b.CreatedAt.Date })
                .GroupBy(x => x.Date)
                .Select(g => new { date = g.Key.ToString("yyyy-MM-dd"), tickets = g.Sum(x => x.Quantity) })
                .OrderBy(x => x.date)
                .ToList();

            return Ok(new { revenue = revenueData, tickets = ticketData });
        }

        // GET: api/Organizers/{id}/bookings
        [Authorize(Roles = "Organizer")]
        [HttpGet("{id}/bookings")]
        public async Task<ActionResult<object>> GetOrganizerBookings(
            int id,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string search = "")
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return BadRequest(new { message = "Authentication error" });
            }

            var organizer = await _context.Organizers
                .FirstOrDefaultAsync(o => o.UserId == userId && o.Id == id);
                
            if (organizer == null)
            {
                return BadRequest(new { message = "No organizer profile found or access denied" });
            }

            var eventIds = await _context.Events
                .Where(e => e.OrganizerId == organizer.Id)
                .Select(e => e.Id)
                .ToListAsync();

            if (!eventIds.Any())
            {
                return Ok(new { bookings = new List<object>(), totalCount = 0, totalPages = 0 });
            }

            var query = _context.Bookings
                .Include(b => b.Event)
                .Include(b => b.BookingLineItems)
                .Where(b => eventIds.Contains(b.EventId));

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(b => 
                    b.CustomerEmail.Contains(search) ||
                    b.CustomerFirstName.Contains(search) ||
                    b.CustomerLastName.Contains(search) ||
                    b.Event.Title.Contains(search));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var bookings = await query
                .OrderByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(b => new
                {
                    id = b.Id,
                    customerName = $"{b.CustomerFirstName} {b.CustomerLastName}",
                    customerEmail = b.CustomerEmail,
                    eventTitle = b.Event.Title,
                    totalAmount = b.TotalAmount,
                    createdAt = b.CreatedAt,
                    paymentStatus = b.PaymentStatus,
                    ticketCount = b.BookingLineItems.Where(bli => bli.ItemType == "Ticket").Sum(bli => bli.Quantity)
                })
                .ToListAsync();

            Response.Headers.Add("Access-Control-Expose-Headers", "X-Total-Count,X-Total-Pages,X-Current-Page");
            Response.Headers.Add("X-Total-Count", totalCount.ToString());
            Response.Headers.Add("X-Total-Pages", totalPages.ToString());
            Response.Headers.Add("X-Current-Page", page.ToString());

            return Ok(new { bookings, totalCount, totalPages });
        }
    }
}
