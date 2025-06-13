namespace EventBooking.API.Models
{
    public class Table
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public Event Event { get; set; }

        public string TableNumber { get; set; } = string.Empty; // e.g., T01, VIP-A, etc.
        public int Capacity { get; set; } = 8;

        public ICollection<TableReservation> TableReservations { get; set; } = new List<TableReservation>();

    }
}
