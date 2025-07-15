using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.DTOs
{
    public class TicketTypeCreateDTO
    {
        [Required]
        public string Type { get; set; } = string.Empty;

        [Required]
        public decimal Price { get; set; }

        public string? Description { get; set; }

        [Required]
        public int EventId { get; set; }
        
        // Color for visual representation in the UI
        public string Color { get; set; } = "#3B82F6"; // Default blue color

        public List<SeatRowAssignmentDTO> SeatRows { get; set; } = new List<SeatRowAssignmentDTO>();
    }

    public class SeatRowAssignmentDTO
    {
        [Required]
        public string RowStart { get; set; } = string.Empty;

        [Required]
        public string RowEnd { get; set; } = string.Empty;

        public int MaxTickets { get; set; }
    }
}
