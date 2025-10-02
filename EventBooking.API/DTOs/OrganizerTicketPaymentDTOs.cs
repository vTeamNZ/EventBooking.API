using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.DTOs
{
    /// <summary>
    /// DTO for displaying organizer ticket payment information
    /// </summary>
    public class OrganizerTicketPaymentDTO
    {
        public int Id { get; set; }
        public int BookingLineItemId { get; set; }
        public int EventId { get; set; }
        public int TicketTypeId { get; set; }
        
        // Customer Information
        public string CustomerFirstName { get; set; } = string.Empty;
        public string? CustomerLastName { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public string? CustomerMobile { get; set; }
        
        // Payment Information
        public decimal TicketPrice { get; set; }
        public bool IsPaidToOrganizer { get; set; }
        public DateTime? PaidDate { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Notes { get; set; }
        
        // Seat/Ticket Details
        public string? SeatDetails { get; set; }
        public string TicketTypeName { get; set; } = string.Empty;
        
        // Metadata
        public string Status { get; set; } = "Active";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// DTO for creating a new organizer ticket payment record
    /// </summary>
    public class CreateOrganizerTicketPaymentRequest
    {
        [Required(ErrorMessage = "Booking line item ID is required")]
        public int BookingLineItemId { get; set; }

        [Required(ErrorMessage = "Event ID is required")]
        public int EventId { get; set; }

        [Required(ErrorMessage = "Ticket type ID is required")]
        public int TicketTypeId { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [StringLength(100, ErrorMessage = "First name must not exceed 100 characters")]
        [Display(Name = "First Name")]
        public string CustomerFirstName { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Last name must not exceed 100 characters")]
        [Display(Name = "Last Name")]
        public string? CustomerLastName { get; set; }

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(255, ErrorMessage = "Email must not exceed 255 characters")]
        [Display(Name = "Email Address")]
        public string CustomerEmail { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20, ErrorMessage = "Mobile number must not exceed 20 characters")]
        [Display(Name = "Mobile Number")]
        public string? CustomerMobile { get; set; }

        [Required(ErrorMessage = "Ticket price is required")]
        [Range(0, 9999.99, ErrorMessage = "Ticket price must be between $0 and $9,999.99")]
        public decimal TicketPrice { get; set; }

        public bool? IsPaidToOrganizer { get; set; }

        public DateTime? PaidDate { get; set; }

        [StringLength(50, ErrorMessage = "Payment method must not exceed 50 characters")]
        public string? PaymentMethod { get; set; }

        [StringLength(500, ErrorMessage = "Notes must not exceed 500 characters")]
        public string? Notes { get; set; }

        public string? SeatDetails { get; set; }
    }

    /// <summary>
    /// DTO for updating an existing organizer ticket payment record
    /// </summary>
    public class UpdateOrganizerTicketPaymentRequest
    {
        [Required(ErrorMessage = "First name is required")]
        [StringLength(100, ErrorMessage = "First name must not exceed 100 characters")]
        [Display(Name = "First Name")]
        public string CustomerFirstName { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Last name must not exceed 100 characters")]
        [Display(Name = "Last Name")]
        public string? CustomerLastName { get; set; }

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(255, ErrorMessage = "Email must not exceed 255 characters")]
        [Display(Name = "Email Address")]
        public string CustomerEmail { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20, ErrorMessage = "Mobile number must not exceed 20 characters")]
        [Display(Name = "Mobile Number")]
        public string? CustomerMobile { get; set; }

        [Required(ErrorMessage = "Ticket price is required")]
        [Range(0, 9999.99, ErrorMessage = "Ticket price must be between $0 and $9,999.99")]
        public decimal TicketPrice { get; set; }

        [Required(ErrorMessage = "Payment status is required")]
        public bool IsPaidToOrganizer { get; set; }

        public DateTime? PaidDate { get; set; }

        [StringLength(50, ErrorMessage = "Payment method must not exceed 50 characters")]
        public string? PaymentMethod { get; set; }

        [StringLength(500, ErrorMessage = "Notes must not exceed 500 characters")]
        public string? Notes { get; set; }

        public string? SeatDetails { get; set; }
    }

    /// <summary>
    /// DTO for bulk updating payment status
    /// </summary>
    public class BulkUpdatePaymentStatusRequest
    {
        [Required(ErrorMessage = "Payment IDs are required")]
        [MinLength(1, ErrorMessage = "At least one payment ID must be provided")]
        public List<int> PaymentIds { get; set; } = new();

        [Required(ErrorMessage = "Payment status is required")]
        public bool IsPaidToOrganizer { get; set; }

        public DateTime? PaidDate { get; set; }

        [StringLength(50, ErrorMessage = "Payment method must not exceed 50 characters")]
        public string? PaymentMethod { get; set; }

        [StringLength(500, ErrorMessage = "Notes must not exceed 500 characters")]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// DTO for organizer payment summary by ticket type
    /// </summary>
    public class OrganizerTicketTypeSummaryDTO
    {
        public int TicketTypeId { get; set; }
        public string TicketTypeName { get; set; } = string.Empty;
        public decimal TicketPrice { get; set; }
        public int TotalIssued { get; set; }
        public int TotalPaid { get; set; }
        public int TotalUnpaid { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal PaidRevenue { get; set; }
        public decimal UnpaidRevenue { get; set; }
        public decimal PaymentPercentage { get; set; }
    }

    /// <summary>
    /// DTO for overall organizer payment summary for an event
    /// </summary>
    public class OrganizerPaymentSummaryDTO
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public DateTime? EventDate { get; set; }
        public int TotalTicketsIssued { get; set; }
        public int TotalTicketsPaid { get; set; }
        public int TotalTicketsUnpaid { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal PaidRevenue { get; set; }
        public decimal UnpaidRevenue { get; set; }
        public decimal OverallPaymentPercentage { get; set; }
        public List<OrganizerTicketTypeSummaryDTO> TicketTypeSummaries { get; set; } = new();
        
        /// <summary>
        /// Aliases for backward compatibility
        /// </summary>
        public int TotalTickets
        {
            get => TotalTicketsIssued;
            set => TotalTicketsIssued = value;
        }
        
        public int PaidTickets
        {
            get => TotalTicketsPaid;
            set => TotalTicketsPaid = value;
        }
        
        public int UnpaidTickets
        {
            get => TotalTicketsUnpaid;
            set => TotalTicketsUnpaid = value;
        }
        
        public decimal PaymentRate
        {
            get => OverallPaymentPercentage;
            set => OverallPaymentPercentage = value;
        }
    }

    /// <summary>
    /// DTO for customer payment search and filtering
    /// </summary>
    public class OrganizerPaymentSearchRequest
    {
        public int EventId { get; set; }
        
        [StringLength(100, ErrorMessage = "Customer name search must not exceed 100 characters")]
        public string? CustomerName { get; set; }
        
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        public string? CustomerEmail { get; set; }
        
        public int? TicketTypeId { get; set; }
        
        public bool? IsPaidToOrganizer { get; set; }
        
        public DateTime? PaidDateFrom { get; set; }
        
        public DateTime? PaidDateTo { get; set; }
        
        [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100")]
        public int PageSize { get; set; } = 20;
        
        [Range(1, int.MaxValue, ErrorMessage = "Page number must be greater than 0")]
        public int PageNumber { get; set; } = 1;
        
        /// <summary>
        /// Alias for PageNumber to maintain backward compatibility
        /// </summary>
        public int Page
        {
            get => PageNumber;
            set => PageNumber = value;
        }
    }

    /// <summary>
    /// DTO for paginated payment results
    /// </summary>
    public class PaginatedOrganizerPaymentsDTO
    {
        public List<OrganizerTicketPaymentDTO> Payments { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
        
        /// <summary>
        /// Alias for PageNumber to maintain backward compatibility
        /// </summary>
        public int Page
        {
            get => PageNumber;
            set => PageNumber = value;
        }
    }

    /// <summary>
    /// DTO for bulk update payment status response
    /// </summary>
    public class BulkUpdatePaymentStatusResponse
    {
        public int UpdatedCount { get; set; }
        public int FailedCount { get; set; }
        public List<int> FailedIds { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }
}
