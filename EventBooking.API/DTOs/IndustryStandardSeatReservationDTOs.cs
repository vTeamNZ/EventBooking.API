using EventBooking.API.Models;

namespace EventBooking.API.DTOs
{
    /// <summary>
    /// Request for reserving multiple seats in a single transaction (Industry Standard)
    /// Uses session for initial reservation, then links to payment email/name
    /// </summary>
    public class ReserveSeatSelectionRequest
    {
        public int EventId { get; set; }
        public List<int> SeatIds { get; set; } = new List<int>();
        public string SessionId { get; set; } = string.Empty;
        public string? UserId { get; set; } // Optional for authenticated users
    }

    /// <summary>
    /// Response when seats are successfully reserved
    /// </summary>
    public class ReservationResponse
    {
        public string ReservationId { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public List<ReservedSeatInfo> ReservedSeats { get; set; } = new List<ReservedSeatInfo>();
        public decimal TotalPrice { get; set; }
        public int SeatsCount { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string? Message { get; set; } // For status messages
    }

    /// <summary>
    /// Information about a reserved seat
    /// </summary>
    public class ReservedSeatInfo
    {
        public int SeatId { get; set; }
        public string SeatNumber { get; set; } = string.Empty;
        public string Row { get; set; } = string.Empty;
        public int Number { get; set; }
        public decimal Price { get; set; }
        public int TicketTypeId { get; set; }
        public string TicketTypeName { get; set; } = string.Empty;
        public string TicketTypeColor { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to extend reservation time
    /// </summary>
    public class ExtendReservationRequest
    {
        public string ReservationId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public int ExtendMinutes { get; set; } = 5; // Default extend by 5 minutes
    }

    /// <summary>
    /// Request to release all seats for a session
    /// </summary>
    public class ReleaseReservationRequest
    {
        public string SessionId { get; set; } = string.Empty;
        public string? ReservationId { get; set; } // Optional - if not provided, releases all for session
    }

    /// <summary>
    /// Response for seat availability check
    /// </summary>
    public class SeatAvailabilityResponse
    {
        public List<int> AvailableSeatIds { get; set; } = new List<int>();
        public List<int> UnavailableSeatIds { get; set; } = new List<int>();
        public List<UnavailableSeatInfo> UnavailableDetails { get; set; } = new List<UnavailableSeatInfo>();
    }

    /// <summary>
    /// Details about why a seat is unavailable
    /// </summary>
    public class UnavailableSeatInfo
    {
        public int SeatId { get; set; }
        public string SeatNumber { get; set; } = string.Empty;
        public SeatStatus Status { get; set; }
        public DateTime? ReservedUntil { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to check if seats are still available before reservation
    /// </summary>
    public class CheckSeatAvailabilityRequest
    {
        public int EventId { get; set; }
        public List<int> SeatIds { get; set; } = new List<int>();
    }

    /// <summary>
    /// Request to mark seats as booked after successful payment
    /// </summary>
    public class ConfirmReservationRequest
    {
        public string ReservationId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string PaymentIntentId { get; set; } = string.Empty;
        public string BuyerEmail { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request to link a session-based reservation to payment information
    /// This connects anonymous seat reservations with user payment details
    /// </summary>
    public class LinkReservationToPaymentRequest
    {
        public int EventId { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Mobile { get; set; }
    }
}
