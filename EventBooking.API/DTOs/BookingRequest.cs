namespace EventBooking.API.DTOs
{
    public class BookingRequest
    {
        public int EventId { get; set; }
        public List<BookingTicketRequest> Tickets { get; set; } = new();
        public List<BookingFoodRequest> FoodItems { get; set; } = new();
    }

    public class BookingTicketRequest
    {
        public int TicketTypeId { get; set; }
        public int Quantity { get; set; }
    }

    public class BookingFoodRequest
    {
        public int FoodItemId { get; set; }
        public int Quantity { get; set; }
    }
}
