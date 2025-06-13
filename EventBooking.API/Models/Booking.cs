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
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        // Navigation properties
        public virtual Event Event { get; set; }
        public virtual ICollection<BookingTicket> BookingTickets { get; set; }
        public virtual ICollection<BookingFood> BookingFoods { get; set; }
    }
}
