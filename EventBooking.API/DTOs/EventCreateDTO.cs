using System.ComponentModel.DataAnnotations;
using EventBooking.API.Models;
using System.Text.RegularExpressions;

namespace EventBooking.API.DTOs
{
    public class EventCreateDTO
    {
        [Required(ErrorMessage = "Event title is required")]
        [MinLength(3, ErrorMessage = "Title must be at least 3 characters")]
        [MaxLength(100, ErrorMessage = "Title cannot exceed 100 characters")]
        [RegularExpression(@"^[a-zA-Z0-9\s]+$", ErrorMessage = "Title can only contain letters, numbers, and spaces")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Event description is required")]
        [MinLength(10, ErrorMessage = "Description must be at least 10 characters")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Event date is required")]
        public DateTime Date { get; set; }

        // Either venueId or location is required
        public int? VenueId { get; set; }
        
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

        // Custom validation method
        public bool IsValidTitle()
        {
            if (string.IsNullOrEmpty(Title))
                return false;

            // Check for multiple consecutive spaces
            return !Regex.IsMatch(Title, @"\s{2,}");
        }
    }
}
