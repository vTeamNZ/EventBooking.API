using EventBooking.API.Data;
using EventBooking.API.Models;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.API.Services
{
    public interface ITicketAvailabilityService
    {
        Task<int> GetTicketsSoldAsync(int ticketTypeId);
        Task<int> GetTicketsAvailableAsync(int ticketTypeId);
        Task<bool> IsTicketTypeAvailableAsync(int ticketTypeId, int requestedQuantity);
        Task<Dictionary<int, int>> GetTicketAvailabilityForEventAsync(int eventId);
    }

    public class TicketAvailabilityService : ITicketAvailabilityService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TicketAvailabilityService> _logger;

        public TicketAvailabilityService(AppDbContext context, ILogger<TicketAvailabilityService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// 🎯 NEW ARCHITECTURE - Get the number of tickets sold for a specific ticket type using BookingLineItems
        /// </summary>
        public async Task<int> GetTicketsSoldAsync(int ticketTypeId)
        {
            var totalSold = await _context.BookingLineItems
                .Where(bli => bli.ItemId == ticketTypeId && 
                            bli.ItemType == "Ticket" && 
                            bli.Status == "Active")
                .SumAsync(bli => bli.Quantity);
            
            _logger.LogInformation("🎯 NEW ARCHITECTURE - GetTicketsSoldAsync: TicketTypeId={TicketTypeId}, TotalSold={TotalSold}", 
                ticketTypeId, totalSold);
            
            return totalSold;
        }

        /// <summary>
        /// 🎯 NEW ARCHITECTURE - Get the number of tickets available for a specific ticket type
        /// </summary>
        public async Task<int> GetTicketsAvailableAsync(int ticketTypeId)
        {
            var ticketType = await _context.TicketTypes
                .FirstOrDefaultAsync(tt => tt.Id == ticketTypeId);

            if (ticketType?.MaxTickets == null)
            {
                _logger.LogInformation("🎯 NEW ARCHITECTURE - GetTicketsAvailableAsync: TicketTypeId={TicketTypeId}, MaxTickets=null, returning -1 (unlimited)", 
                    ticketTypeId);
                // For allocated seating or ticket types without limits, return -1 to indicate unlimited
                return -1;
            }

            var sold = await GetTicketsSoldAsync(ticketTypeId);
            var available = ticketType.MaxTickets.Value - sold;
            
            _logger.LogInformation("🎯 NEW ARCHITECTURE - GetTicketsAvailableAsync: TicketTypeId={TicketTypeId}, MaxTickets={MaxTickets}, Sold={Sold}, Available={Available}", 
                ticketTypeId, ticketType.MaxTickets.Value, sold, available);
            
            return Math.Max(0, available);
        }

        /// <summary>
        /// Check if a specific quantity of tickets is available for a ticket type
        /// </summary>
        public async Task<bool> IsTicketTypeAvailableAsync(int ticketTypeId, int requestedQuantity)
        {
            var available = await GetTicketsAvailableAsync(ticketTypeId);
            
            // If available is -1, it means no limit (allocated seating)
            if (available == -1) return true;
            
            return available >= requestedQuantity;
        }

        /// <summary>
        /// Get ticket availability for all ticket types in an event
        /// </summary>
        public async Task<Dictionary<int, int>> GetTicketAvailabilityForEventAsync(int eventId)
        {
            var ticketTypes = await _context.TicketTypes
                .Where(tt => tt.EventId == eventId)
                .ToListAsync();

            var availability = new Dictionary<int, int>();

            foreach (var ticketType in ticketTypes)
            {
                var available = await GetTicketsAvailableAsync(ticketType.Id);
                availability[ticketType.Id] = available;
            }

            return availability;
        }
    }
}
