using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.Models
{
    public class FoodItem
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public decimal Price { get; set; }

        public string? Description { get; set; }

        [Required]
        public int EventId { get; set; }

        // Navigation property
        public Event? Event { get; set; }
    }
}
