using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.DTOs
{
    /// <summary>
    /// DTO for displaying ticket type with state information
    /// </summary>
    public class TicketTypeWithStateDTO
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? Description { get; set; }
        public int EventId { get; set; }
        public string Color { get; set; } = string.Empty;
        public int? MaxTickets { get; set; }
        public bool IsStanding { get; set; }
        public int? StandingCapacity { get; set; }
        
        // State management properties
        public bool IsActive { get; set; }
        public bool IsHidden { get; set; }
        public int? ReplacedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DisabledAt { get; set; }
        public DateTime? HiddenAt { get; set; }
        
        // Computed properties
        public bool IsAvailableForPurchase => IsActive && !IsHidden;
        public bool IsVisibleToCustomers => !IsHidden;
        public bool HasReplacement => ReplacedBy.HasValue;
        
        // Replacement ticket info (if any)
        public string? ReplacedByTicketName { get; set; }
    }

    /// <summary>
    /// Request DTO for disabling a ticket type
    /// </summary>
    public class DisableTicketTypeRequest
    {
        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Request DTO for hiding a ticket type
    /// </summary>
    public class HideTicketTypeRequest
    {
        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Request DTO for replacing a ticket type
    /// </summary>
    public class ReplaceTicketTypeRequest
    {
        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(7)]
        [RegularExpression("^#[0-9A-Fa-f]{6}$")]
        public string? Color { get; set; }

        public int? MaxTickets { get; set; }
        public bool IsStanding { get; set; } = false;
        public int? StandingCapacity { get; set; }

        [MaxLength(500)]
        public string? ReplacementReason { get; set; }

        /// <summary>
        /// Whether to hide the original ticket type (true) or just disable it (false)
        /// </summary>
        public bool HideOriginal { get; set; } = true;
    }

    /// <summary>
    /// Response DTO for ticket type state operations
    /// </summary>
    public class TicketTypeStateResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public TicketTypeWithStateDTO? TicketType { get; set; }
        public TicketTypeWithStateDTO? ReplacementTicketType { get; set; }
    }
}