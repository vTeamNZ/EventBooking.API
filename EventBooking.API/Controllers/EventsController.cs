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
    [Route("api/[controller]")]
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

            // Get all events with venue and ticket type information included
            var allEvents = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Organizer)
                .Include(e => e.TicketTypes)
                .Where(e => e.Date.HasValue)
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
            var events = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.Organizer)
                .Where(e => e.Title.Contains(title))
                .FirstOrDefaultAsync();

            if (events == null)
            {
                return NotFound();
            }

            return events;
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
                    // Use venue's supported seat selection mode if available, otherwise use DTO value
                    SeatSelectionMode = venue?.SeatSelectionMode ?? dto.SeatSelectionMode,
                    StagePosition = dto.StagePosition,
                    VenueId = dto.VenueId,
                    IsActive = false // Events are inactive by default until approved by admin
                };

                _context.Events.Add(newEvent);
                await _context.SaveChangesAsync();

                // Automatically create seats if we have a venue and are using EventHall mode
                int seatsCreated = 0;
                
                // Log the event data to debug
                logger.LogInformation("Event created with SeatSelectionMode: {SeatSelectionMode} (value: {ModeValue}), VenueId: {VenueId}", 
                    newEvent.SeatSelectionMode, (int)newEvent.SeatSelectionMode, newEvent.VenueId);
                
                // Force EventHall mode if we have a venue with allocated seating
                if (newEvent.VenueId.HasValue && dto.SeatSelectionMode.ToString() == "1")
                {
                    newEvent.SeatSelectionMode = SeatSelectionMode.EventHall;
                    await _context.SaveChangesAsync();
                    logger.LogInformation("Forced SeatSelectionMode to EventHall");
                }
                
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
                    message = "Event created successfully. It will be visible after admin approval.",
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

            logger.LogInformation("GetOrganizerEvents: Found {Count} events for organizer {OrganizerId}", events.Count, organizer.Id);
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

        // POST: api/Events/seed-test-data
        /*[AllowAnonymous]
        [HttpPost("seed-test-data")]
        public async Task<IActionResult> SeedTestData()
        {
            try
            {
                // Create event if it doesn't exist
                var eventExists = await _context.Events.AnyAsync(e => e.Id == 1);
                if (!eventExists)
                {
                    var newEvent = new Event
                    {
                        Title = "Sri Lankan Cultural Night 2025",
                        Description = "A night of traditional Sri Lankan music, dance, and cuisine",
                        Date = new DateTime(2025, 7, 20, 18, 0, 0),
                        Location = "Auckland Town Hall",
                        IsActive = true,
                        OrganizerId = 1,
                        ImageUrl = "events/1.jpg",
                        Capacity = 200
                    };
                    _context.Events.Add(newEvent);
                    await _context.SaveChangesAsync();
                }

                // Add ticket types
                var ticketTypes = new[]
                {
                    new TicketType 
                    { 
                        EventId = 1, 
                        Type = "Regular", 
                        Price = 50.00m, 
                        Description = "Regular entry ticket" 
                    },
                    new TicketType 
                    { 
                        EventId = 1, 
                        Type = "VIP", 
                        Price = 100.00m, 
                        Description = "VIP ticket with special benefits including priority seating" 
                    },
                    new TicketType 
                    { 
                        EventId = 1, 
                        Type = "Student", 
                        Price = 35.00m, 
                        Description = "Student discount ticket (valid student ID required)" 
                    }
                };

                foreach (var ticketType in ticketTypes)
                {
                    if (!await _context.TicketTypes.AnyAsync(t => 
                        t.EventId == ticketType.EventId && 
                        t.Type == ticketType.Type))
                    {
                        _context.TicketTypes.Add(ticketType);
                    }
                }

                // Add food items
                var foodItems = new[]
                {
                    new FoodItem 
                    { 
                        EventId = 1, 
                        Name = "Sri Lankan Rice & Curry", 
                        Price = 18.00m, 
                        Description = "Traditional Sri Lankan rice with 3 vegetables, dhal curry, and papadam" 
                    },
                    new FoodItem 
                    { 
                        EventId = 1, 
                        Name = "Kottu Roti", 
                        Price = 20.00m, 
                        Description = "Famous Sri Lankan street food made with chopped roti, vegetables, and your choice of chicken or vegetarian" 
                    },
                    new FoodItem 
                    { 
                        EventId = 1, 
                        Name = "String Hoppers Meal", 
                        Price = 15.00m, 
                        Description = "String hoppers served with dhal curry and coconut sambol" 
                    }
                };

                foreach (var foodItem in foodItems)
                {
                    if (!await _context.FoodItems.AnyAsync(f => 
                        f.EventId == foodItem.EventId && 
                        f.Name == foodItem.Name))
                    {
                        _context.FoodItems.Add(foodItem);
                    }
                }

                await _context.SaveChangesAsync();
                return Ok("Test data seeded successfully!");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error seeding test data: {ex.Message}");
            }
        }*/
        
        private bool EventExists(int id)
        {
            return _context.Events.Any(e => e.Id == id);
        }
    }
}
