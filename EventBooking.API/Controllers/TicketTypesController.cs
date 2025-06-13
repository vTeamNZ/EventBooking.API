using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventBooking.API.Data;
using EventBooking.API.Models;

namespace EventBooking.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketTypesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TicketTypesController(AppDbContext context)
        {
            _context = context;
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
        public async Task<ActionResult<TicketType>> CreateTicketType(TicketType ticketType)
        {
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
