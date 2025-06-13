using Microsoft.AspNetCore.Identity;

namespace EventBooking.API.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = "Attendee"; // Attendee or Organizer
    }
}
