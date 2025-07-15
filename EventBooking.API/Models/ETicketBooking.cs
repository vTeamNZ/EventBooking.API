using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.Models
{
    public class ETicketBooking
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string EventName { get; set; } = string.Empty;

        [Required]
        public string SeatNo { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string PaymentGUID { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string BuyerEmail { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string OrganizerEmail { get; set; } = string.Empty;

        [Required]
        public DateTime CreatedAt { get; set; }

        [Required]
        public string TicketPath { get; set; } = string.Empty;

        [Required]
        public string EventID { get; set; } = string.Empty;
    }
}
