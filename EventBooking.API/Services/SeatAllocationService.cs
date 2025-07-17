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

            // Get all seats for this event in a single query with change tracking disabled for performance
            var allSeats = await _context.Seats
                .AsNoTracking()
                .Where(s => s.EventId == eventId)
                .ToListAsync();

            var seatsToUpdate = new List<Seat>();
            var unallocatedRows = new HashSet<string>();

            // Prepare seat updates in memory first
            foreach (var seat in allSeats)
            {
                var seatToUpdate = new Seat
                {
                    Id = seat.Id,
                    EventId = seat.EventId,
                    Row = seat.Row,
                    Number = seat.Number,
                    SeatNumber = seat.SeatNumber,
                    X = seat.X,
                    Y = seat.Y,
                    Width = seat.Width,
                    Height = seat.Height,
                    Price = seat.Price,
                    Status = seat.Status,
                    IsReserved = seat.IsReserved,
                    TicketTypeId = seat.TicketTypeId,
                    TableId = seat.TableId,
                    ReservedBy = seat.ReservedBy,
                    ReservedUntil = seat.ReservedUntil
                };

                bool needsUpdate = false;

                if (rowToTicketType.TryGetValue(seat.Row, out var ticketType))
                {
                    // Row is allocated to a ticket type

                    // Update ticket type and price
                    if (seat.TicketTypeId != ticketType.Id)
                    {
                        seatToUpdate.TicketTypeId = ticketType.Id;
                        seatToUpdate.Price = ticketType.Price;
                        needsUpdate = true;
                    }

                    // Set status based on ticket type
                    var isGeneralAdmission = ticketType.Type.Equals("General", StringComparison.OrdinalIgnoreCase) || 
                                           ticketType.Name.Equals("General", StringComparison.OrdinalIgnoreCase);
                    
                    var newStatus = isGeneralAdmission ? SeatStatus.Reserved : SeatStatus.Available;
                    if (seat.Status != newStatus || seat.IsReserved != isGeneralAdmission)
                    {
                        seatToUpdate.Status = newStatus;
                        seatToUpdate.IsReserved = isGeneralAdmission;
                        needsUpdate = true;
                    }
                }
                else
                {
                    // Row is not allocated to any ticket type - mark as unallocated
                    unallocatedRows.Add(seat.Row);
                    
                    if (seat.Status != SeatStatus.Reserved || !seat.IsReserved)
                    {
                        seatToUpdate.Status = SeatStatus.Reserved;
                        seatToUpdate.IsReserved = true;
                        needsUpdate = true;
                    }
                }

                if (needsUpdate)
                {
                    seatsToUpdate.Add(seatToUpdate);
                }
            }

            // Bulk update all seats that need changes
            if (seatsToUpdate.Count > 0)
            {                    // Use SQL bulk update for better performance
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        // Use raw SQL for bulk updates to avoid Entity Framework overhead
                        if (seatsToUpdate.Count > 0)
                        {
                            // Group updates by operation type for better performance
                            var seatIdsToUpdate = seatsToUpdate.Select(s => s.Id).ToList();
                            var seatUpdates = seatsToUpdate.Select(s => new
                            {
                                Id = s.Id,
                                TicketTypeId = s.TicketTypeId,
                                Price = s.Price,
                                Status = (int)s.Status,
                                IsReserved = s.IsReserved
                            }).ToList();

                            // Create a temporary table for bulk updates
                            var tempTableName = $"#TempSeatUpdates_{Guid.NewGuid():N}";
                            
                            // Create temp table
                            await _context.Database.ExecuteSqlRawAsync($@"
                                CREATE TABLE {tempTableName} (
                                    Id int PRIMARY KEY,
                                    TicketTypeId int,
                                    Price decimal(18,2),
                                    Status int,
                                    IsReserved bit
                                )");

                            // Insert data into temp table in batches
                            const int batchSize = 1000;
                            for (int i = 0; i < seatUpdates.Count; i += batchSize)
                            {
                                var batch = seatUpdates.Skip(i).Take(batchSize);
                                var values = string.Join(", ", batch.Select(s => 
                                    $"({s.Id}, {(s.TicketTypeId?.ToString() ?? "NULL")}, {s.Price}, {s.Status}, {(s.IsReserved ? 1 : 0)})"));
                                
                                await _context.Database.ExecuteSqlRawAsync($@"
                                    INSERT INTO {tempTableName} (Id, TicketTypeId, Price, Status, IsReserved)
                                    VALUES {values}");
                            }

                            // Perform bulk update using the temp table
                            await _context.Database.ExecuteSqlRawAsync($@"
                                UPDATE Seats
                                SET TicketTypeId = temp.TicketTypeId,
                                    Price = temp.Price,
                                    Status = temp.Status,
                                    IsReserved = temp.IsReserved
                                FROM Seats
                                INNER JOIN {tempTableName} temp ON Seats.Id = temp.Id");

                            // Drop temp table
                            await _context.Database.ExecuteSqlRawAsync($"DROP TABLE {tempTableName}");
                        }

                        await transaction.CommitAsync();
                        
                        _logger.LogInformation(
                            "Updated {UpdatedSeats} seats for event {EventId}. Allocated rows: {AllocatedRows}, Unallocated rows: {UnallocatedRows}", 
                            seatsToUpdate.Count, eventId, rowToTicketType.Keys.Count, unallocatedRows.Count);
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error updating seat allocations for event {EventId}", eventId);
                        throw;
                    }
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
