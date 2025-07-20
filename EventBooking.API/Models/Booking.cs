using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventBooking.API.Models
{
    public class Booking
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int EventId { get; set; }

        [Required]
        [StringLength(255)]
        public string CustomerEmail { get; set; }

        [Required]
        [StringLength(100)]
        public string CustomerFirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string CustomerLastName { get; set; }

        [StringLength(20)]
        public string CustomerMobile { get; set; }

        [Required]
        [StringLength(255)]
        public string PaymentIntentId { get; set; }

        [Required]
        [StringLength(50)]
        public string PaymentStatus { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ProcessingFee { get; set; } = 0;

        [Required]
        [StringLength(10)]
        public string Currency { get; set; } = "NZD";

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Active";

        // JSON field for extensibility
        public string Metadata { get; set; }

        // Navigation properties
        public virtual Event Event { get; set; }
        public virtual ICollection<BookingLineItem> BookingLineItems { get; set; }
    }
}
