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

        // Navigation properties
        public Event? Event { get; set; }
        public ICollection<Seat> Seats { get; set; } = new List<Seat>();
    }
}
