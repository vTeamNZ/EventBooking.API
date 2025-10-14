using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.DTOs
{
    /// <summary>
    /// DTO for organizer ticket sales management table display
    /// </summary>
    public class OrganizerTicketSalesDTO
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string CustomerFirstName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100)]
        public string CustomerLastName { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string CustomerEmail { get; set; } = string.Empty;
        
        /// <summary>
        /// Seat details for this ticket (seat/ticket numbers)
        /// </summary>
        public string? SeatDetails { get; set; }
        
        public decimal TicketPrice { get; set; }
        
        public bool IsPaid { get; set; }
        
        [Required]
        public string Status { get; set; } = "Active";
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime UpdatedAt { get; set; }
        
        /// <summary>
        /// Full customer name for display purposes
        /// </summary>
        public string CustomerFullName => $"{CustomerFirstName} {CustomerLastName}".Trim();
        
        /// <summary>
        /// Check if this ticket is cancelled
        /// </summary>
        public bool IsCancelled => Status == "Cancelled";
    }
    
    /// <summary>
    /// Request DTO for updating customer details
    /// </summary>
    public class UpdateCustomerDetailsRequest
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string CustomerFirstName { get; set; } = string.Empty;
        
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string CustomerLastName { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        [StringLength(255)]
        public string CustomerEmail { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Request DTO for toggling payment status
    /// </summary>
    public class TogglePaymentRequest
    {
        public bool IsPaid { get; set; }
    }
    
    /// <summary>
    /// Response DTO for simple operations
    /// </summary>
    public class SimpleOperationResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
    }
}
