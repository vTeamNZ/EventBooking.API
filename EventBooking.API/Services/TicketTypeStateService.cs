using EventBooking.API.Data;
using EventBooking.API.DTOs;
using EventBooking.API.Models;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.API.Services
{
    /// <summary>
    /// Service for managing ticket type states and visibility
    /// </summary>
    public class TicketTypeStateService : ITicketTypeStateService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TicketTypeStateService> _logger;

        public TicketTypeStateService(AppDbContext context, ILogger<TicketTypeStateService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all visible ticket types for customers (excludes hidden tickets)
        /// Shows active tickets as purchasable, inactive tickets as "No longer available"
        /// </summary>
        public async Task<List<TicketTypeWithStateDTO>> GetVisibleTicketTypesForCustomers(int eventId)
        {
            try
            {
                var ticketTypes = await _context.TicketTypes
                    .Include(tt => tt.ReplacedByTicketType)
                    .Where(tt => tt.EventId == eventId && !tt.IsHidden) // Only visible tickets
                    .OrderBy(tt => tt.IsActive ? 0 : 1) // Active tickets first
                    .ThenBy(tt => tt.CreatedAt)
                    .ToListAsync();

                var result = new List<TicketTypeWithStateDTO>();
                foreach (var ticketType in ticketTypes)
                {
                    var replacedByName = ticketType.ReplacedByTicketType?.Name;
                    result.Add(MapToStateDTO(ticketType, replacedByName));
                }

                _logger.LogInformation($"Retrieved {result.Count} visible ticket types for event {eventId}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving visible ticket types for event {eventId}");
                throw;
            }
        }

        /// <summary>
        /// Get all ticket types for organizers (includes all states for management)
        /// </summary>
        public async Task<List<TicketTypeWithStateDTO>> GetAllTicketTypesForOrganizer(int eventId)
        {
            try
            {
                var ticketTypes = await _context.TicketTypes
                    .Include(tt => tt.ReplacedByTicketType)
                    .Where(tt => tt.EventId == eventId)
                    .OrderBy(tt => tt.CreatedAt)
                    .ToListAsync();

                var result = new List<TicketTypeWithStateDTO>();
                foreach (var ticketType in ticketTypes)
                {
                    var replacedByName = ticketType.ReplacedByTicketType?.Name;
                    result.Add(MapToStateDTO(ticketType, replacedByName));
                }

                _logger.LogInformation($"Retrieved {result.Count} ticket types (all states) for organizer of event {eventId}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving all ticket types for event {eventId}");
                throw;
            }
        }

        /// <summary>
        /// Get only active ticket types available for purchase
        /// </summary>
        public async Task<List<TicketTypeWithStateDTO>> GetActiveTicketTypesForPurchase(int eventId)
        {
            try
            {
                var ticketTypes = await _context.TicketTypes
                    .Where(tt => tt.EventId == eventId && tt.IsActive && !tt.IsHidden)
                    .OrderBy(tt => tt.CreatedAt)
                    .ToListAsync();

                var result = new List<TicketTypeWithStateDTO>();
                foreach (var ticketType in ticketTypes)
                {
                    result.Add(MapToStateDTO(ticketType));
                }

                _logger.LogInformation($"Retrieved {result.Count} active ticket types for purchase for event {eventId}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving active ticket types for event {eventId}");
                throw;
            }
        }

        /// <summary>
        /// Check if a ticket type has been sold
        /// </summary>
        public async Task<bool> HasBeenSold(int ticketTypeId)
        {
            try
            {
                var hasSales = await _context.BookingLineItems
                    .AnyAsync(bli => bli.ItemType == "Ticket" && bli.ItemId == ticketTypeId);

                _logger.LogDebug($"Ticket type {ticketTypeId} has sales: {hasSales}");
                return hasSales;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking sales for ticket type {ticketTypeId}");
                throw;
            }
        }

        /// <summary>
        /// Get ticket sales count for a specific ticket type
        /// </summary>
        public async Task<int> GetTicketSalesCount(int ticketTypeId)
        {
            try
            {
                var salesCount = await _context.BookingLineItems
                    .Where(bli => bli.ItemType == "Ticket" && bli.ItemId == ticketTypeId)
                    .SumAsync(bli => bli.Quantity);

                _logger.LogDebug($"Ticket type {ticketTypeId} has {salesCount} tickets sold");
                return salesCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting sales count for ticket type {ticketTypeId}");
                throw;
            }
        }

        /// <summary>
        /// Get ticket type with full state information
        /// </summary>
        public async Task<TicketTypeWithStateDTO?> GetTicketTypeWithState(int ticketTypeId)
        {
            try
            {
                var ticketType = await _context.TicketTypes
                    .Include(tt => tt.ReplacedByTicketType)
                    .FirstOrDefaultAsync(tt => tt.Id == ticketTypeId);

                if (ticketType == null)
                {
                    _logger.LogWarning($"Ticket type {ticketTypeId} not found");
                    return null;
                }

                var replacedByName = ticketType.ReplacedByTicketType?.Name;
                return MapToStateDTO(ticketType, replacedByName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving ticket type {ticketTypeId}");
                throw;
            }
        }

        /// <summary>
        /// Validate if a ticket type can be purchased (active, visible, has capacity)
        /// </summary>
        public async Task<bool> CanBePurchased(int ticketTypeId)
        {
            try
            {
                var ticketType = await _context.TicketTypes
                    .FirstOrDefaultAsync(tt => tt.Id == ticketTypeId);

                if (ticketType == null)
                {
                    _logger.LogWarning($"Ticket type {ticketTypeId} not found for purchase validation");
                    return false;
                }

                var canPurchase = ticketType.IsAvailableForPurchase;
                _logger.LogDebug($"Ticket type {ticketTypeId} can be purchased: {canPurchase}");
                return canPurchase;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating purchase eligibility for ticket type {ticketTypeId}");
                throw;
            }
        }

        /// <summary>
        /// Disable a ticket type (no longer purchasable but still visible)
        /// </summary>
        public async Task<bool> DisableTicketType(int ticketTypeId, DisableTicketTypeRequest request)
        {
            try
            {
                var ticketType = await _context.TicketTypes
                    .FirstOrDefaultAsync(tt => tt.Id == ticketTypeId);

                if (ticketType == null)
                {
                    _logger.LogWarning($"Ticket type {ticketTypeId} not found for disabling");
                    return false;
                }

                // Update state
                ticketType.Disable();

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Ticket type {ticketTypeId} disabled successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error disabling ticket type {ticketTypeId}");
                return false;
            }
        }

        /// <summary>
        /// Hide a ticket type (completely remove from customer view)
        /// </summary>
        public async Task<bool> HideTicketType(int ticketTypeId, HideTicketTypeRequest request)
        {
            try
            {
                var ticketType = await _context.TicketTypes
                    .FirstOrDefaultAsync(tt => tt.Id == ticketTypeId);

                if (ticketType == null)
                {
                    _logger.LogWarning($"Ticket type {ticketTypeId} not found for hiding");
                    return false;
                }

                // Update state
                ticketType.Hide();

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Ticket type {ticketTypeId} hidden successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error hiding ticket type {ticketTypeId}");
                return false;
            }
        }

        /// <summary>
        /// Reactivate a disabled or hidden ticket type
        /// </summary>
        public async Task<bool> ReactivateTicketType(int ticketTypeId)
        {
            try
            {
                var ticketType = await _context.TicketTypes
                    .FirstOrDefaultAsync(tt => tt.Id == ticketTypeId);

                if (ticketType == null)
                {
                    _logger.LogWarning($"Ticket type {ticketTypeId} not found for reactivation");
                    return false;
                }

                // Update state
                ticketType.Reactivate();

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Ticket type {ticketTypeId} reactivated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reactivating ticket type {ticketTypeId}");
                return false;
            }
        }

        /// <summary>
        /// Set a replacement ticket type for this one
        /// </summary>
        public async Task<bool> ReplaceTicketType(int ticketTypeId, ReplaceTicketTypeRequest request)
        {
            try
            {
                var ticketType = await _context.TicketTypes
                    .Include(tt => tt.Event)
                    .FirstOrDefaultAsync(tt => tt.Id == ticketTypeId);

                if (ticketType == null)
                {
                    _logger.LogWarning($"Ticket type {ticketTypeId} not found for replacement");
                    return false;
                }

                // Create new replacement ticket type
                var replacementTicket = new TicketType
                {
                    EventId = ticketType.EventId,
                    Type = request.Type,
                    Name = request.Name,
                    Price = request.Price,
                    Description = request.Description,
                    Color = request.Color ?? "#808080",
                    MaxTickets = request.MaxTickets,
                    IsStanding = request.IsStanding,
                    StandingCapacity = request.StandingCapacity,
                    IsActive = true,
                    IsHidden = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.TicketTypes.Add(replacementTicket);
                await _context.SaveChangesAsync();

                // Set replacement relationship
                ticketType.SetReplacement(replacementTicket.Id);

                // Handle original ticket state
                if (request.HideOriginal)
                {
                    ticketType.Hide();
                }
                else
                {
                    ticketType.Disable();
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Ticket type {ticketTypeId} replacement created and set to {replacementTicket.Id}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating replacement for ticket type {ticketTypeId}");
                return false;
            }
        }

        /// <summary>
        /// Check if a ticket type can be disabled
        /// </summary>
        public async Task<bool> CanBeDisabled(int ticketTypeId)
        {
            try
            {
                var ticketType = await _context.TicketTypes
                    .FirstOrDefaultAsync(tt => tt.Id == ticketTypeId);

                if (ticketType == null)
                {
                    return false;
                }

                // Can be disabled if currently active
                return ticketType.IsActive;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if ticket type {ticketTypeId} can be disabled");
                return false;
            }
        }

        /// <summary>
        /// Check if a ticket type can be hidden
        /// </summary>
        public async Task<bool> CanBeHidden(int ticketTypeId)
        {
            try
            {
                var ticketType = await _context.TicketTypes
                    .FirstOrDefaultAsync(tt => tt.Id == ticketTypeId);

                if (ticketType == null)
                {
                    return false;
                }

                // Can be hidden if currently visible
                return !ticketType.IsHidden;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if ticket type {ticketTypeId} can be hidden");
                return false;
            }
        }

        /// <summary>
        /// Convert TicketType entity to TicketTypeWithStateDTO
        /// </summary>
        public TicketTypeWithStateDTO MapToStateDTO(TicketType ticketType, string? replacedByTicketName = null)
        {
            return new TicketTypeWithStateDTO
            {
                Id = ticketType.Id,
                Type = ticketType.Type,
                Name = ticketType.Name,
                Price = ticketType.Price,
                Description = ticketType.Description,
                EventId = ticketType.EventId,
                Color = ticketType.Color,
                MaxTickets = ticketType.MaxTickets,
                IsStanding = ticketType.IsStanding,
                StandingCapacity = ticketType.StandingCapacity,
                
                // State management properties
                IsActive = ticketType.IsActive,
                IsHidden = ticketType.IsHidden,
                ReplacedBy = ticketType.ReplacedBy,
                CreatedAt = ticketType.CreatedAt,
                DisabledAt = ticketType.DisabledAt,
                HiddenAt = ticketType.HiddenAt,
                
                // Additional info
                ReplacedByTicketName = replacedByTicketName
            };
        }
    }
}