using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.Models
{
    /// <summary>
    /// Industry-standard seat reservation model for tracking temporary seat holds
    /// Matches database schema: SeatReservations table
    /// </summary>
    public class SeatReservation
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int EventId { get; set; }
        
        [Required]
        public int Row { get; set; }
        
        [Required]
        public int Number { get; set; }
        
        [Required]
        public string SessionId { get; set; } = string.Empty;
        
        [Required]
        public DateTime ReservedAt { get; set; }
        
        [Required]
        public DateTime ExpiresAt { get; set; }
        
        public bool IsConfirmed { get; set; } = false;
        
        public string? UserId { get; set; }
        
        /// <summary>
        /// Reservation identifier for grouping multiple seat reservations
        /// </summary>
        public string? ReservationId { get; set; }
        
        /// <summary>
        /// Reference to the actual seat being reserved
        /// </summary>
        public int? SeatId { get; set; }
        
        // Navigation properties
        public Event? Event { get; set; }
        public Seat? Seat { get; set; }
    }
}
