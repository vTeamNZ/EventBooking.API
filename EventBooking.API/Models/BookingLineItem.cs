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

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual Booking Booking { get; set; }
    }
}
