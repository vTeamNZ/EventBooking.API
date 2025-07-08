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

            // Get all ticket types with row assignments
            var ticketTypesWithRows = eventEntity.TicketTypes
                .Where(tt => !string.IsNullOrEmpty(tt.SeatRowAssignments))
                .ToList();

            // Get all allocated rows from ticket types
            var allocatedRows = new HashSet<string>();
            
            foreach (var ticketType in ticketTypesWithRows)
            {
                try
                {
                    // Check if seat row assignments are available before deserializing
                    if (!string.IsNullOrEmpty(ticketType.SeatRowAssignments))
                    {
                        var rowAssignments = System.Text.Json.JsonSerializer.Deserialize<List<SeatRowAssignment>>(ticketType.SeatRowAssignments);
                        if (rowAssignments != null)
                        {
                        foreach (var assignment in rowAssignments)
                        {
                            if (!string.IsNullOrEmpty(assignment.RowStart) && !string.IsNullOrEmpty(assignment.RowEnd))
                            {
                                // Add all rows in the range
                                var startChar = assignment.RowStart[0];
                                var endChar = assignment.RowEnd[0];
                                
                                for (char row = startChar; row <= endChar; row++)
                                {
                                    allocatedRows.Add(row.ToString());
                                }
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

            // Get all rows in the venue
            var allRows = new List<string>();
            var rowCount = eventEntity.Venue.NumberOfRows > 0 ? eventEntity.Venue.NumberOfRows : 10;
            
            for (int i = 0; i < rowCount; i++)
            {
                allRows.Add(((char)('A' + i)).ToString());
            }

            // Find unallocated rows
            var unallocatedRows = allRows.Except(allocatedRows).ToList();

            _logger.LogInformation("Event {EventId}: {AllocatedCount} allocated rows, {UnallocatedCount} unallocated rows", 
                eventId, allocatedRows.Count, unallocatedRows.Count);

            // Get all seats for this event
            var allSeats = await _context.Seats
                .Where(s => s.EventId == eventId)
                .ToListAsync();

            var updatedSeats = 0;

            // Mark seats in allocated rows as Available (if they're not already booked)
            foreach (var seat in allSeats.Where(s => allocatedRows.Contains(s.Row)))
            {
                if (seat.Status == SeatStatus.Reserved && seat.IsReserved)
                {
                    seat.Status = SeatStatus.Available;
                    seat.IsReserved = false;
                    updatedSeats++;
                }
            }

            // Mark seats in unallocated rows as Reserved
            foreach (var seat in allSeats.Where(s => unallocatedRows.Contains(s.Row)))
            {
                if (seat.Status != SeatStatus.Reserved || !seat.IsReserved)
                {
                    seat.Status = SeatStatus.Reserved;
                    seat.IsReserved = true;
                    updatedSeats++;
                }
            }

            if (updatedSeats > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated {UpdatedSeats} seats for event {EventId}", updatedSeats, eventId);
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
