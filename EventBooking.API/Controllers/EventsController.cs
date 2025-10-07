using EventBooking.API.Data;
using EventBooking.API.DTOs;
using EventBooking.API.Models;
using EventBooking.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace EventBooking.API.Controllers
{
    //[Authorize(Roles = "Admin,Organizer")]
    [Authorize(Roles = "Admin,Organizer")]
    [Route("[controller]")]
    [ApiController]
    public class EventsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEventStatusService _eventStatusService;
        private readonly IImageService _imageService;
        private readonly ISeatCreationService _seatCreationService;
        private readonly ITicketAvailabilityService _ticketAvailabilityService;

        public EventsController(
            AppDbContext context, 
            IEventStatusService eventStatusService, 
            IImageService imageService,
            ISeatCreationService seatCreationService,
            ITicketAvailabilityService ticketAvailabilityService)
        {
            _context = context;
            _eventStatusService = eventStatusService;
            _imageService = imageService;
            _seatCreationService = seatCreationService;
            _ticketAvailabilityService = ticketAvailabilityService;
        }



        // GET: api/Events
        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Event>>> GetEvents()
        {
            var currentNZTime = _eventStatusService.GetCurrentNZTime();
            var currentDate = currentNZTime.Date;

            // For public endpoint, only show ACTIVE events (status = 2)
            // Get all events with venue and ticket type information included
            var allEvents = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Organizer)
                .Include(e => e.TicketTypes)
                .Where(e => e.Date.HasValue && e.Status == EventStatus.Active) // Only active events for public
                .OrderBy(e => e.Date)
                .ToListAsync();

            // Separate and sort using service logic
            var upcomingEvents = allEvents
                .Where(e => _eventStatusService.IsEventActive(e.Date))
                .ToList();

            var pastEvents = allEvents
                .Where(e => _eventStatusService.IsEventExpired(e.Date))
                .ToList();

            // Return upcoming events first, then past events
            return upcomingEvents.Concat(pastEvents).ToList();
        }

        // GET: api/Events/5
        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult<Event>> GetEvent(int id)
        {
            var @event = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Organizer)
                .Include(e => e.TicketTypes)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (@event == null)
            {
                return NotFound();
            }

            return @event;
        }

        // GET: api/Events/by-title/{title}
        [AllowAnonymous]
        [HttpGet("by-title/{title}")]
        public async Task<ActionResult<Event>> GetEventByTitle(string title)
        {
            // Get all events and check slug generation client-side
            var allEvents = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Organizer)
                .Include(e => e.TicketTypes)
                .ToListAsync();

            // Find event where generated slug matches the requested title
            foreach (var evt in allEvents)
            {
                var generatedSlug = GenerateSlugFromTitle(evt.Title);
                if (generatedSlug.Equals(title, StringComparison.OrdinalIgnoreCase))
                {
                    return evt;
                }
            }

            return NotFound();
        }

        /// <summary>
        /// Generate slug from title - must match frontend slugUtils.ts logic exactly
        /// </summary>
        private string GenerateSlugFromTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return string.Empty;
            }

            return title
                .ToLower()
                .Trim()
                .Replace(" ", "-")                    // Replace spaces with -
                .Replace("'", "")                     // Remove apostrophes
                .Replace("\"", "")                    // Remove quotes
                .Replace("&", "and")                  // Replace & with and
                .Replace(",", "")                     // Remove commas
                .Replace(".", "")                     // Remove periods
                .Replace("!", "")                     // Remove exclamation marks
                .Replace("?", "")                     // Remove question marks
                .Replace("(", "")                     // Remove parentheses
                .Replace(")", "")                     // Remove parentheses
                .Replace("[", "")                     // Remove brackets
                .Replace("]", "")                     // Remove brackets
                .Replace("{", "")                     // Remove braces
                .Replace("}", "")                     // Remove braces
                .Replace(":", "")                     // Remove colons
                .Replace(";", "")                     // Remove semicolons
                .Replace("/", "")                     // Remove slashes
                .Replace("\\", "")                    // Remove backslashes
                .Replace("|", "")                     // Remove pipes
                .Replace("*", "")                     // Remove asterisks
                .Replace("+", "")                     // Remove plus signs
                .Replace("=", "")                     // Remove equals signs
                .Replace("@", "")                     // Remove at signs
                .Replace("#", "")                     // Remove hash signs
                .Replace("$", "")                     // Remove dollar signs
                .Replace("%", "")                     // Remove percent signs
                .Replace("^", "")                     // Remove carets
                .Replace("`", "")                     // Remove backticks
                .Replace("~", "")                     // Remove tildes
                .Replace("<", "")                     // Remove less than
                .Replace(">", "")                     // Remove greater than
                .Replace("--", "-")                   // Replace double hyphens
                .Replace("---", "-")                  // Replace triple hyphens
                .Trim('-');                           // Remove leading/trailing hyphens
        }

        [Authorize(Roles = "Organizer")]
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CreateEvent([FromForm] EventCreateDTO dto)
        {
            // Log the incoming request
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<EventsController>>();
            logger.LogInformation("CreateEvent called with data: {@EventCreateDTO}", dto);

            // Check model validation
            if (!ModelState.IsValid)
            {
                logger.LogWarning("CreateEvent validation failed. Errors: {@ValidationErrors}", 
                    ModelState.Where(x => x.Value?.Errors.Count > 0)
                             .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage)));
                
                return BadRequest(ModelState);
            }

            // Additional custom validation for title
            if (!dto.IsValidTitle())
            {
                ModelState.AddModelError("Title", "Title cannot contain multiple consecutive spaces");
                return BadRequest(ModelState);
            }

            // Get the current user's organizer ID or use first available organizer for testing
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Organizer organizer;
            
            if (userId == null)
            {
                // For testing purposes, use the first available organizer
                logger.LogWarning("CreateEvent: No authenticated user, using first available organizer for testing");
                organizer = await _context.Organizers.FirstOrDefaultAsync();
            }
            else
            {
                organizer = await _context.Organizers
                    .FirstOrDefaultAsync(o => o.UserId == userId);
            }
            
            if (organizer == null)
            {
                logger.LogWarning("CreateEvent: No organizer found");
                return BadRequest("No organizer available. Please contact support.");
            }

            try
            {
                string? imageUrl = null;
                
                // Handle image upload
                if (dto.Image != null && dto.Image.Length > 0)
                {
                    imageUrl = await _imageService.SaveImageAsync(dto.Image);
                    logger.LogInformation("Image uploaded successfully for event: {ImageUrl}", imageUrl);
                }

                // Check if VenueId is provided and valid
                Venue? venue = null;
                if (dto.VenueId.HasValue && dto.VenueId.Value > 0)
                {
                    venue = await _context.Venues.FindAsync(dto.VenueId.Value);
                    if (venue == null)
                    {
                        logger.LogWarning("CreateEvent: Invalid venue ID {VenueId}", dto.VenueId.Value);
                        return BadRequest(new { message = "The selected venue does not exist." });
                    }
                }

                var newEvent = new Event
                {
                    Title = dto.Title,
                    Description = dto.Description,
                    Date = dto.Date,
                    Location = dto.Location,
                    Price = dto.Price,
                    Capacity = dto.Capacity,
                    OrganizerId = organizer.Id,
                    ImageUrl = imageUrl,
                    // Use DTO's seat selection mode directly
                    SeatSelectionMode = dto.SeatSelectionMode,
                    StagePosition = dto.StagePosition,
                    VenueId = dto.VenueId,
                    IsActive = false, // Keep for backward compatibility
                    Status = EventStatus.Draft // Events start as draft for organizer testing
                };

                _context.Events.Add(newEvent);
                await _context.SaveChangesAsync();

                // Automatically create seats if we have a venue and are using EventHall or Hybrid mode
                int seatsCreated = 0;
                
                // Log the event data to debug
                logger.LogInformation("Event created with SeatSelectionMode: {SeatSelectionMode} (value: {ModeValue}), VenueId: {VenueId}", 
                    newEvent.SeatSelectionMode, (int)newEvent.SeatSelectionMode, newEvent.VenueId);
                
                if (newEvent.VenueId.HasValue && 
                    (newEvent.SeatSelectionMode == SeatSelectionMode.EventHall || 
                     newEvent.SeatSelectionMode == SeatSelectionMode.Hybrid))
                {
                    logger.LogInformation("Creating seats for event {EventId} with venue {VenueId} in {Mode} mode", 
                        newEvent.Id, newEvent.VenueId.Value, newEvent.SeatSelectionMode);
                    seatsCreated = await _seatCreationService.CreateSeatsForEventAsync(newEvent.Id, newEvent.VenueId.Value);
                    logger.LogInformation("Created {SeatsCount} seats for event {EventId}", seatsCreated, newEvent.Id);
                }
                else
                {
                    logger.LogWarning("Not creating seats because: VenueId exists: {HasVenue}, SeatSelectionMode: {Mode}", 
                        newEvent.VenueId.HasValue, newEvent.SeatSelectionMode);
                }

                logger.LogInformation("Event created successfully with ID {EventId} by organizer {OrganizerId}", 
                    newEvent.Id, organizer.Id);

                return Ok(new { 
                    id = newEvent.Id,
                    message = "Event created successfully as draft. You can test it privately before submitting for approval.",
                    eventData = newEvent,
                    seatsCreated = seatsCreated
                });
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning("Validation error creating event: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating event for organizer {OrganizerId}", organizer.Id);
                return StatusCode(500, "An error occurred while creating the event");
            }
        }

        // GET: api/Events/by-organizer
        [Authorize(Roles = "Organizer")]
        [HttpGet("by-organizer")]
        public async Task<ActionResult<IEnumerable<Event>>> GetOrganizerEvents()
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<EventsController>>();
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (userId == null)
            {
                logger.LogWarning("GetOrganizerEvents: User ID not found in claims");
                return BadRequest(new { message = "Authentication error. Please try logging in again." });
            }

            var organizer = await _context.Organizers
                .FirstOrDefaultAsync(o => o.UserId == userId);
            
            if (organizer == null)
            {
                logger.LogWarning("GetOrganizerEvents: No organizer profile found for user {UserId}", userId);
                return BadRequest(new { message = "No organizer profile found. Please complete your organizer registration." });
            }

            var events = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.TicketTypes)
                .Where(e => e.OrganizerId == organizer.Id)
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            return events;
        }

        // PUT: api/Events/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AllowAnonymous]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutEvent(int id, Event @event)
        {
            if (id != @event.Id)
            {
                return BadRequest();
            }

            _context.Entry(@event).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EventExists(id))
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

        // POST: api/Events
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        /*[Authorize(Roles = "Organizer")]
        [HttpPost]
        public async Task<ActionResult<Event>> PostEvent(Event @event)
        {
            _context.Events.Add(@event);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetEvent", new { id = @event.Id }, @event);
        }*/

        // DELETE: api/Events/5
        [Authorize(Roles = "Admin,Organizer")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var @event = await _context.Events.FindAsync(id);
            if (@event == null)
            {
                return NotFound();
            }

            // Delete associated image if it exists
            if (!string.IsNullOrEmpty(@event.ImageUrl))
            {
                await _imageService.DeleteImageAsync(@event.ImageUrl);
            }

            _context.Events.Remove(@event);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // REMOVED: Commented out seed endpoint - Development artifacts removed for production security
        
        // PUT: api/Events/{id}/submit-for-review
        [Authorize(Roles = "Organizer")]
        [HttpPut("{id}/submit-for-review")]
        public async Task<IActionResult> SubmitEventForReview(int id)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<EventsController>>();
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

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == id && e.OrganizerId == organizer.Id);
            
            if (eventItem == null)
            {
                return NotFound(new { message = "Event not found or you don't have permission to access it." });
            }

            if (eventItem.Status != EventStatus.Draft)
            {
                return BadRequest(new { message = "Only draft events can be submitted for review." });
            }

            eventItem.Status = EventStatus.Pending;
            eventItem.IsActive = false; // Keep backward compatibility
            await _context.SaveChangesAsync();

            logger.LogInformation("Event {EventId} submitted for review by organizer {OrganizerId}", id, organizer.Id);

            return Ok(new { 
                message = "Event submitted for admin review successfully",
                eventId = id,
                status = "Pending"
            });
        }

        // PUT: api/Events/{id}/return-to-draft
        [Authorize(Roles = "Organizer")]
        [HttpPut("{id}/return-to-draft")]
        public async Task<IActionResult> ReturnEventToDraft(int id)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<EventsController>>();
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

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == id && e.OrganizerId == organizer.Id);
            
            if (eventItem == null)
            {
                return NotFound(new { message = "Event not found or you don't have permission to access it." });
            }

            if (eventItem.Status != EventStatus.Pending && eventItem.Status != EventStatus.Inactive)
            {
                return BadRequest(new { message = $"Only pending or inactive events can be returned to draft. Current status: {eventItem.Status}" });
            }

            eventItem.Status = EventStatus.Draft;
            eventItem.IsActive = false; // Keep backward compatibility
            await _context.SaveChangesAsync();

            logger.LogInformation("Event {EventId} returned to draft by organizer {OrganizerId}", id, organizer.Id);

            return Ok(new { 
                message = "Event returned to draft status successfully",
                eventId = id,
                status = "Draft"
            });
        }

        // GET: api/Events/{id}/preview
        [Authorize(Roles = "Organizer")]
        [HttpGet("{id}/preview")]
        public async Task<ActionResult<Event>> PreviewEvent(int id)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<EventsController>>();
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

            var eventItem = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.TicketTypes)
                .Include(e => e.FoodItems)
                .FirstOrDefaultAsync(e => e.Id == id && e.OrganizerId == organizer.Id);
            
            if (eventItem == null)
            {
                return NotFound(new { message = "Event not found or you don't have permission to access it." });
            }

            logger.LogInformation("Event {EventId} previewed by organizer {OrganizerId}", id, organizer.Id);

            return eventItem;
        }

        // ==============================================
        // REVENUE ANALYSIS ENDPOINTS 
        // Sales Dashboard Enhancement - Phase 2
        // ==============================================

        /// <summary>
        /// Tab 1: Get ticket capacity summary for revenue analysis
        /// GET: api/Events/{eventId}/ticket-capacity
        /// </summary>
        [Authorize(Roles = "Organizer")]
        [HttpGet("{eventId}/ticket-capacity")]
        public async Task<ActionResult<TicketCapacityResponseDTO>> GetTicketCapacity(int eventId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return BadRequest(new { message = "Authentication error" });
            }

            // Verify organizer owns this event
            var eventExists = await _context.Events
                .Include(e => e.Organizer)
                .Where(e => e.Id == eventId && e.Organizer.UserId == userId)
                .FirstOrDefaultAsync();

            if (eventExists == null)
            {
                return NotFound(new { message = "Event not found or access denied" });
            }

            try
            {
                // 🎯 ARCHITECTURE v6: Reuse Tab 02 (Stripe) and Tab 03 (Organizer) data instead of duplicating logic
                // Using private helper methods that return DTOs directly (no ActionResult wrapper issues)
                
                // Get Stripe revenue data (Tab 02) - contains ticket counts per pricing tier
                var stripeRevenue = await GetStripeRevenueDataAsync(eventId);
                
                // Get Organizer revenue data (Tab 03) - contains issued tickets per ticket type
                var organizerRevenue = await GetOrganizerRevenueDataAsync(eventId);
                
                // Get all ticket types for this event (exclude $0 tickets)
                var ticketTypes = await _context.TicketTypes
                    .Where(tt => tt.EventId == eventId && tt.Price > 0)
                    .ToListAsync();

                var ticketCapacityList = new List<TicketCapacityDTO>();

                foreach (var tt in ticketTypes)
                {
                    // Calculate sold tickets by summing Tab 02 + Tab 03 data
                    var stripeTicketsSold = 0;
                    var organizerTicketsSold = 0;
                    
                    if (stripeRevenue != null)
                    {
                        // Match by ticket price (Tab 02 groups by pricing tier)
                        stripeTicketsSold = stripeRevenue.PricingTiers
                            .Where(tier => tier.TicketPrice == tt.Price)
                            .Sum(tier => tier.Quantity);
                    }
                    
                    if (organizerRevenue != null)
                    {
                        // Match by ticket type ID (Tab 03 has direct ticket type data)
                        var organizerTicketType = organizerRevenue.TicketTypes
                            .FirstOrDefault(ott => ott.TicketTypeId == tt.Id);
                        organizerTicketsSold = organizerTicketType?.IssuedTickets ?? 0;
                    }
                    
                    var soldTickets = stripeTicketsSold + organizerTicketsSold;
                    
                    // Determine total capacity based on event type
                    int totalCapacity;
                    int reservedTickets = 0;
                    
                    if (tt.MaxTickets.HasValue)
                    {
                        // General Admission or Standing tickets (uses MaxTickets)
                        totalCapacity = tt.MaxTickets.Value;
                        // For general admission, reserved count is based on Status.Booked seats minus sold tickets
                        var bookedSeatsCount = await _context.Seats
                            .Where(s => s.TicketTypeId == tt.Id && s.EventId == eventId && s.Status == SeatStatus.Booked)
                            .CountAsync();
                        reservedTickets = Math.Max(0, bookedSeatsCount - soldTickets);
                    }
                    else
                    {
                        // Seated tickets (count Seat records, excluding Unavailable seats)
                        totalCapacity = await _context.Seats
                            .Where(s => s.TicketTypeId == tt.Id && s.EventId == eventId && s.Status != SeatStatus.Unavailable)
                            .CountAsync();
                        
                        // Reserved = Seats with Status.Booked - Sold Count
                        var bookedSeatsCount = await _context.Seats
                            .Where(s => s.TicketTypeId == tt.Id && s.EventId == eventId && s.Status == SeatStatus.Booked)
                            .CountAsync();
                        reservedTickets = Math.Max(0, bookedSeatsCount - soldTickets);
                    }
                    
                    // Available = Total Capacity - (Sold + Reserved)
                    var availableTickets = totalCapacity - (soldTickets + reservedTickets);
                    var utilization = totalCapacity > 0 
                        ? Math.Round((decimal)soldTickets / totalCapacity * 100, 1)
                        : 0;

                    ticketCapacityList.Add(new TicketCapacityDTO
                    {
                        TicketTypeId = tt.Id,
                        TicketTypeName = tt.Name ?? "Unknown",
                        TicketPrice = tt.Price,
                        SoldTickets = soldTickets,
                        ReservedTickets = reservedTickets,
                        AvailableTickets = availableTickets,
                        TotalCapacity = totalCapacity,
                        UtilizationPercentage = utilization,
                        Color = tt.Color ?? "#6b7280"
                    });
                }

                // Sort by ticket price descending (most expensive first)
                var sortedList = ticketCapacityList.OrderByDescending(tc => tc.TicketPrice).ToList();

                // Calculate summary totals
                var summary = new TicketCapacitySummaryDTO
                {
                    TotalSoldTickets = sortedList.Sum(tc => tc.SoldTickets),
                    TotalReservedTickets = sortedList.Sum(tc => tc.ReservedTickets),
                    TotalAvailableTickets = sortedList.Sum(tc => tc.AvailableTickets),
                    TotalMaxCapacity = sortedList.Sum(tc => tc.TotalCapacity),
                    OverallUtilizationPercentage = sortedList.Sum(tc => tc.TotalCapacity) > 0
                        ? Math.Round((decimal)sortedList.Sum(tc => tc.SoldTickets) / sortedList.Sum(tc => tc.TotalCapacity) * 100, 1)
                        : 0
                };

                // Return response with summary and ticket types
                var response = new TicketCapacityResponseDTO
                {
                    Summary = summary,
                    TicketTypes = sortedList
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve ticket capacity data", error = ex.Message });
            }
        }

        /// <summary>
        /// Tab 2: Get Stripe revenue analysis for event
        /// GET: api/Events/{eventId}/stripe-revenue
        /// </summary>
        [Authorize(Roles = "Organizer")]
        [HttpGet("{eventId}/stripe-revenue")]
        public async Task<ActionResult<EventBooking.API.DTOs.StripeRevenueAnalysisDTO>> GetStripeRevenue(int eventId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return BadRequest(new { message = "Authentication error" });
            }

            // Verify organizer owns this event
            var eventItem = await _context.Events
                .Include(e => e.Organizer)
                .Where(e => e.Id == eventId && e.Organizer.UserId == userId)
                .FirstOrDefaultAsync();

            if (eventItem == null)
            {
                return NotFound(new { message = "Event not found or access denied" });
            }

            // Call private helper method that contains the business logic
            var result = await GetStripeRevenueDataAsync(eventId);
            
            if (result == null)
            {
                return StatusCode(500, new { message = "Failed to retrieve Stripe revenue data" });
            }

            return Ok(result);
        }

        /// <summary>
        /// Tab 3: Get organizer revenue analysis for event
        /// GET: api/Events/{eventId}/organizer-revenue
        /// </summary>
        [Authorize(Roles = "Organizer")]
        [HttpGet("{eventId}/organizer-revenue")]
        public async Task<ActionResult<EventBooking.API.DTOs.OrganizerRevenueDTO>> GetOrganizerRevenue(int eventId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return BadRequest(new { message = "Authentication error" });
            }

            // Verify organizer owns this event
            var eventExists = await _context.Events
                .Include(e => e.Organizer)
                .Where(e => e.Id == eventId && e.Organizer.UserId == userId)
                .FirstOrDefaultAsync();

            if (eventExists == null)
            {
                return NotFound(new { message = "Event not found or access denied" });
            }

            // Call private helper method that contains the business logic
            var result = await GetOrganizerRevenueDataAsync(eventId);
            
            if (result == null)
            {
                return StatusCode(500, new { message = "Failed to retrieve organizer revenue data" });
            }

            return Ok(result);
        }

        // ========================================================================================
        // PRIVATE HELPER METHODS - Extracted business logic without HTTP concerns
        // These methods are called internally by GetTicketCapacity to avoid ActionResult issues
        // ========================================================================================

        /// <summary>
        /// Private helper: Get Stripe revenue data without HTTP wrapper
        /// Returns DTO directly (nullable) instead of ActionResult
        /// </summary>
        private async Task<EventBooking.API.DTOs.StripeRevenueAnalysisDTO?> GetStripeRevenueDataAsync(int eventId)
        {
            try
            {
                // Get event details (no authorization check - caller already validated)
                var eventItem = await _context.Events.FindAsync(eventId);
                if (eventItem == null) return null;

                // Get Stripe configuration
                var stripeSecretKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
                if (string.IsNullOrEmpty(stripeSecretKey))
                {
                    var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                    stripeSecretKey = configuration["Stripe:SecretKey"];
                }

                if (string.IsNullOrEmpty(stripeSecretKey)) return null;

                // Get event booking date range from database
                DateTime? firstBookingDate = null;
                try
                {
                    var firstBooking = await _context.Bookings
                        .Where(b => b.EventId == eventId)
                        .MinAsync(b => (DateTime?)b.CreatedAt);
                    
                    if (firstBooking.HasValue)
                    {
                        firstBookingDate = firstBooking.Value.Date;
                    }
                }
                catch
                {
                    // If no bookings found, firstBookingDate remains null
                }

                // Initialize Stripe
                Stripe.StripeConfiguration.ApiKey = stripeSecretKey;
                var sessionService = new Stripe.Checkout.SessionService();

                // Fetch checkout sessions with date filter if available
                var allSessions = new List<Stripe.Checkout.Session>();
                var hasMore = true;
                string? startingAfter = null;
                const int maxSessionsFallback = 1000;

                while (hasMore)
                {
                    var options = new Stripe.Checkout.SessionListOptions
                    {
                        Limit = 100,
                        StartingAfter = startingAfter
                    };

                    if (firstBookingDate.HasValue)
                    {
                        options.Created = new Stripe.DateRangeOptions
                        {
                            GreaterThanOrEqual = firstBookingDate.Value
                        };
                    }

                    var sessionList = await sessionService.ListAsync(options);
                    allSessions.AddRange(sessionList.Data);

                    hasMore = sessionList.HasMore;
                    if (hasMore && sessionList.Data.Any())
                    {
                        startingAfter = sessionList.Data.Last().Id;
                        
                        if (!firstBookingDate.HasValue && allSessions.Count >= maxSessionsFallback)
                        {
                            break;
                        }
                    }
                    else
                    {
                        hasMore = false;
                    }
                }

                // Filter sessions by event title and paid status
                var eventTitle = eventItem.Title;
                var relevantSessions = allSessions
                    .Where(s => s.Metadata != null && 
                               s.Metadata.ContainsKey("eventTitle") && 
                               s.Metadata["eventTitle"].Equals(eventTitle, StringComparison.OrdinalIgnoreCase) &&
                               s.PaymentStatus == "paid")
                    .ToList();

                // Analyze ticket types
                var ticketTypes = new Dictionary<string, dynamic>();
                var totalStripeRevenue = 0m;
                var totalTicketRevenue = 0m;

                foreach (var session in relevantSessions)
                {
                    totalStripeRevenue += (decimal)session.AmountTotal.GetValueOrDefault() / 100;

                    if (session.Metadata != null && session.Metadata.ContainsKey("ticketDetails"))
                    {
                        try
                        {
                            var ticketDetailsJson = session.Metadata["ticketDetails"];
                            
                            using var document = JsonDocument.Parse(ticketDetailsJson);
                            var ticketDetails = document.RootElement;

                            if (ticketDetails.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var ticketElement in ticketDetails.EnumerateArray())
                                {
                                    if (ticketElement.TryGetProperty("Type", out var typeProperty) &&
                                        ticketElement.TryGetProperty("Quantity", out var quantityProperty) &&
                                        ticketElement.TryGetProperty("UnitPrice", out var unitPriceProperty))
                                    {
                                        var type = typeProperty.GetString() ?? "Unknown";
                                        var quantity = quantityProperty.GetInt32();
                                        var unitPrice = unitPriceProperty.GetDecimal();
                                        var revenue = quantity * unitPrice;

                                        totalTicketRevenue += revenue;

                                        if (!ticketTypes.ContainsKey(type))
                                        {
                                            ticketTypes[type] = new
                                            {
                                                Revenue = 0m,
                                                Quantity = 0,
                                                Transactions = 0
                                            };
                                        }

                                        var existing = ticketTypes[type];
                                        ticketTypes[type] = new
                                        {
                                            Revenue = existing.Revenue + revenue,
                                            Quantity = existing.Quantity + quantity,
                                            Transactions = existing.Transactions + 1
                                        };
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error parsing session {session.Id}: {ex.Message}");
                        }
                    }
                }

                // Create pricing tier analysis
                var priceGroups = new Dictionary<decimal, StripePricingTierDTO>();

                foreach (var ticketType in ticketTypes)
                {
                    var type = ticketType.Key;
                    var data = ticketType.Value;
                    var avgPrice = Math.Round(data.Revenue / data.Quantity, 0);

                    if (!priceGroups.ContainsKey(avgPrice))
                    {
                        priceGroups[avgPrice] = new StripePricingTierDTO
                        {
                            TicketPrice = avgPrice,
                            Revenue = 0,
                            Quantity = 0,
                            SeatCombinations = 0,
                            Transactions = 0,
                            RevenuePercentage = 0
                        };
                    }

                    priceGroups[avgPrice].Revenue += data.Revenue;
                    priceGroups[avgPrice].Quantity += data.Quantity;
                    priceGroups[avgPrice].SeatCombinations += 1;
                    priceGroups[avgPrice].Transactions += data.Transactions;
                }

                // Calculate percentages
                foreach (var priceGroup in priceGroups.Values)
                {
                    if (totalTicketRevenue > 0)
                    {
                        priceGroup.RevenuePercentage = Math.Round((priceGroup.Revenue / totalTicketRevenue) * 100, 1);
                    }
                }

                // Calculate totals
                var totalTickets = priceGroups.Values.Sum(p => p.Quantity);
                var totalTransactions = relevantSessions.Count;
                var averagePrice = totalTickets > 0 ? Math.Round(totalTicketRevenue / totalTickets, 2) : 0;

                return new EventBooking.API.DTOs.StripeRevenueAnalysisDTO
                {
                    EventId = eventId,
                    EventTitle = eventTitle,
                    PricingTiers = priceGroups.Values.Where(p => p.TicketPrice > 0).OrderByDescending(p => p.TicketPrice).ToList(),
                    TotalStripeRevenue = totalTicketRevenue,
                    TotalStripeTickets = totalTickets,
                    TotalStripeTransactions = totalTransactions,
                    AverageTicketPrice = averagePrice,
                    AnalysisDate = DateTime.UtcNow,
                    SessionsFetched = allSessions.Count,
                    AnalysisMethod = firstBookingDate.HasValue 
                        ? $"Database-driven (from {firstBookingDate.Value:yyyy-MM-dd} 00:00:00 - midnight)" 
                        : $"Fallback mode (recent {maxSessionsFallback} sessions)"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetStripeRevenueDataAsync: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Private helper: Get organizer revenue data without HTTP wrapper
        /// Returns DTO directly (nullable) instead of ActionResult
        /// </summary>
        private async Task<EventBooking.API.DTOs.OrganizerRevenueDTO?> GetOrganizerRevenueDataAsync(int eventId)
        {
            try
            {
                // Get event details (no authorization check - caller already validated)
                var eventItem = await _context.Events.FindAsync(eventId);
                if (eventItem == null) return null;

                // Get organizer ticket payment data
                var organizerPayments = await _context.OrganizerTicketPayments
                    .Where(otp => otp.EventId == eventId)
                    .GroupBy(otp => new { otp.TicketTypeId, otp.TicketPrice })
                    .Select(g => new
                    {
                        TicketTypeId = g.Key.TicketTypeId,
                        TicketPrice = g.Key.TicketPrice,
                        IssuedTickets = g.Count(),
                        PaidTickets = g.Count(otp => otp.IsPaidToOrganizer == true),
                        UnpaidTickets = g.Count(otp => otp.IsPaidToOrganizer == false),
                        PaidRevenue = g.Where(otp => otp.IsPaidToOrganizer == true).Sum(otp => otp.TicketPrice),
                        UnpaidRevenue = g.Where(otp => otp.IsPaidToOrganizer == false).Sum(otp => otp.TicketPrice)
                    })
                    .ToListAsync();

                // Calculate total transaction count (unique combinations of customer and booking)
                var totalTransactions = await _context.OrganizerTicketPayments
                    .Where(otp => otp.EventId == eventId)
                    .GroupBy(otp => new { otp.BookingLineItemId, otp.CustomerEmail, otp.CustomerFirstName, otp.CustomerLastName })
                    .CountAsync();

                // Get ticket type details
                var ticketTypes = await _context.TicketTypes
                    .Where(tt => tt.EventId == eventId)
                    .Select(tt => new { tt.Id, tt.Name, tt.Color })
                    .ToListAsync();

                // Combine data
                var organizerTicketTypes = organizerPayments.Select(op =>
                {
                    var ticketType = ticketTypes.FirstOrDefault(tt => tt.Id == op.TicketTypeId);
                    var totalRevenue = op.PaidRevenue + op.UnpaidRevenue;

                    return new OrganizerTicketTypeRevenueDTO
                    {
                        TicketTypeId = op.TicketTypeId,
                        TicketTypeName = ticketType?.Name ?? "Unknown",
                        TicketPrice = op.TicketPrice,
                        IssuedTickets = op.IssuedTickets,
                        PaidTickets = op.PaidTickets,
                        UnpaidTickets = op.UnpaidTickets,
                        TotalRevenue = totalRevenue,
                        PaidRevenue = op.PaidRevenue,
                        UnpaidRevenue = op.UnpaidRevenue,
                        PaymentPercentage = op.IssuedTickets > 0 
                            ? Math.Round((decimal)op.PaidTickets / op.IssuedTickets * 100, 1)
                            : 0
                    };
                }).ToList();

                // Sort by ticket price descending (most expensive first) and filter out $0 tickets
                organizerTicketTypes = organizerTicketTypes
                    .Where(ott => ott.TicketPrice > 0)
                    .OrderByDescending(ott => ott.TicketPrice)
                    .ToList();

                // Calculate summary totals
                var totalIssued = organizerTicketTypes.Sum(ott => ott.IssuedTickets);
                var totalPaid = organizerTicketTypes.Sum(ott => ott.PaidTickets);
                var totalUnpaid = organizerTicketTypes.Sum(ott => ott.UnpaidTickets);
                var totalOrganizerRevenue = organizerTicketTypes.Sum(ott => ott.TotalRevenue);
                var paidRevenue = organizerTicketTypes.Sum(ott => ott.PaidRevenue);
                var unpaidRevenue = organizerTicketTypes.Sum(ott => ott.UnpaidRevenue);
                var paymentCompletionRate = totalIssued > 0 
                    ? Math.Round((decimal)totalPaid / totalIssued * 100, 1)
                    : 0;

                return new EventBooking.API.DTOs.OrganizerRevenueDTO
                {
                    EventId = eventId,
                    EventTitle = eventItem.Title ?? "Unknown Event",
                    TicketTypes = organizerTicketTypes,
                    TotalIssued = totalIssued,
                    TotalPaid = totalPaid,
                    TotalUnpaid = totalUnpaid,
                    TotalTransactions = totalTransactions, // Add transaction count
                    TotalOrganizerRevenue = totalOrganizerRevenue,
                    PaidOrganizerRevenue = paidRevenue,
                    UnpaidOrganizerRevenue = unpaidRevenue,
                    OverallPaymentPercentage = paymentCompletionRate
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetOrganizerRevenueDataAsync: {ex.Message}");
                return null;
            }
        }

        // ========================================================================================
        // END OF PRIVATE HELPER METHODS
        // ========================================================================================

        /// <summary>
        /// Tab 4: Get complete revenue summary combining all data sources
        /// GET: api/Events/{eventId}/revenue-summary
        /// </summary>
        [Authorize(Roles = "Organizer")]
        [HttpGet("{eventId}/revenue-summary")]
        public async Task<ActionResult<EventBooking.API.DTOs.RevenueSummaryDTO>> GetRevenueSummary(int eventId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                return BadRequest(new { message = "Authentication error" });
            }

            // Verify organizer owns this event
            var eventItem = await _context.Events
                .Include(e => e.Organizer)
                .Where(e => e.Id == eventId && e.Organizer.UserId == userId)
                .FirstOrDefaultAsync();

            if (eventItem == null)
            {
                return NotFound(new { message = "Event not found or access denied" });
            }

            try
            {
                // 🎯 REUSE Tab 02 and Tab 03 data instead of recalculating
                var stripeRevenueData = await GetStripeRevenueDataAsync(eventId);
                var organizerRevenueData = await GetOrganizerRevenueDataAsync(eventId);

                // Get all ticket types for this event (exclude $0 tickets)
                var ticketTypes = await _context.TicketTypes
                    .Where(tt => tt.EventId == eventId && tt.Price > 0)
                    .ToListAsync();

                // Calculate capacity for each type
                var ticketTypeData = new List<(int Id, string Name, decimal Price, int TotalCapacity)>();
                
                foreach (var tt in ticketTypes)
                {
                    // Determine total capacity based on event type
                    int totalCapacity;
                    if (tt.MaxTickets.HasValue)
                    {
                        // General Admission or Standing tickets
                        totalCapacity = tt.MaxTickets.Value;
                    }
                    else
                    {
                        // Seated tickets (count Seat records, excluding Unavailable seats)
                        totalCapacity = await _context.Seats
                            .Where(s => s.TicketTypeId == tt.Id && s.EventId == eventId && s.Status != SeatStatus.Unavailable)
                            .CountAsync();
                    }
                    
                    ticketTypeData.Add((tt.Id, tt.Name ?? "Unknown", tt.Price, totalCapacity));
                }

                // Calculate Max Possible Revenue
                var maxPossibleRevenue = ticketTypeData.Sum(tt => tt.Price * tt.TotalCapacity);

                // Create Panel 1: Max Possible Revenue
                var maxRevenueItems = ticketTypeData.Select(ttData => new RevenueBreakdownItemDTO
                {
                    TicketTypeName = ttData.Name,
                    TicketPrice = ttData.Price,
                    Quantity = ttData.TotalCapacity,
                    Revenue = ttData.Price * ttData.TotalCapacity,
                    FormattedLine = $"{ttData.Name}: ${ttData.Price:F2} × {ttData.TotalCapacity} = ${ttData.Price * ttData.TotalCapacity:F2}"
                }).OrderByDescending(item => item.TicketPrice).ToList();

                // Create Panel 2: KiwiLanka Revenue (from Tab 02 - Stripe data)
                var kiwiLankaItems = new List<RevenueBreakdownItemDTO>();
                var stripeRevenue = 0m;
                var stripeTicketCount = 0;

                if (stripeRevenueData != null && stripeRevenueData.PricingTiers != null)
                {
                    // Use Tab 02 pricing tiers directly
                    kiwiLankaItems = stripeRevenueData.PricingTiers.Select(tier => new RevenueBreakdownItemDTO
                    {
                        TicketTypeName = $"Ticket at ${tier.TicketPrice:F2}",
                        TicketPrice = tier.TicketPrice,
                        Quantity = tier.Quantity,
                        Revenue = tier.Revenue,
                        FormattedLine = $"${tier.TicketPrice:F2} tickets: ${tier.TicketPrice:F2} × {tier.Quantity} = ${tier.Revenue:F2}"
                    }).OrderByDescending(item => item.TicketPrice).ToList();

                    stripeRevenue = stripeRevenueData.TotalStripeRevenue;
                    stripeTicketCount = stripeRevenueData.TotalStripeTickets;
                }

                // Create Panel 3: Organizer Revenue (from Tab 03)
                var organizerItems = new List<RevenueBreakdownItemDTO>();
                var organizerRevenue = 0m;
                var organizerTicketCount = 0;

                if (organizerRevenueData != null && organizerRevenueData.TicketTypes != null)
                {
                    organizerItems = organizerRevenueData.TicketTypes.Select(ott => new RevenueBreakdownItemDTO
                    {
                        TicketTypeName = ott.TicketTypeName,
                        TicketPrice = ott.TicketPrice,
                        Quantity = ott.IssuedTickets,
                        Revenue = ott.TotalRevenue,
                        FormattedLine = $"{ott.TicketTypeName}: ${ott.TicketPrice:F2} × {ott.IssuedTickets} = ${ott.TotalRevenue:F2}"
                    }).Where(item => item.Quantity > 0).OrderByDescending(item => item.TicketPrice).ToList();

                    organizerRevenue = organizerRevenueData.TotalOrganizerRevenue;
                    organizerTicketCount = organizerRevenueData.TotalIssued;
                }

                // Calculate combined totals
                var totalRevenue = stripeRevenue + organizerRevenue;
                var totalSold = stripeTicketCount + organizerTicketCount;
                var combinedTotalCapacity = ticketTypeData.Sum(tt => tt.TotalCapacity);
                var remainingCapacityValue = maxPossibleRevenue - totalRevenue;
                
                var utilizationPercentage = combinedTotalCapacity > 0 
                    ? Math.Round((decimal)totalSold / combinedTotalCapacity * 100, 1) 
                    : 0;

                var kiwiLankaPercentage = totalRevenue > 0 
                    ? Math.Round(stripeRevenue / totalRevenue * 100, 1) 
                    : 0;
                var organizerPercentage = totalRevenue > 0 
                    ? Math.Round(organizerRevenue / totalRevenue * 100, 1) 
                    : 0;

                var result = new EventBooking.API.DTOs.RevenueSummaryDTO
                {
                    EventId = eventId,
                    EventTitle = eventItem.Title ?? "Unknown Event",
                    MaxPossibleRevenuePanel = new RevenueCapacityPanelDTO
                    {
                        PanelTitle = "Max Possible Revenue",
                        BreakdownItems = maxRevenueItems,
                        TotalRevenue = maxPossibleRevenue,
                        DisplayCurrency = "NZD"
                    },
                    KiwiLankaRevenuePanel = new RevenueCapacityPanelDTO
                    {
                        PanelTitle = "KiwiLanka Revenue (Stripe)",
                        BreakdownItems = kiwiLankaItems,
                        TotalRevenue = stripeRevenue,
                        DisplayCurrency = "NZD"
                    },
                    OrganizerRevenuePanel = new RevenueCapacityPanelDTO
                    {
                        PanelTitle = "Organizer Direct Sales",
                        BreakdownItems = organizerItems,
                        TotalRevenue = organizerRevenue,
                        DisplayCurrency = "NZD"
                    },
                    CombinedSummary = new RevenueSummaryTotalsDTO
                    {
                        KiwiLankaRevenue = stripeRevenue,
                        KiwiLankaPercentage = kiwiLankaPercentage,
                        OrganizerRevenue = organizerRevenue,
                        OrganizerPercentage = organizerPercentage,
                        TotalRevenue = totalRevenue,
                        RemainingCapacityValue = remainingCapacityValue,
                        OverallEventUtilization = utilizationPercentage
                    },
                    GeneratedAt = DateTime.UtcNow
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Failed to retrieve revenue summary data", error = ex.Message });
            }
        }

        private bool EventExists(int id)
        {
            return _context.Events.Any(e => e.Id == id);
        }
    }
}
