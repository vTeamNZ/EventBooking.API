using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.Models
{
    public class Reservation
    {
        public int Id { get; set; }

        // Foreign key to Event
        public int EventId { get; set; }
        public Event Event { get; set; } = default!;

        // Foreign key to Identity User
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = default!;

        public string Row { get; set; } = string.Empty;
        public int Number { get; set; }

        public bool IsReserved { get; set; } = true;
        public DateTime ReservedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
    }
}
