namespace EventBooking.API.Models
{
    public class Venue
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        
        // Layout configuration
        public string LayoutType { get; set; } = "Theater"; // Theater, Classroom, Banquet, etc.
        public string LayoutData { get; set; } = string.Empty; // JSON configuration for custom layouts
        public int Width { get; set; } = 800; // Layout width in pixels
        public int Height { get; set; } = 600; // Layout height in pixels
        
        // Seat selection configuration
        public SeatSelectionMode SeatSelectionMode { get; set; } = SeatSelectionMode.GeneralAdmission;
        
        // Seating configuration
        public int NumberOfRows { get; set; }
        public int SeatsPerRow { get; set; }
        public int RowSpacing { get; set; } = 40; // Space between rows in pixels
        public int SeatSpacing { get; set; } = 30; // Space between seats in pixels
        public bool HasStaggeredSeating { get; set; } = false; // For better viewing angles
        public bool HasWheelchairSpaces { get; set; } = false;
        public int WheelchairSpaces { get; set; } = 0;
        
        // Aisle configuration
        public bool HasHorizontalAisles { get; set; } = false;
        public string HorizontalAisleRows { get; set; } = string.Empty; // JSON array of row indices where aisles should be placed
        public bool HasVerticalAisles { get; set; } = false;
        public string VerticalAisleSeats { get; set; } = string.Empty; // JSON array of seat indices where aisles should be placed
        public int AisleWidth { get; set; } = 2; // Width of aisles in seat units
        
        // Navigation properties
        public ICollection<Event> Events { get; set; } = new List<Event>();
        public ICollection<Section> Sections { get; set; } = new List<Section>();

        // Computed total capacity
        public int Capacity => NumberOfRows * SeatsPerRow;
    }
}
