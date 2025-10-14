using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.Models
{
    public class TicketType
    {
        public int Id { get; set; }

        [Required]
        public string Type { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty; // Display name for UI

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        public string? Description { get; set; }

        [Required]
        public int EventId { get; set; }

        // Seat row assignments for venues with allocated seating
        public string? SeatRowAssignments { get; set; } = string.Empty; // JSON string storing seat row assignments
        
        // Color for visual representation in the UI (replaces Section color)
        [Required]
        [MaxLength(7)]
        [RegularExpression("^#[0-9A-Fa-f]{6}$")]
        public string Color { get; set; } = "#3B82F6"; // Default blue color

        // Maximum number of tickets available for this type (for General Admission events)
        public int? MaxTickets { get; set; }

        // Standing ticket configuration (for Hybrid events)
        public bool IsStanding { get; set; } = false;
        public int? StandingCapacity { get; set; }

        // State management properties
        public bool IsActive { get; set; } = true;
        public bool IsHidden { get; set; } = false;
        public int? ReplacedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DisabledAt { get; set; }
        public DateTime? HiddenAt { get; set; }

        // Navigation properties
        public Event? Event { get; set; }
        public ICollection<Seat> Seats { get; set; } = new List<Seat>();
        public TicketType? ReplacedByTicketType { get; set; }
        public ICollection<TicketType> ReplacedTicketTypes { get; set; } = new List<TicketType>();

        // Computed properties for business logic
        public bool IsAvailableForPurchase => IsActive && !IsHidden;
        public bool IsVisibleToCustomers => !IsHidden;
        public bool HasReplacement => ReplacedBy.HasValue;

        // Helper methods for state transitions
        public void Disable()
        {
            IsActive = false;
            DisabledAt = DateTime.UtcNow;
        }

        public void Hide()
        {
            IsActive = false;
            IsHidden = true;
            HiddenAt = DateTime.UtcNow;
            
            // If not already disabled, set disabled timestamp too
            if (DisabledAt == null)
            {
                DisabledAt = DateTime.UtcNow;
            }
        }

        public void Reactivate()
        {
            IsActive = true;
            IsHidden = false;
            // Keep timestamps for history - don't reset them
        }

        public void SetReplacement(int replacementTicketTypeId)
        {
            ReplacedBy = replacementTicketTypeId;
            Hide(); // When replaced, hide the original ticket type
        }
    }
}
