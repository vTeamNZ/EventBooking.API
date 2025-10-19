using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.Models
{
    /// <summary>
    /// Request model for QR code validation
    /// </summary>
    public class QRValidationRequest
    {
        [Required]
        public string QRData { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional: Location where the scan occurred (e.g., "Main Entrance", "VIP Gate")
        /// </summary>
        public string? ScanLocation { get; set; }
        
        /// <summary>
        /// Optional: Additional scan context or notes
        /// </summary>
        public string? ScanNotes { get; set; }
    }

    /// <summary>
    /// Response model for QR code validation
    /// </summary>
    public class QRValidationResponse
    {
        /// <summary>
        /// Indicates if the API call was successful
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Overall validation result
        /// </summary>
        public bool IsValid { get; set; }
        
        /// <summary>
        /// Validation status: Valid, Invalid, AlreadyUsed, Expired, NotFound
        /// </summary>
        public string Status { get; set; } = string.Empty;
        
        /// <summary>
        /// Human-readable message about the validation result
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// Parsed QR data components
        /// </summary>
        public QRDataComponents? QRData { get; set; }
        
        /// <summary>
        /// Ticket information from database (if found)
        /// </summary>
        public TicketInfo? Ticket { get; set; }
        
        /// <summary>
        /// Event information
        /// </summary>
        public EventInfo? Event { get; set; }
        
        /// <summary>
        /// Entry tracking information
        /// </summary>
        public EntryInfo? Entry { get; set; }
        
        /// <summary>
        /// Validation timestamp
        /// </summary>
        public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Parsed components from QR code data
    /// </summary>
    public class QRDataComponents
    {
        public string EventID { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        public string SeatNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string PaymentGUID { get; set; } = string.Empty;
        
        /// <summary>
        /// Raw QR data string
        /// </summary>
        public string RawData { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether QR data was successfully parsed
        /// </summary>
        public bool IsParsed { get; set; }
    }

    /// <summary>
    /// Ticket information from database
    /// </summary>
    public class TicketInfo
    {
        public int? BookingId { get; set; }
        public int? LineItemId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string SeatNumber { get; set; } = string.Empty;
        public string TicketType { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime BookingDate { get; set; }
        public string QRCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        
        /// <summary>
        /// Any food orders associated with this ticket
        /// </summary>
        public List<FoodOrderDetail> FoodOrders { get; set; } = new();
    }

    /// <summary>
    /// Event information
    /// </summary>
    public class EventInfo
    {
        public int EventId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string OrganizerName { get; set; } = string.Empty;
        public string OrganizerEmail { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Entry tracking information
    /// </summary>
    public class EntryInfo
    {
        public bool HasPreviousEntry { get; set; }
        public DateTime? FirstEntryTime { get; set; }
        public DateTime? LastEntryTime { get; set; }
        public int EntryCount { get; set; }
        public string? LastScanLocation { get; set; }
        public bool AllowReEntry { get; set; } = true;
    }

    /// <summary>
    /// Food order details associated with ticket
    /// </summary>
    public class FoodOrderDetail
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Entry log model for tracking ticket usage
    /// </summary>
    public class QREntryLog
    {
        public int Id { get; set; }
        public string QRData { get; set; } = string.Empty;
        public string? EventID { get; set; }
        public string? PaymentGUID { get; set; }
        public string? SeatNumber { get; set; }
        public string? AttendeeeName { get; set; }
        public DateTime ScanTime { get; set; } = DateTime.UtcNow;
        public string? ScanLocation { get; set; }
        public string ValidationResult { get; set; } = string.Empty;
        public string? ScanNotes { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
    }
}