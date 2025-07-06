using System.Collections.Generic;

namespace EventBooking.API.Models
{
    public enum SeatSelectionMode
    {
        EventHall = 1,
        TableSeating = 2,
        GeneralAdmission = 3
    }

    public class Event
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
        public string Location { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public int? Capacity { get; set; }

        public int? OrganizerId { get; set; }
        public Organizer? Organizer { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }

        // Seat selection configuration
        public SeatSelectionMode SeatSelectionMode { get; set; } = SeatSelectionMode.GeneralAdmission;
        public int? VenueId { get; set; }
        public Venue? Venue { get; set; }
        public string? StagePosition { get; set; } // JSON: {"x": 50, "y": 10, "width": 100, "height": 20}

        // Navigation properties for ticket types and food items
        public ICollection<TicketType> TicketTypes { get; set; } = new List<TicketType>();
        public ICollection<FoodItem> FoodItems { get; set; } = new List<FoodItem>();
        public ICollection<Seat> Seats { get; set; } = new List<Seat>();
        public ICollection<Table> Tables { get; set; } = new List<Table>();
    }
}
