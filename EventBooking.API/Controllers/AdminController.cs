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
    [Authorize(Roles = "Admin")]
    [Route("[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AdminController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(
            AppDbContext context, 
            ILogger<AdminController> logger,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
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
                // Admin only sees events that are submitted for review or already processed
                var query = _context.Events
                    .Where(e => e.Status != EventStatus.Draft);

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
                        e.Status,
                        StatusText = e.Status.ToString(),
                        e.SeatSelectionMode,
                        e.ProcessingFeePercentage,
                        e.ProcessingFeeFixedAmount,
                        e.ProcessingFeeEnabled,
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

                // New status-based logic
                if (eventItem.Status == EventStatus.Pending)
                {
                    eventItem.Status = EventStatus.Active;
                    eventItem.IsActive = true;
                }
                else if (eventItem.Status == EventStatus.Active)
                {
                    eventItem.Status = EventStatus.Inactive;
                    eventItem.IsActive = false;
                }
                else if (eventItem.Status == EventStatus.Inactive)
                {
                    eventItem.Status = EventStatus.Active;
                    eventItem.IsActive = true;
                }
                else
                {
                    return BadRequest("Draft events cannot be activated directly. They must be submitted for review first.");
                }

                await _context.SaveChangesAsync();

                var statusText = eventItem.Status switch
                {
                    EventStatus.Active => "activated",
                    EventStatus.Inactive => "deactivated", 
                    _ => "updated"
                };

                _logger.LogInformation("Event {EventId} status changed to {Status} by admin {AdminId}", 
                    id, eventItem.Status, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

                return Ok(new { 
                    message = $"Event {statusText} successfully",
                    eventId = id,
                    status = eventItem.Status.ToString(),
                    isActive = eventItem.IsActive
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling event status {EventId}", id);
                return StatusCode(500, "Error updating event status");
            }
        }

        // PUT: api/Admin/events/{id}/processing-fee
        [HttpPut("events/{id}/processing-fee")]
        public async Task<IActionResult> UpdateEventProcessingFee(int id, [FromBody] UpdateProcessingFeeRequest request)
        {
            try
            {
                var eventItem = await _context.Events.FindAsync(id);
                if (eventItem == null)
                {
                    return NotFound("Event not found");
                }

                // Validate input
                if (request.ProcessingFeePercentage < 0 || request.ProcessingFeePercentage > 100)
                {
                    return BadRequest("Processing fee percentage must be between 0 and 100");
                }

                if (request.ProcessingFeeFixedAmount < 0)
                {
                    return BadRequest("Processing fee fixed amount must be non-negative");
                }

                // Update processing fee settings
                eventItem.ProcessingFeePercentage = request.ProcessingFeePercentage;
                eventItem.ProcessingFeeFixedAmount = request.ProcessingFeeFixedAmount;
                eventItem.ProcessingFeeEnabled = request.ProcessingFeeEnabled;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Event {EventId} processing fee updated by admin {AdminId}. " +
                    "Enabled: {Enabled}, Percentage: {Percentage}%, Fixed: ${Fixed}", 
                    id, User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                    request.ProcessingFeeEnabled, request.ProcessingFeePercentage, request.ProcessingFeeFixedAmount);

                return Ok(new { 
                    message = "Processing fee updated successfully",
                    eventId = id,
                    processingFeeEnabled = eventItem.ProcessingFeeEnabled,
                    processingFeePercentage = eventItem.ProcessingFeePercentage,
                    processingFeeFixedAmount = eventItem.ProcessingFeeFixedAmount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating processing fee for event {EventId}", id);
                return StatusCode(500, "Error updating processing fee");
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

        // === USER MANAGEMENT ENDPOINTS ===

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _userManager.Users
                    .Select(u => new
                    {
                        u.Id,
                        u.Email,
                        u.FullName,
                        u.Role,
                        u.EmailConfirmed,
                        u.LockoutEnabled,
                        u.LockoutEnd
                    })
                    .ToListAsync();

                // Get roles for each user
                var usersWithRoles = new List<object>();
                foreach (var user in users)
                {
                    var userEntity = await _userManager.FindByIdAsync(user.Id);
                    var roles = await _userManager.GetRolesAsync(userEntity!);
                    
                    usersWithRoles.Add(new
                    {
                        user.Id,
                        user.Email,
                        user.FullName,
                        user.Role,
                        user.EmailConfirmed,
                        user.LockoutEnabled,
                        user.LockoutEnd,
                        Roles = roles
                    });
                }

                return Ok(usersWithRoles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving users");
                return StatusCode(500, new { message = "An error occurred while retrieving users" });
            }
        }

        [HttpPost("create-admin")]
        public async Task<IActionResult> CreateAdminUser([FromBody] RegisterDTO dto)
        {
            try
            {
                // Ensure Admin role exists
                if (!await _roleManager.RoleExistsAsync("Admin"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Admin"));
                }

                var user = new ApplicationUser
                {
                    FullName = dto.FullName,
                    Email = dto.Email,
                    UserName = dto.Email,
                    Role = "Admin",
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, dto.Password);

                if (!result.Succeeded)
                    return BadRequest(result.Errors);

                await _userManager.AddToRoleAsync(user, "Admin");

                return Ok(new { 
                    message = "Admin user created successfully",
                    userId = user.Id,
                    role = "Admin"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating admin user");
                return StatusCode(500, new { message = "An error occurred while creating admin user" });
            }
        }

        [HttpPost("create-organizer")]
        public async Task<IActionResult> CreateOrganizerUser([FromBody] RegisterDTO dto)
        {
            try
            {
                // Ensure Organizer role exists
                if (!await _roleManager.RoleExistsAsync("Organizer"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Organizer"));
                }

                var user = new ApplicationUser
                {
                    FullName = dto.FullName,
                    Email = dto.Email,
                    UserName = dto.Email,
                    Role = "Organizer",
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, dto.Password);

                if (!result.Succeeded)
                    return BadRequest(result.Errors);

                await _userManager.AddToRoleAsync(user, "Organizer");

                return Ok(new { 
                    message = "Organizer user created successfully",
                    userId = user.Id,
                    role = "Organizer"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating organizer user");
                return StatusCode(500, new { message = "An error occurred while creating organizer user" });
            }
        }

        [HttpPost("reset-user-password")]
        public async Task<IActionResult> ResetUserPassword([FromBody] ResetPasswordDTO dto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(dto.Email);
                if (user == null)
                    return NotFound("User not found");

                // Remove existing password and set new one
                var removePasswordResult = await _userManager.RemovePasswordAsync(user);
                if (!removePasswordResult.Succeeded)
                    return BadRequest("Failed to remove old password");

                var addPasswordResult = await _userManager.AddPasswordAsync(user, dto.NewPassword);
                if (!addPasswordResult.Succeeded)
                    return BadRequest(addPasswordResult.Errors);

                return Ok(new { message = "Password reset successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting user password");
                return StatusCode(500, new { message = "An error occurred while resetting password" });
            }
        }
    }

    public class VerifyOrganizerRequest
    {
        public string? Notes { get; set; }
    }
}
