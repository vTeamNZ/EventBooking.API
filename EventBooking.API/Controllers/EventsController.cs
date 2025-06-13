using EventBooking.API.Data;
using EventBooking.API.DTOs;
using EventBooking.API.Models;
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
    //[AllowAnonymous]
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class EventsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EventsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Events
        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Event>>> GetEvents()
        {
            return await _context.Events.ToListAsync();
        }

        // GET: api/Events/5
        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult<Event>> GetEvent(int id)
        {
            var @event = await _context.Events.FindAsync(id);

            if (@event == null)
            {
                return NotFound();
            }

            return @event;
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> CreateEvent(EventCreateDTO dto)
        {
            // For demo/testing purposes, assign a static organizerId
            var defaultOrganizer = await _context.Organizers.FirstOrDefaultAsync();
            if (defaultOrganizer == null)
                return BadRequest("No organizer available. Please create one first.");

            var newEvent = new Event
            {
                Title = dto.Title,
                Description = dto.Description,
                Date = dto.Date,
                Location = dto.Location,
                Price = dto.Price,
                Capacity = dto.Capacity,
                OrganizerId = defaultOrganizer.Id,
                ImageUrl = dto.imageUrl
            };

            _context.Events.Add(newEvent);
            await _context.SaveChangesAsync();

            return Ok(newEvent);
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

            _context.Events.Remove(@event);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/Events/seed-test-data
        [AllowAnonymous]
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
        }
        
        private bool EventExists(int id)
        {
            return _context.Events.Any(e => e.Id == id);
        }
    }
}
