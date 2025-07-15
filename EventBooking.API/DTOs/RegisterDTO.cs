using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.DTOs
{
    public class RegisterDTO
    {
        [Required(ErrorMessage = "Full name is required")]
        [MinLength(2, ErrorMessage = "Full name must be at least 2 characters")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role is required")]
        public string Role { get; set; } = "Attendee"; // Attendee or Organizer

        // Additional fields for organizers
        public string? OrganizationName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Website { get; set; }
    }
}
