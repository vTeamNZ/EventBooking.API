namespace EventBooking.API.Models
{
    public class TableReservation
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = default!;

        public int TableId { get; set; }
        public Table Table { get; set; } = default!;

        public int SeatsReserved { get; set; }
        public DateTime ReservedAt { get; set; } = DateTime.UtcNow;
    }
}
