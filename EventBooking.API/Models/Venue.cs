namespace EventBooking.API.Models
{
    public class Venue
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string LayoutData { get; set; } = string.Empty; // JSON configuration for custom layouts
        public int Width { get; set; } = 800; // Layout width in pixels
        public int Height { get; set; } = 600; // Layout height in pixels

        // Navigation properties
        public ICollection<Event> Events { get; set; } = new List<Event>();
        public ICollection<Section> Sections { get; set; } = new List<Section>();
    }
}
