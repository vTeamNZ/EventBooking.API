using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventBooking.API.Models
{
    /// <summary>
    /// Tracks individual ticket payments for organizer-issued tickets.
    /// Provides granular payment status management and customer information for each ticket.
    /// </summary>
    public class OrganizerTicketPayment
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Reference to the BookingLineItem that contains the consolidated organizer tickets
        /// </summary>
        [Required]
        public int BookingLineItemId { get; set; }

        /// <summary>
        /// Reference to the specific event for this ticket payment
        /// </summary>
        [Required]
        public int EventId { get; set; }

        /// <summary>
        /// Reference to the ticket type for this payment
        /// </summary>
        [Required]
        public int TicketTypeId { get; set; }

        /// <summary>
        /// Customer's first name - REQUIRED field
        /// </summary>
        [Required]
        [StringLength(100, ErrorMessage = "First name must not exceed 100 characters")]
        [Display(Name = "First Name")]
        public string CustomerFirstName { get; set; }

        /// <summary>
        /// Customer's last name - OPTIONAL field
        /// </summary>
        [StringLength(100, ErrorMessage = "Last name must not exceed 100 characters")]
        [Display(Name = "Last Name")]
        public string? CustomerLastName { get; set; }

        /// <summary>
        /// Customer's email address - REQUIRED field
        /// </summary>
        [Required]
        [StringLength(255, ErrorMessage = "Email must not exceed 255 characters")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [Display(Name = "Email Address")]
        public string CustomerEmail { get; set; }

        /// <summary>
        /// Customer's mobile phone - OPTIONAL field
        /// </summary>
        [StringLength(20, ErrorMessage = "Mobile number must not exceed 20 characters")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [Display(Name = "Mobile Number")]
        public string? CustomerMobile { get; set; }

        /// <summary>
        /// Seat details for this specific ticket (JSON format)
        /// Example: {"row": "A", "number": 12, "seatId": 456}
        /// </summary>
        public string? SeatDetails { get; set; }

        /// <summary>
        /// Price paid for this individual ticket
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 9999.99, ErrorMessage = "Ticket price must be between $0 and $9,999.99")]
        public decimal TicketPrice { get; set; }

        /// <summary>
        /// Whether this ticket has been paid to the organizer
        /// </summary>
        [Required]
        public bool IsPaidToOrganizer { get; set; } = false;

        /// <summary>
        /// Date when payment was received by organizer
        /// </summary>
        public DateTime? PaidDate { get; set; }

        /// <summary>
        /// Payment method used by customer (e.g., "Cash", "Bank Transfer", "Cheque")
        /// </summary>
        [StringLength(50, ErrorMessage = "Payment method must not exceed 50 characters")]
        public string? PaymentMethod { get; set; }

        /// <summary>
        /// Additional notes about this payment
        /// </summary>
        [StringLength(500, ErrorMessage = "Notes must not exceed 500 characters")]
        public string? Notes { get; set; }

        /// <summary>
        /// Current status of this ticket payment
        /// </summary>
        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Active";

        /// <summary>
        /// When this payment record was created
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this payment record was last updated
        /// </summary>
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User ID who created this payment record (for audit trail)
        /// </summary>
        [StringLength(450)] // Standard ASP.NET Identity user ID length
        public string? CreatedByUserId { get; set; }

        /// <summary>
        /// User ID who last updated this payment record (for audit trail)
        /// </summary>
        [StringLength(450)] // Standard ASP.NET Identity user ID length
        public string? UpdatedByUserId { get; set; }

        // Navigation properties
        public virtual BookingLineItem? BookingLineItem { get; set; }
        public virtual Event? Event { get; set; }
        public virtual TicketType? TicketType { get; set; }
    }
}
