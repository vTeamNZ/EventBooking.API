using Microsoft.EntityFrameworkCore;
using EventBooking.API.Data;
using EventBooking.API.Models;

namespace EventBooking.API.Services
{
    public class SeatCreationService : ISeatCreationService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SeatCreationService> _logger;
        private readonly ISeatAllocationService _seatAllocationService;

        public SeatCreationService(
            AppDbContext context, 
            ILogger<SeatCreationService> logger,
            ISeatAllocationService seatAllocationService)
        {
            _context = context;
            _logger = logger;
            _seatAllocationService = seatAllocationService;
        }

        public async Task<int> CreateSeatsForEventAsync(int eventId, int venueId)
        {
            _logger.LogInformation("Creating seats for event {EventId} with venue {VenueId}", eventId, venueId);
            
            var event_ = await _context.Events.FindAsync(eventId);
            var venue = await _context.Venues
                .FirstOrDefaultAsync(v => v.Id == venueId);

            if (event_ == null || venue == null)
            {
                _logger.LogWarning("Event {EventId} or Venue {VenueId} not found", eventId, venueId);
                return 0;
            }

            // Only create seats if we're using EventHall mode
            _logger.LogInformation("Event {EventId} has SeatSelectionMode: {SeatSelectionMode} (value: {ModeValue})", 
                eventId, event_.SeatSelectionMode, (int)event_.SeatSelectionMode);
                
            // Check for EventHall mode (value 1)
            if (event_.SeatSelectionMode != SeatSelectionMode.EventHall)
            {
                _logger.LogWarning("Event {EventId} uses {SeatSelectionMode} (value: {ModeValue}) - attempting to set to EventHall mode", 
                    eventId, event_.SeatSelectionMode, (int)event_.SeatSelectionMode);
                
                // Force EventHall mode for venues that should have seats
                event_.SeatSelectionMode = SeatSelectionMode.EventHall;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated event {EventId} to use EventHall mode", eventId);
            }

            // Check if seats already exist for this event
            var existingSeats = await _context.Seats.AnyAsync(s => s.EventId == eventId);
            if (existingSeats)
            {
                _logger.LogWarning("Seats already exist for event {EventId}, skipping creation", eventId);
                var seatCount = await _context.Seats.CountAsync(s => s.EventId == eventId);
                return seatCount;
            }
            
            // Get ticket types or create a default one if none exist
            var sections = await _context.TicketTypes
                .Where(tt => tt.EventId == eventId)
                .ToListAsync();
            
            if (!sections.Any())
            {
                _logger.LogInformation("No ticket types found for event {EventId}, creating default ticket type", eventId);
                var defaultSection = new TicketType
                {
                    Type = "General",
                    Name = "General",
                    Color = "#4B5563",
                    Price = event_.Price ?? 50.0m,
                    EventId = eventId,
                };
                _context.TicketTypes.Add(defaultSection);
                await _context.SaveChangesAsync();
                sections.Add(defaultSection);
            }

            var seats = new List<Seat>();
            int rowCount = venue.NumberOfRows > 0 ? venue.NumberOfRows : 10;
            int seatsPerRow = venue.SeatsPerRow > 0 ? venue.SeatsPerRow : 12;
            
            // Distribute ticket types evenly across rows (e.g., premium in front, standard in back)
            for (int row = 0; row < rowCount; row++)
            {
                // Determine which ticket type this row belongs to based on position
                // Front rows get the first ticket types (usually more premium)
                int ticketTypeIndex = (row * sections.Count) / rowCount;
                var ticketType = sections[ticketTypeIndex];
                
                for (int seatNum = 0; seatNum < seatsPerRow; seatNum++)
                {
                    var newSeat = new Seat
                    {
                        EventId = eventId,
                        TicketTypeId = ticketType.Id,
                        Row = ((char)('A' + row)).ToString(),
                        Number = seatNum + 1,
                        SeatNumber = $"{(char)('A' + row)}{seatNum + 1}",
                        X = 50 + seatNum * (venue.SeatSpacing > 0 ? venue.SeatSpacing : 30),
                        Y = 100 + row * (venue.RowSpacing > 0 ? venue.RowSpacing : 40),
                        Width = 30,
                        Height = 35,
                        Price = section.BasePrice,
                        Status = SeatStatus.Reserved, // Initially mark all seats as reserved
                        IsReserved = true // Initially mark all seats as reserved until ticket types allocate them
                    };
                    
                    seats.Add(newSeat);
                }
            }

            _context.Seats.AddRange(seats);
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Created {SeatCount} seats for event {EventId}", seats.Count, eventId);
            return seats.Count;
        }
    }
}