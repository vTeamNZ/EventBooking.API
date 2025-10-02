using Microsoft.EntityFrameworkCore;
using EventBooking.API.Data;
using EventBooking.API.DTOs;
using EventBooking.API.Models;

namespace EventBooking.API.Services
{
    /// <summary>
    /// Service for managing organizer-issued ticket payment tracking
    /// Handles CRUD operations and payment status management for OrganizerTicketPayment table
    /// </summary>
    public class OrganizerTicketPaymentService : IOrganizerTicketPaymentService
    {
        private readonly AppDbContext _context;

        public OrganizerTicketPaymentService(AppDbContext context)
        {
            _context = context;
        }

        // Core CRUD Operations
        public async Task<OrganizerTicketPaymentDTO> CreatePaymentAsync(CreateOrganizerTicketPaymentRequest request)
        {
            var payment = new OrganizerTicketPayment
            {
                BookingLineItemId = request.BookingLineItemId,
                EventId = request.EventId,
                TicketTypeId = request.TicketTypeId,
                CustomerFirstName = request.CustomerFirstName,
                CustomerLastName = request.CustomerLastName,
                CustomerEmail = request.CustomerEmail,
                CustomerMobile = request.CustomerMobile,
                TicketPrice = request.TicketPrice,
                IsPaidToOrganizer = request.IsPaidToOrganizer ?? false,
                PaidDate = request.PaidDate,
                PaymentMethod = request.PaymentMethod,
                Notes = request.Notes,
                SeatDetails = request.SeatDetails,
                Status = "Active",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.OrganizerTicketPayments.Add(payment);
            await _context.SaveChangesAsync();

            return await GetPaymentByIdAsync(payment.Id) ?? throw new InvalidOperationException("Failed to create payment");
        }

        public async Task<OrganizerTicketPaymentDTO> UpdatePaymentAsync(int paymentId, UpdateOrganizerTicketPaymentRequest request)
        {
            var payment = await _context.OrganizerTicketPayments.FindAsync(paymentId);
            if (payment == null) throw new KeyNotFoundException($"Payment with ID {paymentId} not found");

            payment.CustomerFirstName = request.CustomerFirstName;
            payment.CustomerLastName = request.CustomerLastName;
            payment.CustomerEmail = request.CustomerEmail;
            payment.CustomerMobile = request.CustomerMobile;
            payment.TicketPrice = request.TicketPrice;
            payment.IsPaidToOrganizer = request.IsPaidToOrganizer;
            payment.PaidDate = request.PaidDate;
            payment.PaymentMethod = request.PaymentMethod;
            payment.Notes = request.Notes;
            payment.SeatDetails = request.SeatDetails;
            payment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return await GetPaymentByIdAsync(paymentId) ?? throw new InvalidOperationException("Failed to update payment");
        }

        public async Task<OrganizerTicketPaymentDTO?> GetPaymentByIdAsync(int paymentId)
        {
            return await _context.OrganizerTicketPayments
                .Where(p => p.Id == paymentId)
                .Include(p => p.Event)
                .Include(p => p.TicketType)
                .Select(p => new OrganizerTicketPaymentDTO
                {
                    Id = p.Id,
                    BookingLineItemId = p.BookingLineItemId,
                    EventId = p.EventId,
                    TicketTypeId = p.TicketTypeId,
                    CustomerFirstName = p.CustomerFirstName,
                    CustomerLastName = p.CustomerLastName,
                    CustomerEmail = p.CustomerEmail,
                    CustomerMobile = p.CustomerMobile,
                    TicketPrice = p.TicketPrice,
                    IsPaidToOrganizer = p.IsPaidToOrganizer,
                    PaidDate = p.PaidDate,
                    PaymentMethod = p.PaymentMethod,
                    Notes = p.Notes,
                    SeatDetails = p.SeatDetails,
                    TicketTypeName = p.TicketType.Name ?? p.TicketType.Type,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .FirstOrDefaultAsync();
        }

        public async Task<PaginatedOrganizerPaymentsDTO> GetEventPaymentsAsync(OrganizerPaymentSearchRequest searchRequest)
        {
            var query = _context.OrganizerTicketPayments
                .Where(p => p.EventId == searchRequest.EventId)
                .Include(p => p.Event)
                .Include(p => p.TicketType)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(searchRequest.CustomerEmail))
            {
                query = query.Where(p => p.CustomerEmail.Contains(searchRequest.CustomerEmail));
            }

            if (!string.IsNullOrEmpty(searchRequest.CustomerName))
            {
                query = query.Where(p => p.CustomerFirstName.Contains(searchRequest.CustomerName) || 
                                        p.CustomerLastName.Contains(searchRequest.CustomerName));
            }

            if (searchRequest.IsPaidToOrganizer.HasValue)
            {
                query = query.Where(p => p.IsPaidToOrganizer == searchRequest.IsPaidToOrganizer.Value);
            }

            if (searchRequest.TicketTypeId.HasValue)
            {
                query = query.Where(p => p.TicketTypeId == searchRequest.TicketTypeId.Value);
            }

            // Total count for pagination
            var totalCount = await query.CountAsync();

            // Apply pagination
            var payments = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((searchRequest.Page - 1) * searchRequest.PageSize)
                .Take(searchRequest.PageSize)
                .Select(p => new OrganizerTicketPaymentDTO
                {
                    Id = p.Id,
                    BookingLineItemId = p.BookingLineItemId,
                    EventId = p.EventId,
                    TicketTypeId = p.TicketTypeId,
                    CustomerFirstName = p.CustomerFirstName,
                    CustomerLastName = p.CustomerLastName,
                    CustomerEmail = p.CustomerEmail,
                    CustomerMobile = p.CustomerMobile,
                    TicketPrice = p.TicketPrice,
                    IsPaidToOrganizer = p.IsPaidToOrganizer,
                    PaidDate = p.PaidDate,
                    PaymentMethod = p.PaymentMethod,
                    Notes = p.Notes,
                    SeatDetails = p.SeatDetails,
                    TicketTypeName = p.TicketType.Name ?? p.TicketType.Type,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();

            return new PaginatedOrganizerPaymentsDTO
            {
                Payments = payments,
                TotalCount = totalCount,
                Page = searchRequest.Page,
                PageSize = searchRequest.PageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / searchRequest.PageSize)
            };
        }

        public async Task<OrganizerPaymentSummaryDTO> GetEventPaymentSummaryAsync(int eventId)
        {
            var payments = await _context.OrganizerTicketPayments
                .Where(p => p.EventId == eventId)
                .ToListAsync();

            var totalTickets = payments.Count;
            var totalRevenue = payments.Sum(p => p.TicketPrice);
            var paidTickets = payments.Count(p => p.IsPaidToOrganizer);
            var unpaidTickets = totalTickets - paidTickets;
            var paidRevenue = payments.Where(p => p.IsPaidToOrganizer).Sum(p => p.TicketPrice);
            var unpaidRevenue = totalRevenue - paidRevenue;

            return new OrganizerPaymentSummaryDTO
            {
                EventId = eventId,
                TotalTickets = totalTickets,
                TotalRevenue = totalRevenue,
                PaidTickets = paidTickets,
                UnpaidTickets = unpaidTickets,
                PaidRevenue = paidRevenue,
                UnpaidRevenue = unpaidRevenue,
                PaymentRate = totalTickets > 0 ? (decimal)paidTickets / totalTickets * 100 : 0
            };
        }

        public async Task<List<OrganizerTicketPaymentDTO>> BulkUpdatePaymentStatusAsync(BulkUpdatePaymentStatusRequest request)
        {
            var payments = await _context.OrganizerTicketPayments
                .Where(p => request.PaymentIds.Contains(p.Id))
                .ToListAsync();

            foreach (var payment in payments)
            {
                payment.IsPaidToOrganizer = request.IsPaidToOrganizer;
                payment.PaidDate = request.PaidDate;
                payment.PaymentMethod = request.PaymentMethod;
                payment.Notes = request.Notes;
                payment.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Return updated payment DTOs
            var updatedPaymentIds = payments.Select(p => p.Id).ToList();
            return await _context.OrganizerTicketPayments
                .Where(p => updatedPaymentIds.Contains(p.Id))
                .Include(p => p.Event)
                .Include(p => p.TicketType)
                .Select(p => new OrganizerTicketPaymentDTO
                {
                    Id = p.Id,
                    BookingLineItemId = p.BookingLineItemId,
                    EventId = p.EventId,
                    TicketTypeId = p.TicketTypeId,
                    CustomerFirstName = p.CustomerFirstName,
                    CustomerLastName = p.CustomerLastName,
                    CustomerEmail = p.CustomerEmail,
                    CustomerMobile = p.CustomerMobile,
                    TicketPrice = p.TicketPrice,
                    IsPaidToOrganizer = p.IsPaidToOrganizer,
                    PaidDate = p.PaidDate,
                    PaymentMethod = p.PaymentMethod,
                    Notes = p.Notes,
                    SeatDetails = p.SeatDetails,
                    TicketTypeName = p.TicketType.Name ?? p.TicketType.Type,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<bool> DeletePaymentAsync(int paymentId)
        {
            var payment = await _context.OrganizerTicketPayments.FindAsync(paymentId);
            if (payment == null) return false;

            _context.OrganizerTicketPayments.Remove(payment);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<OrganizerPaymentSummaryDTO>> GetOrganizerPaymentStatisticsAsync(int organizerId)
        {
            // Get all events for this organizer
            var organizerEvents = await _context.Events
                .Where(e => e.OrganizerId == organizerId)
                .Select(e => e.Id)
                .ToListAsync();

            var results = new List<OrganizerPaymentSummaryDTO>();
            
            foreach (var eventId in organizerEvents)
            {
                var summary = await GetEventPaymentSummaryAsync(eventId);
                results.Add(summary);
            }

            return results;
        }

        public async Task<List<OrganizerTicketPaymentDTO>> MigrateBookingLineItemToIndividualPaymentsAsync(int bookingLineItemId, List<CreateOrganizerTicketPaymentRequest> individualTickets)
        {
            var results = new List<OrganizerTicketPaymentDTO>();
            
            foreach (var ticketRequest in individualTickets)
            {
                ticketRequest.BookingLineItemId = bookingLineItemId; // Ensure consistency
                var payment = await CreatePaymentAsync(ticketRequest);
                results.Add(payment);
            }

            return results;
        }

        public async Task<(bool IsValid, List<string> ValidationErrors)> ValidateCustomerInformationAsync(CreateOrganizerTicketPaymentRequest request)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(request.CustomerFirstName))
                errors.Add("Customer first name is required");
            
            if (string.IsNullOrWhiteSpace(request.CustomerLastName))
                errors.Add("Customer last name is required");
            
            if (string.IsNullOrWhiteSpace(request.CustomerEmail))
                errors.Add("Customer email is required");
            else if (!IsValidEmail(request.CustomerEmail))
                errors.Add("Customer email format is invalid");
            
            if (request.TicketPrice < 0)
                errors.Add("Ticket price cannot be negative");

            return (errors.Count == 0, errors);
        }

        public async Task<List<OrganizerTicketPaymentDTO>> GetCustomerPaymentHistoryAsync(string customerEmail)
        {
            return await _context.OrganizerTicketPayments
                .Where(p => p.CustomerEmail == customerEmail)
                .Include(p => p.Event)
                .Include(p => p.TicketType)
                .Select(p => new OrganizerTicketPaymentDTO
                {
                    Id = p.Id,
                    BookingLineItemId = p.BookingLineItemId,
                    EventId = p.EventId,
                    TicketTypeId = p.TicketTypeId,
                    CustomerFirstName = p.CustomerFirstName,
                    CustomerLastName = p.CustomerLastName,
                    CustomerEmail = p.CustomerEmail,
                    CustomerMobile = p.CustomerMobile,
                    TicketPrice = p.TicketPrice,
                    IsPaidToOrganizer = p.IsPaidToOrganizer,
                    PaidDate = p.PaidDate,
                    PaymentMethod = p.PaymentMethod,
                    Notes = p.Notes,
                    SeatDetails = p.SeatDetails,
                    TicketTypeName = p.TicketType.Name ?? p.TicketType.Type,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<OrganizerTicketPaymentDTO> MarkPaymentReceivedAsync(int paymentId, string? paymentMethod = null, string? notes = null)
        {
            var payment = await _context.OrganizerTicketPayments.FindAsync(paymentId);
            if (payment == null) throw new KeyNotFoundException($"Payment with ID {paymentId} not found");

            payment.IsPaidToOrganizer = true;
            payment.PaidDate = DateTime.UtcNow;
            payment.PaymentMethod = paymentMethod ?? payment.PaymentMethod;
            payment.Notes = notes ?? payment.Notes;
            payment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return await GetPaymentByIdAsync(paymentId) ?? throw new InvalidOperationException("Failed to mark payment as received");
        }

        public async Task<OrganizerTicketPaymentDTO> MarkPaymentPendingAsync(int paymentId, string? notes = null)
        {
            var payment = await _context.OrganizerTicketPayments.FindAsync(paymentId);
            if (payment == null) throw new KeyNotFoundException($"Payment with ID {paymentId} not found");

            payment.IsPaidToOrganizer = false;
            payment.PaidDate = null;
            payment.Notes = notes ?? payment.Notes;
            payment.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return await GetPaymentByIdAsync(paymentId) ?? throw new InvalidOperationException("Failed to mark payment as pending");
        }

        // Helper methods
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
