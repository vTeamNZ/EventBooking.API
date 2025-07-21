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

        public EventsController(
            AppDbContext context, 
            IEventStatusService eventStatusService, 
            IImageService imageService,
            ISeatCreationService seatCreationService)
        {
            _context = context;
            _eventStatusService = eventStatusService;
            _imageService = imageService;
            _seatCreationService = seatCreationService;
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
            // First try exact match
            var exactMatch = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Organizer)
                .Include(e => e.TicketTypes)
                .FirstOrDefaultAsync(e => e.Title.ToLower().Replace(" ", "-").Replace("'", "").Replace("\"", "").Replace("&", "and") == title.ToLower());

            if (exactMatch != null)
            {
                return exactMatch;
            }

            // Fallback to contains search for partial matches
            var partialMatch = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Organizer)
                .Include(e => e.TicketTypes)
                .Where(e => e.Title.Contains(title))
                .FirstOrDefaultAsync();

            if (partialMatch == null)
            {
                return NotFound();
            }

            return partialMatch;
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

                // Automatically create seats if we have a venue and are using EventHall mode
                int seatsCreated = 0;
                
                // Log the event data to debug
                logger.LogInformation("Event created with SeatSelectionMode: {SeatSelectionMode} (value: {ModeValue}), VenueId: {VenueId}", 
                    newEvent.SeatSelectionMode, (int)newEvent.SeatSelectionMode, newEvent.VenueId);
                
                if (newEvent.VenueId.HasValue && newEvent.SeatSelectionMode == SeatSelectionMode.EventHall)
                {
                    logger.LogInformation("Creating seats for event {EventId} with venue {VenueId}", 
                        newEvent.Id, newEvent.VenueId.Value);
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

        private bool EventExists(int id)
        {
            return _context.Events.Any(e => e.Id == id);
        }
    }
}
