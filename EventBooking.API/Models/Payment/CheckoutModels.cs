using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EventBooking.API.Models.Payment
{
    public class CreateCheckoutSessionRequest
    {
        [Required]
        public int EventId { get; set; }

        [Required]
        public string EventTitle { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [StringLength(100)]
        public string? FirstName { get; set; }

        [StringLength(100)]
        public string? LastName { get; set; }

        [Phone]
        [StringLength(20)]
        public string? Mobile { get; set; }

        [Required]
        public string SuccessUrl { get; set; }

        [Required]
        public string CancelUrl { get; set; }

        public List<TicketLineItem>? TicketDetails { get; set; }
        public List<FoodLineItem>? FoodDetails { get; set; }
        public List<string>? SelectedSeats { get; set; }
        public string? UserSessionId { get; set; }
    }

    public class TicketLineItem
    {
        [Required]
        public string Type { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal UnitPrice { get; set; }
    }

    public class FoodLineItem
    {
        [Required]
        public string Name { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal UnitPrice { get; set; }
        
        // Seat/ticket association fields for individual food selection
        public string? SeatTicketId { get; set; }
        public string? SeatTicketType { get; set; }
    }

    public class CreateCheckoutSessionResponse
    {
        public string SessionId { get; set; }
        public string Url { get; set; }
    }

    public class CheckoutSessionStatusResponse
    {
        public string Status { get; set; }
        public string PaymentStatus { get; set; }
        public bool IsSuccessful { get; set; }
        public string CustomerEmail { get; set; }
        public long? AmountTotal { get; set; }
        public string PaymentId { get; set; }
        public string EventTitle { get; set; }
        public List<string> BookedSeats { get; set; } = new List<string>();
        public string CustomerName { get; set; }
        public string TicketReference { get; set; }
    }
}
