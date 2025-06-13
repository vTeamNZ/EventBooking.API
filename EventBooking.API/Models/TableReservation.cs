namespace EventBooking.API.Models
{
    public class TableReservation
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; }

        public int TableId { get; set; }
        public Table Table { get; set; }

        public int SeatsReserved { get; set; }
        public DateTime ReservedAt { get; set; } = DateTime.UtcNow;
    }
}
