using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventBooking.API.Data;
using EventBooking.API.Models;
using EventBooking.API.DTOs;
using EventBooking.API.Services;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace EventBooking.API.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class TicketTypesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TicketTypesController> _logger;
        private readonly ISeatAllocationService _seatAllocationService;

        public TicketTypesController(
            AppDbContext context, 
            ILogger<TicketTypesController> logger,
            ISeatAllocationService seatAllocationService)
        {
            _context = context;
            _logger = logger;
            _seatAllocationService = seatAllocationService;
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

            // Synchronize Type and Name fields using the same value
            string typeValue = dto.Type ?? "General Admission";
            
            var ticketType = new TicketType
            {
                Type = typeValue,
                Name = typeValue, // Set Name field to same value as Type
                Price = dto.Price,
                Description = dto.Description,
                EventId = dto.EventId,
                Color = dto.Color // Add the color from the DTO
            };

            // If venue has allocated seating and row assignments are provided
            if (eventEntity.SeatSelectionMode == SeatSelectionMode.EventHall && 
                eventEntity.Venue != null && 
                dto.SeatRows?.Any() == true)
            {
                // Store seat row assignments as JSON
                ticketType.SeatRowAssignments = JsonSerializer.Serialize(dto.SeatRows);
            }

            _context.TicketTypes.Add(ticketType);
            await _context.SaveChangesAsync();

            // Update seat allocations using the new service (only if not bulk creating)
            if (eventEntity.SeatSelectionMode == SeatSelectionMode.EventHall && eventEntity.Venue != null)
            {
                await _seatAllocationService.UpdateSeatAllocationsAsync(dto.EventId);
            }

            return CreatedAtAction(nameof(GetTicketTypesForEvent), new { eventId = ticketType.EventId }, ticketType);
        }

        // POST: api/TicketTypes/bulk
        [HttpPost("bulk")]
        public async Task<ActionResult<List<TicketType>>> CreateTicketTypesBulk(List<TicketTypeCreateDTO> dtos)
        {
            if (!dtos.Any())
            {
                return BadRequest("No ticket types provided");
            }

            var eventId = dtos.First().EventId;
            
            // Verify all DTOs are for the same event
            if (dtos.Any(dto => dto.EventId != eventId))
            {
                return BadRequest("All ticket types must be for the same event");
            }

            // Get the event to check its venue and seating info
            var eventEntity = await _context.Events
                .Include(e => e.Venue)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventEntity == null)
            {
                return NotFound("Event not found");
            }

            var createdTicketTypes = new List<TicketType>();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var dto in dtos)
                {
                    // Synchronize Type and Name fields using the same value
                    string typeValue = dto.Type ?? "General Admission";
                    
                    var ticketType = new TicketType
                    {
                        Type = typeValue,
                        Name = typeValue, // Set Name field to same value as Type
                        Price = dto.Price,
                        Description = dto.Description,
                        EventId = dto.EventId,
                        Color = dto.Color // Add the color from the DTO
                    };

                    // If venue has allocated seating and row assignments are provided
                    if (eventEntity.SeatSelectionMode == SeatSelectionMode.EventHall && 
                        eventEntity.Venue != null && 
                        dto.SeatRows?.Any() == true)
                    {
                        // Store seat row assignments as JSON
                        ticketType.SeatRowAssignments = JsonSerializer.Serialize(dto.SeatRows);
                    }

                    _context.TicketTypes.Add(ticketType);
                    createdTicketTypes.Add(ticketType);
                }

                await _context.SaveChangesAsync();

                // Update seat allocations once for all ticket types (if allocated seating)
                if (eventEntity.SeatSelectionMode == SeatSelectionMode.EventHall && eventEntity.Venue != null)
                {
                    await _seatAllocationService.UpdateSeatAllocationsAsync(eventId);
                }

                await transaction.CommitAsync();

                _logger.LogInformation("Successfully created {Count} ticket types for event {EventId}", createdTicketTypes.Count, eventId);
                return Ok(createdTicketTypes);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating bulk ticket types for event {EventId}", eventId);
                return StatusCode(500, "Error creating ticket types");
            }
        }

        // PUT: api/TicketTypes/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTicketType(int id, TicketType ticketType)
        {
            if (id != ticketType.Id)
            {
                return BadRequest();
            }

            // Get the event to check if it uses allocated seating
            var eventEntity = await _context.Events
                .Include(e => e.Venue)
                .FirstOrDefaultAsync(e => e.Id == ticketType.EventId);

            // Synchronize Type and Name fields
            string typeValue = ticketType.Type ?? ticketType.Name ?? "General Admission";
            ticketType.Type = typeValue;
            ticketType.Name = typeValue;

            _context.Entry(ticketType).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();

                // Update seat allocations if this is an allocated seating event
                if (eventEntity?.SeatSelectionMode == SeatSelectionMode.EventHall && eventEntity.Venue != null)
                {
                    await _seatAllocationService.UpdateSeatAllocationsAsync(ticketType.EventId);
                }
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

            var eventId = ticketType.EventId;

            // Get the event to check if it uses allocated seating
            var eventEntity = await _context.Events
                .Include(e => e.Venue)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            _context.TicketTypes.Remove(ticketType);
            await _context.SaveChangesAsync();

            // Update seat allocations if this is an allocated seating event
            if (eventEntity?.SeatSelectionMode == SeatSelectionMode.EventHall && eventEntity.Venue != null)
            {
                await _seatAllocationService.UpdateSeatAllocationsAsync(eventId);
            }

            return NoContent();
        }

        // POST: api/TicketTypes/update-colors (temporary for testing)
        [HttpPost("update-colors")]
        public async Task<ActionResult> UpdateTicketTypeColors()
        {
            try
            {
                var ticketTypes = await _context.TicketTypes.ToListAsync();
                
                foreach (var ticketType in ticketTypes)
                {
                    // Only update if color is missing or is the default blue
                    if (string.IsNullOrEmpty(ticketType.Color) || ticketType.Color == "#3B82F6")
                    {
                        switch (ticketType.Type.ToLower())
                        {
                            case "vip":
                                ticketType.Color = "#FFD700"; // Gold
                                break;
                            case "premium":
                                ticketType.Color = "#C0C0C0"; // Silver
                                break;
                            case "general":
                                ticketType.Color = "#CD7F32"; // Bronze
                                break;
                            case "front":
                            case "front tables":
                                ticketType.Color = "#FF6B6B"; // Red
                                break;
                            case "back":
                            case "back tables":
                                ticketType.Color = "#4ECDC4"; // Teal
                                break;
                            default:
                                // Generate a random color for unknown types
                                var colors = new[] { "#4299E1", "#48BB78", "#ED8936", "#9F7AEA", "#F56565" };
                                var hash = ticketType.Type.GetHashCode();
                                ticketType.Color = colors[Math.Abs(hash) % colors.Length];
                                break;
                        }
                    }
                }
                
                await _context.SaveChangesAsync();
                
                return Ok(new { 
                    message = "Ticket type colors updated successfully",
                    updatedCount = ticketTypes.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ticket type colors");
                return StatusCode(500, "Internal server error");
            }
        }

        // POST: api/TicketTypes/update-seat-allocations/{eventId}
        [HttpPost("update-seat-allocations/{eventId}")]
        public async Task<IActionResult> UpdateSeatAllocations(int eventId)
        {
            try
            {
                await _seatAllocationService.UpdateSeatAllocationsAsync(eventId);
                return Ok(new { message = "Seat allocations updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating seat allocations for event {EventId}", eventId);
                return StatusCode(500, new { message = "Error updating seat allocations" });
            }
        }

        private bool TicketTypeExists(int id)
        {
            return _context.TicketTypes.Any(e => e.Id == id);
        }
    }
}
