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

        /// <summary>
        /// When this booking was refunded/cancelled (whole booking only)
        /// </summary>
        public DateTime? RefundedAt { get; set; }

        /// <summary>
        /// Admin user ID who performed the refund/cancellation
        /// </summary>
        [StringLength(450)]
        public string? RefundedBy { get; set; }

        // JSON field for extensibility
        public string Metadata { get; set; }

        /// <summary>
        /// Check if this booking is refunded
        /// </summary>
        public bool IsRefunded => Status == "Refunded" || Status == "Cancelled";

        // Navigation properties
        public virtual Event Event { get; set; }
        public virtual ICollection<BookingLineItem> BookingLineItems { get; set; }
        public virtual ApplicationUser? RefundedByUser { get; set; }
    }
}
