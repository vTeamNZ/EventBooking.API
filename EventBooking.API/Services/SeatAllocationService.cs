using EventBooking.API.Data;
using EventBooking.API.Models;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.API.Services
{
    public interface ISeatAllocationService
    {
        Task UpdateSeatAllocationsAsync(int eventId);
        Task MarkUnallocatedSeatsAsReservedAsync(int eventId);
    }

    public class SeatAllocationService : ISeatAllocationService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SeatAllocationService> _logger;

        public SeatAllocationService(AppDbContext context, ILogger<SeatAllocationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Updates seat allocations for an event based on its ticket types
        /// Marks unallocated rows as Reserved and allocated rows as Available
        /// </summary>
        /// <param name="eventId">The event ID to update seat allocations for</param>
        public async Task UpdateSeatAllocationsAsync(int eventId)
        {
            _logger.LogInformation("Updating seat allocations for event {EventId}", eventId);

            var eventEntity = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.TicketTypes)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventEntity == null)
            {
                _logger.LogWarning("Event {EventId} not found", eventId);
                return;
            }

            // Only process events with allocated seating
            if (eventEntity.SeatSelectionMode != SeatSelectionMode.EventHall || eventEntity.Venue == null)
            {
                _logger.LogInformation("Event {EventId} does not use allocated seating, skipping seat allocation update", eventId);
                return;
            }

            // Dictionary to store ticket type assignments: row -> ticket type
            var rowToTicketType = new Dictionary<string, TicketType>();
            
            // Process ticket types with row assignments first
            foreach (var ticketType in eventEntity.TicketTypes.Where(tt => !string.IsNullOrEmpty(tt.SeatRowAssignments)))
            {
                try
                {
                    var rowAssignments = System.Text.Json.JsonSerializer.Deserialize<List<SeatRowAssignment>>(ticketType.SeatRowAssignments);
                    if (rowAssignments != null)
                    {
                        foreach (var assignment in rowAssignments)
                        {
                            if (!string.IsNullOrEmpty(assignment.RowStart) && !string.IsNullOrEmpty(assignment.RowEnd))
                            {
                                var startChar = assignment.RowStart[0];
                                var endChar = assignment.RowEnd[0];
                                
                                for (char row = startChar; row <= endChar; row++)
                                {
                                    rowToTicketType[row.ToString()] = ticketType;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse seat row assignments for ticket type {TicketTypeId}", ticketType.Id);
                }
            }

            // Get all seats for this event
            var allSeats = await _context.Seats
                .Where(s => s.EventId == eventId)
                .ToListAsync();

            var updatedSeats = 0;
            var unallocatedRows = new HashSet<string>();

            // Update all seats based on their row assignment
            foreach (var seat in allSeats)
            {
                if (rowToTicketType.TryGetValue(seat.Row, out var ticketType))
                {
                    // Row is allocated to a ticket type
                    bool needsUpdate = false;

                    // Update ticket type and price
                    if (seat.TicketTypeId != ticketType.Id)
                    {
                        seat.TicketTypeId = ticketType.Id;
                        seat.Price = ticketType.Price;
                        needsUpdate = true;
                    }

                    // Set status based on ticket type
                    var isGeneralAdmission = ticketType.Type.Equals("General", StringComparison.OrdinalIgnoreCase) || 
                                           ticketType.Name.Equals("General", StringComparison.OrdinalIgnoreCase);
                    
                    var newStatus = isGeneralAdmission ? SeatStatus.Reserved : SeatStatus.Available;
                    if (seat.Status != newStatus || seat.IsReserved != isGeneralAdmission)
                    {
                        seat.Status = newStatus;
                        seat.IsReserved = isGeneralAdmission;
                        needsUpdate = true;
                    }

                    if (needsUpdate)
                    {
                        updatedSeats++;
                    }
                }
                else
                {
                    // Row is not allocated to any ticket type - mark as unallocated
                    unallocatedRows.Add(seat.Row);
                    
                    if (seat.Status != SeatStatus.Reserved || !seat.IsReserved)
                    {
                        seat.Status = SeatStatus.Reserved;
                        seat.IsReserved = true;
                        updatedSeats++;
                    }
                }
            }

            if (updatedSeats > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Updated {UpdatedSeats} seats for event {EventId}. Allocated rows: {AllocatedRows}, Unallocated rows: {UnallocatedRows}", 
                    updatedSeats, eventId, rowToTicketType.Keys.Count, unallocatedRows.Count);
            }
            else
            {
                _logger.LogInformation("No seat updates needed for event {EventId}", eventId);
            }
        }

        /// <summary>
        /// Marks all unallocated seats as reserved for an event
        /// This is called when an event is created but before ticket types are assigned
        /// </summary>
        /// <param name="eventId">The event ID</param>
        public async Task MarkUnallocatedSeatsAsReservedAsync(int eventId)
        {
            _logger.LogInformation("Marking unallocated seats as reserved for event {EventId}", eventId);

            var eventEntity = await _context.Events
                .Include(e => e.Venue)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventEntity == null || eventEntity.SeatSelectionMode != SeatSelectionMode.EventHall)
            {
                return;
            }

            // Initially mark all seats as reserved until ticket types are assigned
            var allSeats = await _context.Seats
                .Where(s => s.EventId == eventId)
                .ToListAsync();

            foreach (var seat in allSeats)
            {
                seat.Status = SeatStatus.Reserved;
                seat.IsReserved = true;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Marked {SeatCount} seats as reserved for event {EventId}", allSeats.Count, eventId);
        }
    }

    // DTO class for seat row assignments
    public class SeatRowAssignment
    {
        public string RowStart { get; set; } = string.Empty;
        public string RowEnd { get; set; } = string.Empty;
        public int MaxTickets { get; set; }
    }
}
