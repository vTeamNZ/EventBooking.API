namespace EventBooking.API.Models
{
    public class Section
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // VIP, General, Balcony, etc.
        public string Color { get; set; } = "#3B82F6"; // Section color for UI
        public decimal BasePrice { get; set; }
        public int VenueId { get; set; }
        public Venue Venue { get; set; } = null!;

        // Navigation properties
        public ICollection<Seat> Seats { get; set; } = new List<Seat>();
        public ICollection<Table> Tables { get; set; } = new List<Table>();
    }
}
