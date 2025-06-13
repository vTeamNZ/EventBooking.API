using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventBooking.API.Models
{
    public class PaymentRecord
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string PaymentIntentId { get; set; }
        
        [Required]
        public string FirstName { get; set; }
        
        [Required]
        public string LastName { get; set; }
        
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        
        [Required]
        public string Mobile { get; set; }

        public int EventId { get; set; }
        public string EventTitle { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        
        public string TicketDetails { get; set; }
        public string FoodDetails { get; set; }
        
        [Required]
        public string PaymentStatus { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
