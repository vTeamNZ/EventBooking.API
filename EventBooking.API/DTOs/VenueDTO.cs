using EventBooking.API.Models;

namespace EventBooking.API.DTOs
{
    public class VenueDTO
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string LayoutType { get; set; } = "Theater";
        public string LayoutData { get; set; } = string.Empty;
        public int Width { get; set; } = 800;
        public int Height { get; set; } = 600;
        public int NumberOfRows { get; set; }
        public int SeatsPerRow { get; set; }
        public int RowSpacing { get; set; } = 40;
        public int SeatSpacing { get; set; } = 30;
        public bool HasStaggeredSeating { get; set; } = false;
        public bool HasWheelchairSpaces { get; set; } = false;
        public int WheelchairSpaces { get; set; } = 0;
        
        // Aisle configuration
        public bool HasHorizontalAisles { get; set; } = false;
        public string HorizontalAisleRows { get; set; } = string.Empty;
        public bool HasVerticalAisles { get; set; } = false;
        public string VerticalAisleSeats { get; set; } = string.Empty;
        public int AisleWidth { get; set; } = 2;
        
        // Seat selection mode
        public SeatSelectionMode SeatSelectionMode { get; set; } = SeatSelectionMode.EventHall;
    }

    public class VenueUpdateDTO : VenueDTO
    {
        public int Id { get; set; }
    }
}
