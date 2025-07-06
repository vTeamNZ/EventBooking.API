namespace EventBooking.API.DTOs
{
    public class SeatHoldRequest
    {
        public int EventId { get; set; }
        public string Row { get; set; } = string.Empty;
        public int Number { get; set; }
    }
}
