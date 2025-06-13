namespace EventBooking.API.DTOs
{
    public class ReservationCreateDTO
    {
        public int EventId { get; set; }
        public string Row { get; set; } = string.Empty;
        public int Number { get; set; }
        public bool IncludeFood { get; set; } = false;
    }
}
