namespace EventBooking.API.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = "Attendee"; // "Attendee", "Organizer", "Admin"
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}
