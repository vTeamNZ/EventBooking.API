using System;

namespace EventBooking.API.Models
{
    public class SeatReservation
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public int Row { get; set; }
        public int Number { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public DateTime ReservedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool IsConfirmed { get; set; }
        public string? UserId { get; set; }

        // Navigation properties
        public Event Event { get; set; } = null!;
    }
}
