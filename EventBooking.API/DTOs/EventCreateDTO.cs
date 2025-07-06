using System.ComponentModel.DataAnnotations;
using EventBooking.API.Models;

namespace EventBooking.API.DTOs
{
    public class EventCreateDTO
    {
        [Required(ErrorMessage = "Event title is required")]
        [MinLength(3, ErrorMessage = "Title must be at least 3 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Event description is required")]
        [MinLength(10, ErrorMessage = "Description must be at least 10 characters")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Event date is required")]
        public DateTime Date { get; set; }

        [Required(ErrorMessage = "Event location is required")]
        [MinLength(3, ErrorMessage = "Location must be at least 3 characters")]
        public string Location { get; set; } = string.Empty;

        [Required(ErrorMessage = "Event price is required")]
        [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Event capacity is required")]
        [Range(1, 10000, ErrorMessage = "Capacity must be between 1 and 10,000")]
        public int Capacity { get; set; }

        public IFormFile? Image { get; set; }

        [Required(ErrorMessage = "Seat selection mode is required")]
        public SeatSelectionMode SeatSelectionMode { get; set; } = SeatSelectionMode.GeneralAdmission;

        public string? StagePosition { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
