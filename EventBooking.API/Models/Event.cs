using System.Collections.Generic;

namespace EventBooking.API.Models
{
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
        public Organizer? Organizer { get; set; }        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }

        // Navigation properties for ticket types and food items
        public ICollection<TicketType> TicketTypes { get; set; } = new List<TicketType>();
        public ICollection<FoodItem> FoodItems { get; set; } = new List<FoodItem>();
    }
}
