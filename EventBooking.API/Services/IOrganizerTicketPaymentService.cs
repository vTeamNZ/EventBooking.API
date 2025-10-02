using EventBooking.API.DTOs;

namespace EventBooking.API.Services
{
    public interface IOrganizerTicketPaymentService
    {
        /// <summary>
        /// Create a new organizer ticket payment record
        /// </summary>
        Task<OrganizerTicketPaymentDTO> CreatePaymentAsync(CreateOrganizerTicketPaymentRequest request);
        
        /// <summary>
        /// Update an existing organizer ticket payment record
        /// </summary>
        Task<OrganizerTicketPaymentDTO> UpdatePaymentAsync(int paymentId, UpdateOrganizerTicketPaymentRequest request);
        
        /// <summary>
        /// Get a single organizer ticket payment by ID
        /// </summary>
        Task<OrganizerTicketPaymentDTO?> GetPaymentByIdAsync(int paymentId);
        
        /// <summary>
        /// Get all organizer ticket payments for an event with optional filtering and pagination
        /// </summary>
        Task<PaginatedOrganizerPaymentsDTO> GetEventPaymentsAsync(OrganizerPaymentSearchRequest searchRequest);
        
        /// <summary>
        /// Get organizer payment summary for a specific event
        /// </summary>
        Task<OrganizerPaymentSummaryDTO> GetEventPaymentSummaryAsync(int eventId);
        
        /// <summary>
        /// Bulk update payment status for multiple tickets
        /// </summary>
        Task<List<OrganizerTicketPaymentDTO>> BulkUpdatePaymentStatusAsync(BulkUpdatePaymentStatusRequest request);
        
        /// <summary>
        /// Delete an organizer ticket payment record
        /// </summary>
        Task<bool> DeletePaymentAsync(int paymentId);
        
        /// <summary>
        /// Get payment statistics for an organizer across all their events
        /// </summary>
        Task<List<OrganizerPaymentSummaryDTO>> GetOrganizerPaymentStatisticsAsync(int organizerId);
        
        /// <summary>
        /// Create payment records from existing consolidated BookingLineItems (migration helper)
        /// </summary>
        Task<List<OrganizerTicketPaymentDTO>> MigrateBookingLineItemToIndividualPaymentsAsync(int bookingLineItemId, List<CreateOrganizerTicketPaymentRequest> individualTickets);
        
        /// <summary>
        /// Validate that customer information meets requirements
        /// </summary>
        Task<(bool IsValid, List<string> ValidationErrors)> ValidateCustomerInformationAsync(CreateOrganizerTicketPaymentRequest request);
        
        /// <summary>
        /// Get payment records for a specific customer across all events
        /// </summary>
        Task<List<OrganizerTicketPaymentDTO>> GetCustomerPaymentHistoryAsync(string customerEmail);
        
        /// <summary>
        /// Mark payment as received with automatic date setting
        /// </summary>
        Task<OrganizerTicketPaymentDTO> MarkPaymentReceivedAsync(int paymentId, string? paymentMethod = null, string? notes = null);
        
        /// <summary>
        /// Mark payment as pending/unpaid
        /// </summary>
        Task<OrganizerTicketPaymentDTO> MarkPaymentPendingAsync(int paymentId, string? notes = null);
    }
}
