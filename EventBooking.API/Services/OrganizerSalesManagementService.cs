using System.Security.Claims;
using System.Text.Json;
using EventBooking.API.Data;
using EventBooking.API.DTOs;
using EventBooking.API.Models;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.API.Services
{
    /// <summary>
    /// Service interface for organizer sales management functionality
    /// Simplified version - no complex refunding, just basic CRUD operations
    /// </summary>
    public interface IOrganizerSalesManagementService
    {
        /// <summary>
        /// Get tickets for sales management table display
        /// </summary>
        Task<List<OrganizerTicketSalesDTO>> GetTicketsForSalesManagementAsync(int eventId);
        
        /// <summary>
        /// Update customer details (first name, last name, email)
        /// </summary>
        Task<bool> UpdateCustomerDetailsAsync(int paymentId, UpdateCustomerDetailsRequest request);
        
        /// <summary>
        /// Toggle payment status (paid/unpaid)
        /// </summary>
        Task<bool> TogglePaymentStatusAsync(int paymentId, bool isPaid);
        
        /// <summary>
        /// Cancel ticket (change status to "Cancelled") - PERMANENT action
        /// </summary>
        Task<bool> CancelTicketAsync(int paymentId);
    }
    
    /// <summary>
    /// Implementation of organizer sales management service
    /// Focuses on simple operations without complex refund processing
    /// </summary>
    public class OrganizerSalesManagementService : IOrganizerSalesManagementService
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<OrganizerSalesManagementService> _logger;
        
        public OrganizerSalesManagementService(
            AppDbContext context,
            IHttpContextAccessor httpContextAccessor,
            ILogger<OrganizerSalesManagementService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }
        
        /// <summary>
        /// Get all tickets for the organizer's event in sales management format
        /// </summary>
        public async Task<List<OrganizerTicketSalesDTO>> GetTicketsForSalesManagementAsync(int eventId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                _logger.LogInformation("Getting sales management tickets for event {EventId} by user {UserId}", eventId, currentUserId);
                
                // Verify organizer owns this event and get tickets
                var tickets = await _context.OrganizerTicketPayments
                    .Include(p => p.Event)
                    .ThenInclude(e => e.Organizer)
                    .Where(p => p.EventId == eventId && p.Event.Organizer.UserId == currentUserId)
                    .Select(p => new OrganizerTicketSalesDTO
                    {
                        Id = p.Id,
                        CustomerFirstName = p.CustomerFirstName ?? "",
                        CustomerLastName = p.CustomerLastName ?? "",
                        CustomerEmail = p.CustomerEmail ?? "",
                        SeatDetails = p.SeatDetails,
                        TicketPrice = p.TicketPrice,
                        IsPaid = p.IsPaidToOrganizer,
                        Status = p.Status ?? "Active",
                        CreatedAt = p.CreatedAt,
                        UpdatedAt = p.UpdatedAt
                    })
                    .OrderByDescending(p => p.CreatedAt) // Latest bookings first
                    .ToListAsync();
                
                _logger.LogInformation("Retrieved {Count} tickets for sales management", tickets.Count);
                return tickets;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sales management tickets for event {EventId}", eventId);
                throw;
            }
        }
        
        /// <summary>
        /// Update customer details for a ticket
        /// </summary>
        public async Task<bool> UpdateCustomerDetailsAsync(int paymentId, UpdateCustomerDetailsRequest request)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                _logger.LogInformation("Updating customer details for payment {PaymentId} by user {UserId}", paymentId, currentUserId);
                
                var payment = await GetOrganizerTicketPaymentAsync(paymentId, currentUserId);
                
                // Update customer details
                payment.CustomerFirstName = request.CustomerFirstName.Trim();
                payment.CustomerLastName = request.CustomerLastName.Trim();
                payment.CustomerEmail = request.CustomerEmail.Trim();
                payment.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Successfully updated customer details for payment {PaymentId}", paymentId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer details for payment {PaymentId}", paymentId);
                throw;
            }
        }
        
        /// <summary>
        /// Toggle payment status for a ticket
        /// </summary>
        public async Task<bool> TogglePaymentStatusAsync(int paymentId, bool isPaid)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                _logger.LogInformation("Toggling payment status for payment {PaymentId} to {IsPaid} by user {UserId}", paymentId, isPaid, currentUserId);
                
                var payment = await GetOrganizerTicketPaymentAsync(paymentId, currentUserId);
                
                // Update payment status
                payment.IsPaidToOrganizer = isPaid;
                payment.PaidDate = isPaid ? DateTime.UtcNow : null;
                payment.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Successfully toggled payment status for payment {PaymentId} to {IsPaid}", paymentId, isPaid);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling payment status for payment {PaymentId}", paymentId);
                throw;
            }
        }
        
        /// <summary>
        /// Cancel a ticket (change status to "Cancelled")
        /// Also frees up seats if it's a seated ticket type
        /// </summary>
        public async Task<bool> CancelTicketAsync(int paymentId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                _logger.LogInformation("Cancelling ticket for payment {PaymentId} by user {UserId}", paymentId, currentUserId);
                
                // Get payment with related ticket type information
                var payment = await _context.OrganizerTicketPayments
                    .Include(p => p.Event)
                    .ThenInclude(e => e.Organizer)
                    .Include(p => p.TicketType)
                    .FirstOrDefaultAsync(p => p.Id == paymentId);
                
                if (payment == null)
                {
                    _logger.LogWarning("Ticket payment {PaymentId} not found", paymentId);
                    throw new KeyNotFoundException($"Ticket payment {paymentId} not found");
                }
                
                if (payment.Event?.Organizer?.UserId != currentUserId)
                {
                    _logger.LogWarning("User {UserId} attempted to access ticket payment {PaymentId} they don't own", currentUserId, paymentId);
                    throw new UnauthorizedAccessException("Access denied - ticket not owned by current organizer");
                }
                
                // Update status to cancelled
                payment.Status = "Cancelled";
                payment.UpdatedAt = DateTime.UtcNow;
                
                // Check if this is a seated ticket type and free up the seat
                await FreeSeatIfSeatedTicketAsync(payment);
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Successfully cancelled ticket for payment {PaymentId}", paymentId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling ticket for payment {PaymentId}", paymentId);
                throw;
            }
        }
        
        /// <summary>
        /// Frees up seat if the ticket is for a seated ticket type
        /// </summary>
        private async Task FreeSeatIfSeatedTicketAsync(OrganizerTicketPayment payment)
        {
            try
            {
                // Check if this ticket type has seat assignments (indicates it's a seated ticket type)
                if (payment.TicketType != null && 
                    !string.IsNullOrEmpty(payment.TicketType.SeatRowAssignments) &&
                    !string.IsNullOrWhiteSpace(payment.SeatDetails))
                {
                    _logger.LogInformation("Freeing seat for seated ticket - Payment {PaymentId}, SeatDetails: {SeatDetails}", 
                        payment.Id, payment.SeatDetails);
                    
                    // Find the seat by SeatNumber matching the SeatDetails value
                    var seat = await _context.Seats
                        .FirstOrDefaultAsync(s => s.EventId == payment.EventId && 
                                                  s.SeatNumber == payment.SeatDetails);
                    
                    if (seat != null)
                    {
                        // Free up the seat
                        seat.Status = SeatStatus.Available;
                        seat.ReservedUntil = null;
                        seat.ReservedBy = null;
                        seat.IsReserved = false;
                        
                        _logger.LogInformation("Successfully freed seat {SeatNumber} (ID: {SeatId}) for cancelled ticket {PaymentId}", 
                            seat.SeatNumber, seat.Id, payment.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Seat with number {SeatNumber} not found for event {EventId} when cancelling payment {PaymentId}", 
                            payment.SeatDetails, payment.EventId, payment.Id);
                    }
                }
                else
                {
                    _logger.LogDebug("Payment {PaymentId} is for general admission ticket type - no seat to free", payment.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error freeing seat for payment {PaymentId}", payment.Id);
                // Don't throw here - we don't want seat freeing errors to prevent ticket cancellation
            }
        }
        
        /// <summary>
        /// Get organizer ticket payment with authorization check
        /// </summary>
        private async Task<OrganizerTicketPayment> GetOrganizerTicketPaymentAsync(int paymentId, string userId)
        {
            var payment = await _context.OrganizerTicketPayments
                .Include(p => p.Event)
                .ThenInclude(e => e.Organizer)
                .FirstOrDefaultAsync(p => p.Id == paymentId);
            
            if (payment == null)
            {
                _logger.LogWarning("Ticket payment {PaymentId} not found", paymentId);
                throw new KeyNotFoundException($"Ticket payment {paymentId} not found");
            }
            
            if (payment.Event?.Organizer?.UserId != userId)
            {
                _logger.LogWarning("User {UserId} attempted to access ticket payment {PaymentId} they don't own", userId, paymentId);
                throw new UnauthorizedAccessException("Access denied - ticket not owned by current organizer");
            }
            
            return payment;
        }
        
        /// <summary>
        /// Get current user ID from JWT token
        /// </summary>
        private string GetCurrentUserId()
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("User not authenticated or user ID not found in token");
                throw new UnauthorizedAccessException("User not authenticated");
            }
            
            return userId;
        }
    }
}
