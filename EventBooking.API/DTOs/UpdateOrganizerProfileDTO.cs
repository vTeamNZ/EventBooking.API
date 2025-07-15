using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.DTOs
{
    public class UpdateOrganizerProfileDTO
    {
        [MinLength(2, ErrorMessage = "Name must be at least 2 characters")]
        public string? Name { get; set; }

        public string? OrganizationName { get; set; }

        [Phone(ErrorMessage = "Invalid phone number format")]
        public string? PhoneNumber { get; set; }

        [Url(ErrorMessage = "Invalid website URL")]
        public string? Website { get; set; }

        [Url(ErrorMessage = "Invalid Facebook URL")]
        public string? FacebookUrl { get; set; }

        [Url(ErrorMessage = "Invalid YouTube URL")]
        public string? YoutubeUrl { get; set; }
    }
}
