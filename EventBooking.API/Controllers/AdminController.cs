using EventBooking.API.Data;
using EventBooking.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EventBooking.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(AppDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Admin/dashboard-stats
        [HttpGet("dashboard-stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var stats = new
                {
                    TotalEvents = await _context.Events.CountAsync(),
                    ActiveEvents = await _context.Events.CountAsync(e => e.IsActive),
                    TotalOrganizers = await _context.Organizers.CountAsync(),
                    PendingOrganizers = await _context.Organizers.CountAsync(o => !o.IsVerified),
                    VerifiedOrganizers = await _context.Organizers.CountAsync(o => o.IsVerified),
                    TotalUsers = await _context.Users.CountAsync(),
                    TotalReservations = await _context.Reservations.CountAsync(),
                    RecentEvents = await _context.Events
                        .OrderByDescending(e => e.Id)
                        .Take(5)
                        .Select(e => new { e.Id, e.Title, e.Date, e.IsActive })
                        .ToListAsync()
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard stats");
                return StatusCode(500, "Error retrieving dashboard statistics");
            }
        }

        // GET: api/Admin/organizers
        [HttpGet("organizers")]
        public async Task<IActionResult> GetAllOrganizers([FromQuery] bool? verified = null)
        {
            try
            {
                var query = _context.Organizers.AsQueryable();

                if (verified.HasValue)
                {
                    query = query.Where(o => o.IsVerified == verified.Value);
                }

                var organizers = await query
                    .Include(o => o.User)
                    .Select(o => new
                    {
                        o.Id,
                        o.Name,
                        o.OrganizationName,
                        o.ContactEmail,
                        o.PhoneNumber,
                        o.Website,
                        o.FacebookUrl,
                        o.YoutubeUrl,
                        o.IsVerified,
                        o.CreatedAt,
                        UserEmail = o.User != null ? o.User.Email : null,
                        EventsCount = _context.Events.Count(e => e.OrganizerId == o.Id)
                    })
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                return Ok(organizers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving organizers list");
                return StatusCode(500, "Error retrieving organizers");
            }
        }

        // PUT: api/Admin/organizers/{id}/verify
        [HttpPut("organizers/{id}/verify")]
        public async Task<IActionResult> VerifyOrganizer(int id, [FromBody] VerifyOrganizerRequest request)
        {
            try
            {
                var organizer = await _context.Organizers.FindAsync(id);
                if (organizer == null)
                {
                    return NotFound("Organizer not found");
                }

                organizer.IsVerified = true;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Organizer {OrganizerId} verified by admin {AdminId}", 
                    id, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

                return Ok(new { 
                    message = "Organizer verified successfully",
                    organizerId = id,
                    verifiedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying organizer {OrganizerId}", id);
                return StatusCode(500, "Error verifying organizer");
            }
        }

        // PUT: api/Admin/organizers/{id}/unverify
        [HttpPut("organizers/{id}/unverify")]
        public async Task<IActionResult> UnverifyOrganizer(int id)
        {
            try
            {
                var organizer = await _context.Organizers.FindAsync(id);
                if (organizer == null)
                {
                    return NotFound("Organizer not found");
                }

                organizer.IsVerified = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Organizer {OrganizerId} unverified by admin {AdminId}", 
                    id, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

                return Ok(new { 
                    message = "Organizer verification removed",
                    organizerId = id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unverifying organizer {OrganizerId}", id);
                return StatusCode(500, "Error removing verification");
            }
        }

        // GET: api/Admin/events
        [HttpGet("events")]
        public async Task<IActionResult> GetAllEvents([FromQuery] bool? active = null)
        {
            try
            {
                var query = _context.Events.AsQueryable();

                if (active.HasValue)
                {
                    query = query.Where(e => e.IsActive == active.Value);
                }

                var events = await query
                    .Include(e => e.Organizer)
                    .Select(e => new
                    {
                        e.Id,
                        e.Title,
                        e.Description,
                        e.Date,
                        e.Location,
                        e.Price,
                        e.Capacity,
                        e.ImageUrl,
                        e.IsActive,
                        e.SeatSelectionMode,
                        Organizer = e.Organizer == null ? null : new
                        {
                            e.Organizer.Id,
                            e.Organizer.Name,
                            e.Organizer.IsVerified
                        },
                        ReservationsCount = _context.Reservations.Count(r => r.EventId == e.Id)
                    })
                    .OrderByDescending(e => e.Date)
                    .ToListAsync();

                return Ok(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving events list");
                return StatusCode(500, "Error retrieving events");
            }
        }

        // PUT: api/Admin/events/{id}/toggle-status
        [HttpPut("events/{id}/toggle-status")]
        public async Task<IActionResult> ToggleEventStatus(int id)
        {
            try
            {
                var eventItem = await _context.Events.FindAsync(id);
                if (eventItem == null)
                {
                    return NotFound("Event not found");
                }

                eventItem.IsActive = !eventItem.IsActive;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Event {EventId} status changed to {Status} by admin {AdminId}", 
                    id, eventItem.IsActive ? "Active" : "Inactive", User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

                return Ok(new { 
                    message = $"Event {(eventItem.IsActive ? "activated" : "deactivated")} successfully",
                    eventId = id,
                    isActive = eventItem.IsActive
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling event status {EventId}", id);
                return StatusCode(500, "Error updating event status");
            }
        }

        // GET: api/Admin/users
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _context.Users
                    .Select(u => new
                    {
                        u.Id,
                        u.UserName,
                        u.Email,
                        u.FullName,
                        u.Role,
                        u.EmailConfirmed,
                        u.LockoutEnd,
                        IsOrganizer = _context.Organizers.Any(o => o.UserId == u.Id)
                    })
                    .OrderBy(u => u.Email)
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users list");
                return StatusCode(500, "Error retrieving users");
            }
        }

        // PUT: api/Admin/seats/{seatId}/toggle-availability
        [HttpPut("seats/{seatId}/toggle-availability")]
        public async Task<IActionResult> ToggleSeatAvailability(int seatId)
        {
            try
            {
                var seat = await _context.Seats.FindAsync(seatId);
                if (seat == null)
                {
                    return NotFound(new { message = "Seat not found" });
                }

                // Only allow toggling between Available and Unavailable
                // Don't allow changing Reserved or Booked seats
                if (seat.Status == SeatStatus.Reserved || seat.Status == SeatStatus.Booked)
                {
                    return BadRequest(new { message = "Cannot change status of reserved or booked seats" });
                }

                // Toggle between Available and Unavailable
                seat.Status = seat.Status == SeatStatus.Available ? SeatStatus.Unavailable : SeatStatus.Available;
                
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Admin toggled seat {seatId} status to {seat.Status}");

                return Ok(new { 
                    message = "Seat status updated successfully",
                    seatId = seatId,
                    newStatus = seat.Status.ToString(),
                    statusValue = (int)seat.Status
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error toggling seat availability for seat {seatId}");
                return StatusCode(500, new { message = "An error occurred while updating seat status" });
            }
        }
    }

    public class VerifyOrganizerRequest
    {
        public string? Notes { get; set; }
    }
}
