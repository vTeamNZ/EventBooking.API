using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.DTOs
{
    public class UserLockoutDTO
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        [Required]
        public bool IsLocked { get; set; }
    }
}
