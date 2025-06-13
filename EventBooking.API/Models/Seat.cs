namespace EventBooking.API.Models
{
    public class Seat
    {
        public int Id { get; set; }
        public int EventId { get; set; }
        public Event Event { get; set; }

        public string Row { get; set; } = string.Empty;  // e.g., "A"
        public int Number { get; set; }  // e.g., 1, 2, 3
        public bool IsReserved { get; set; } = false;
    }
}
