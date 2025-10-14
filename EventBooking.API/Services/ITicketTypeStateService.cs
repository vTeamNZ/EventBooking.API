using EventBooking.API.DTOs;
using EventBooking.API.Models;

namespace EventBooking.API.Services
{
    /// <summary>
    /// Service interface for managing ticket type states and visibility
    /// </summary>
    public interface ITicketTypeStateService
    {
        /// <summary>
        /// Get all visible ticket types for customers (excludes hidden tickets)
        /// </summary>
        Task<List<TicketTypeWithStateDTO>> GetVisibleTicketTypesForCustomers(int eventId);

        /// <summary>
        /// Get all ticket types for organizers (includes all states for management)
        /// </summary>
        Task<List<TicketTypeWithStateDTO>> GetAllTicketTypesForOrganizer(int eventId);

        /// <summary>
        /// Get only active ticket types available for purchase
        /// </summary>
        Task<List<TicketTypeWithStateDTO>> GetActiveTicketTypesForPurchase(int eventId);

        /// <summary>
        /// Check if a ticket type has been sold
        /// </summary>
        Task<bool> HasBeenSold(int ticketTypeId);

        /// <summary>
        /// Get ticket sales count for a specific ticket type
        /// </summary>
        Task<int> GetTicketSalesCount(int ticketTypeId);

        /// <summary>
        /// Get ticket type with full state information
        /// </summary>
        Task<TicketTypeWithStateDTO?> GetTicketTypeWithState(int ticketTypeId);

        /// <summary>
        /// Validate if a ticket type can be purchased (active, visible, has capacity)
        /// </summary>
        Task<bool> CanBePurchased(int ticketTypeId);

        /// <summary>
        /// Convert TicketType entity to TicketTypeWithStateDTO
        /// </summary>
        TicketTypeWithStateDTO MapToStateDTO(TicketType ticketType, string? replacedByTicketName = null);

        /// <summary>
        /// Disable a ticket type (no longer purchasable but still visible)
        /// </summary>
        Task<bool> DisableTicketType(int ticketTypeId, DisableTicketTypeRequest request);

        /// <summary>
        /// Hide a ticket type (completely remove from customer view)
        /// </summary>
        Task<bool> HideTicketType(int ticketTypeId, HideTicketTypeRequest request);

        /// <summary>
        /// Reactivate a disabled or hidden ticket type
        /// </summary>
        Task<bool> ReactivateTicketType(int ticketTypeId);

        /// <summary>
        /// Set a replacement ticket type for this one
        /// </summary>
        Task<bool> ReplaceTicketType(int ticketTypeId, ReplaceTicketTypeRequest request);

        /// <summary>
        /// Check if a ticket type can be disabled
        /// </summary>
        Task<bool> CanBeDisabled(int ticketTypeId);

        /// <summary>
        /// Check if a ticket type can be hidden
        /// </summary>
        Task<bool> CanBeHidden(int ticketTypeId);
    }
}