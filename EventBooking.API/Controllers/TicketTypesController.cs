using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventBooking.API.Data;
using EventBooking.API.Models;
using EventBooking.API.DTOs;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace EventBooking.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketTypesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TicketTypesController> _logger;

        public TicketTypesController(AppDbContext context, ILogger<TicketTypesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/TicketTypes/event/5
        [HttpGet("event/{eventId}")]
        public async Task<ActionResult<IEnumerable<TicketType>>> GetTicketTypesForEvent(int eventId)
        {
            var ticketTypes = await _context.TicketTypes
                .Where(t => t.EventId == eventId)
                .ToListAsync();

            return ticketTypes;
        }

        // POST: api/TicketTypes
        [HttpPost]
        public async Task<ActionResult<TicketType>> CreateTicketType(TicketTypeCreateDTO dto)
        {
            // Get the event to check its venue and seating info
            var eventEntity = await _context.Events
                .Include(e => e.Venue)
                .FirstOrDefaultAsync(e => e.Id == dto.EventId);

            if (eventEntity == null)
            {
                return NotFound("Event not found");
            }

            var ticketType = new TicketType
            {
                Type = dto.Type,
                Price = dto.Price,
                Description = dto.Description,
                EventId = dto.EventId
            };

            // If venue has allocated seating and row assignments are provided
            if (eventEntity.SeatSelectionMode == SeatSelectionMode.EventHall && 
                eventEntity.Venue != null && 
                dto.SeatRows?.Any() == true)
            {
                // Store seat row assignments as JSON
                ticketType.SeatRowAssignments = JsonSerializer.Serialize(dto.SeatRows);

                // Mark seats in unassigned rows as Reserved
                var assignedRows = dto.SeatRows
                    .SelectMany(sr => 
                        Enumerable.Range(
                            sr.RowStart[0] - 'A', 
                            sr.RowEnd[0] - sr.RowStart[0] + 1
                        )
                        .Select(i => ((char)('A' + i)).ToString())
                    )
                    .ToHashSet();

                // Get all rows in the venue
                var allRows = Enumerable.Range(0, eventEntity.Venue.NumberOfRows)
                    .Select(i => ((char)('A' + i)).ToString())
                    .ToList();

                // Find unassigned rows
                var unassignedRows = allRows.Except(assignedRows).ToList();

                // Mark seats in unassigned rows as Reserved
                var seatsToUpdate = await _context.Seats
                    .Where(s => s.EventId == dto.EventId && unassignedRows.Contains(s.Row))
                    .ToListAsync();

                foreach (var seat in seatsToUpdate)
                {
                    seat.Status = SeatStatus.Reserved;
                }
            }

            _context.TicketTypes.Add(ticketType);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTicketTypesForEvent), new { eventId = ticketType.EventId }, ticketType);
        }

        // PUT: api/TicketTypes/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTicketType(int id, TicketType ticketType)
        {
            if (id != ticketType.Id)
            {
                return BadRequest();
            }

            _context.Entry(ticketType).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TicketTypeExists(id))
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

        // DELETE: api/TicketTypes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTicketType(int id)
        {
            var ticketType = await _context.TicketTypes.FindAsync(id);
            if (ticketType == null)
            {
                return NotFound();
            }

            _context.TicketTypes.Remove(ticketType);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool TicketTypeExists(int id)
        {
            return _context.TicketTypes.Any(e => e.Id == id);
        }
    }
}
