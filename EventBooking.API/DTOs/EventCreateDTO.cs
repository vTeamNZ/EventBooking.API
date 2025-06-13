namespace EventBooking.API.DTOs
{
    public class EventCreateDTO
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Location { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Capacity { get; set; }
        public string imageUrl { get; set; } = string.Empty;
    }
}
