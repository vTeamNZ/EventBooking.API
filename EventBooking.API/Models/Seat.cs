namespace EventBooking.API.Models
{
    public enum SeatStatus
    {
        Available = 0,
        Reserved = 1,
        Booked = 2,
        Unavailable = 3
    }

    public class Seat
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public Event Event { get; set; } = null!;

        public string Row { get; set; } = string.Empty;  // e.g., "A"
        public int Number { get; set; }  // e.g., 1, 2, 3
        public string SeatNumber { get; set; } = string.Empty; // Combined: A1, B5, etc.
        
        // Position for visual layout
        public decimal X { get; set; }
        public decimal Y { get; set; }
        public decimal Width { get; set; } = 30;
        public decimal Height { get; set; } = 30;

        // Pricing and availability
        public decimal Price { get; set; }
        public SeatStatus Status { get; set; } = SeatStatus.Available;

        // Temporary reservation
        public DateTime? ReservedUntil { get; set; }
        public string? ReservedBy { get; set; } // Session ID or User ID

        // TicketType relationship (replaces Section)
        public int? TicketTypeId { get; set; }
        public TicketType? TicketType { get; set; }

        // Table relationship (for table seating mode)
        public int? TableId { get; set; }
        public Table? Table { get; set; }

        public bool IsReserved { get; set; } = false; // Keep for backward compatibility
    }
}
