using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventBooking.API.Models.Payments
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(255)]
        public string PaymentIntentId { get; set; } = string.Empty;

        public int EventId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(10)]
        public string Currency { get; set; } = "nzd";

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "pending";

        [ForeignKey("EventId")]
        public Event Event { get; set; } = null!;

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string TicketDetails { get; set; } = "[]";

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string FoodDetails { get; set; } = "[]";

        public DateTime? UpdatedAt { get; set; }

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [StringLength(255)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [StringLength(50)]
        [Phone]
        public string Mobile { get; set; } = string.Empty;
    }
}
