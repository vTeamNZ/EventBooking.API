using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventBooking.API.Models
{
    public class BookingFood
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int BookingId { get; set; }

        [Required]
        public int FoodItemId { get; set; }

        [Required]
        public int Quantity { get; set; }

        // Navigation properties
        public virtual Booking Booking { get; set; }
        public virtual FoodItem FoodItem { get; set; }
    }
}
