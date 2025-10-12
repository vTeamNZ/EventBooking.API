using System.Security.Claims;
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
        /// Cancel ticket (change status to "Cancelled")
        /// </summary>
        Task<bool> CancelTicketAsync(int paymentId);
        
        /// <summary>
        /// Restore ticket (change status back to "Active")
        /// </summary>
        Task<bool> RestoreTicketAsync(int paymentId);
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
        /// </summary>
        public async Task<bool> CancelTicketAsync(int paymentId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                _logger.LogInformation("Cancelling ticket for payment {PaymentId} by user {UserId}", paymentId, currentUserId);
                
                var payment = await GetOrganizerTicketPaymentAsync(paymentId, currentUserId);
                
                // Update status to cancelled
                payment.Status = "Cancelled";
                payment.UpdatedAt = DateTime.UtcNow;
                
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
        /// Restore a ticket (change status back to "Active")
        /// </summary>
        public async Task<bool> RestoreTicketAsync(int paymentId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                _logger.LogInformation("Restoring ticket for payment {PaymentId} by user {UserId}", paymentId, currentUserId);
                
                var payment = await GetOrganizerTicketPaymentAsync(paymentId, currentUserId);
                
                // Update status to active
                payment.Status = "Active";
                payment.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Successfully restored ticket for payment {PaymentId}", paymentId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring ticket for payment {PaymentId}", paymentId);
                throw;
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
