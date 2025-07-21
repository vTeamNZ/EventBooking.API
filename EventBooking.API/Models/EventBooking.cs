using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventBooking.API.Models
{
    [Table("EventBookings")]
    public class EventBookingRecord
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
        public string BuyerEmail { get; set; } = string.Empty;

        [Required]
        public string OrganizerEmail { get; set; } = string.Empty;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string TicketPath { get; set; } = string.Empty;

        [Required]
        public string EventID { get; set; } = string.Empty;
    }
}