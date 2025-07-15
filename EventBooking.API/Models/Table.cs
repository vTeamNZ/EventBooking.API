namespace EventBooking.API.Models
{
    public class Table
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public Event Event { get; set; } = null!;

        public string TableNumber { get; set; } = string.Empty; // e.g., T01, VIP-A, etc.
        public int Capacity { get; set; } = 8;

        // Position for visual layout
        public decimal X { get; set; }
        public decimal Y { get; set; }
        public decimal Width { get; set; } = 80;
        public decimal Height { get; set; } = 80;
        public string Shape { get; set; } = "round"; // round, square, rectangle

        // Pricing
        public decimal PricePerSeat { get; set; }
        public decimal? TablePrice { get; set; } // For full table booking

        // TicketType relationship (replaces Section)
        public int? TicketTypeId { get; set; }
        public TicketType? TicketType { get; set; }

        // Navigation properties
        public ICollection<TableReservation> TableReservations { get; set; } = new List<TableReservation>();
        public ICollection<Seat> Seats { get; set; } = new List<Seat>(); // Individual seats at table
    }
}
