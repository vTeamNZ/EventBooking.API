using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.Models
{
    public class TicketType
    {
        public int Id { get; set; }

        [Required]
        public string Type { get; set; } = string.Empty;

        [Required]
        public decimal Price { get; set; }

        public string? Description { get; set; }

        [Required]
        public int EventId { get; set; }

        // Seat row assignments for venues with allocated seating
        public string? SeatRowAssignments { get; set; } = string.Empty; // JSON string storing seat row assignments

        // Navigation property
        public Event? Event { get; set; }
    }
}
