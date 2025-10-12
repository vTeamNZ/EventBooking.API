using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventBooking.API.Models
{
    public class BookingLineItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BookingId { get; set; }

        [Required]
        [StringLength(20)]
        public string ItemType { get; set; } // 'Ticket', 'Food', 'Merchandise'

        [Required]
        public int ItemId { get; set; } // TicketTypeId or FoodItemId

        [Required]
        [StringLength(255)]
        public string ItemName { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        // JSON fields for flexibility
        public string SeatDetails { get; set; } // JSON: {"row": "A", "number": 1, "seatId": 123}
        public string ItemDetails { get; set; } // JSON: Additional item-specific data

        public string QRCode { get; set; } // Generated QR for tickets

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Active";

        /// <summary>
        /// When this line item was refunded/cancelled (follows parent booking)
        /// </summary>
        public DateTime? RefundedAt { get; set; }

        /// <summary>
        /// Admin user ID who performed the refund/cancellation
        /// </summary>
        [StringLength(450)]
        public string? RefundedBy { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Check if this line item is refunded
        /// </summary>
        public bool IsRefunded => Status == "Refunded" || Status == "Cancelled";

        // Navigation properties
        public virtual Booking Booking { get; set; }
        public virtual ApplicationUser? RefundedByUser { get; set; }
    }
}
