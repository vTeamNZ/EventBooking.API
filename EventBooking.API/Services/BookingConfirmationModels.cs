using EventBooking.API.Models;

namespace EventBooking.API.Services
{
    public class BookingConfirmationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public List<string> BookedSeats { get; set; } = new();
        public decimal AmountTotal { get; set; }
        public string TicketReference { get; set; } = string.Empty;
        public List<QRGenerationResult> QRResults { get; set; } = new();
        public int BookingId { get; set; }
    }

    public class QRGenerationResult
    {
        public string SeatNumber { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? TicketPath { get; set; }
        public string? BookingId { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class QRApiResult
    {
        public bool Success { get; set; }
        public string? TicketPath { get; set; }
        public string? BookingId { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class QRApiResponse
    {
        public string? TicketPath { get; set; }
        public string? BookingId { get; set; }
        public bool IsDuplicate { get; set; }
        public string? Message { get; set; }
    }

    public class WebhookPaymentStatusResponse
    {
        public bool IsProcessed { get; set; }
        public DateTime ProcessedAt { get; set; }
        public BookingDetailsResponse? BookingDetails { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class BookingDetailsResponse
    {
        public string EventTitle { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public List<string> BookedSeats { get; set; } = new();
        public decimal AmountTotal { get; set; }
        public string PaymentId { get; set; } = string.Empty;
        public List<QRGenerationResult> QRTicketsGenerated { get; set; } = new();
        public string TicketReference { get; set; } = string.Empty;
        public int BookingId { get; set; }
        public string ProcessedAt { get; set; } = string.Empty;
    }
}
