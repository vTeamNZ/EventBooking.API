using EventBooking.API.Models;

namespace EventBooking.API.DTOs
{
    public class ReservedSeatDTO
    {
        public int SeatId { get; set; }
        public string Row { get; set; } = string.Empty;
        public int Number { get; set; }
        public string SeatNumber { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public TicketType? TicketType { get; set; }
        public DateTime? ReservedUntil { get; set; }
        public SeatStatus Status { get; set; }
    }
}
