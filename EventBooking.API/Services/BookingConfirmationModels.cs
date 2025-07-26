using EventBooking.API.Models;
using EventBooking.API.Services;

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
        
        // ✅ NEW: Summary properties for frontend display
        public ProcessingSummary ProcessingSummary { get; set; } = new();
    }
    
    /// <summary>
    /// Summary of QR generation and email sending for user feedback
    /// </summary>
    public class ProcessingSummary
    {
        public int TotalTickets { get; set; }
        public int SuccessfulQRGenerations { get; set; }
        public int FailedQRGenerations { get; set; }
        public int SuccessfulCustomerEmails { get; set; }
        public int FailedCustomerEmails { get; set; }
        public int SuccessfulOrganizerEmails { get; set; }
        public int FailedOrganizerEmails { get; set; }
        
        // Computed properties for easy frontend display
        public bool AllQRGenerationsSuccessful => FailedQRGenerations == 0 && TotalTickets > 0;
        public bool AllCustomerEmailsSuccessful => FailedCustomerEmails == 0 && TotalTickets > 0;
        public bool AllOrganizerEmailsSuccessful => FailedOrganizerEmails == 0 && TotalTickets > 0;
        public bool HasAnyFailures => FailedQRGenerations > 0 || FailedCustomerEmails > 0 || FailedOrganizerEmails > 0;
        
        public string GetStatusMessage()
        {
            if (TotalTickets == 0) return "No tickets processed";
            if (!HasAnyFailures) return "All tickets and emails processed successfully";
            
            var issues = new List<string>();
            if (FailedQRGenerations > 0) issues.Add($"{FailedQRGenerations} QR generation(s) failed");
            if (FailedCustomerEmails > 0) issues.Add($"{FailedCustomerEmails} customer email(s) failed");
            if (FailedOrganizerEmails > 0) issues.Add($"{FailedOrganizerEmails} organizer email(s) failed");
            
            return $"Issues: {string.Join(", ", issues)}";
        }
    }

    public class QRGenerationResult
    {
        public string SeatNumber { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? TicketPath { get; set; }
        public string? BookingId { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsDuplicate { get; set; }
        
        // ✅ NEW: Properties for consolidated email sending
        public byte[]? QRCodeImage { get; set; }
        public List<FoodOrderInfo>? SeatSpecificFoodOrders { get; set; }
        
        // ✅ NEW: Email sending results for user feedback
        public EmailDeliveryResult CustomerEmailResult { get; set; } = new();
        public EmailDeliveryResult OrganizerEmailResult { get; set; } = new();
    }
    
    /// <summary>
    /// Tracks the success/failure of email delivery for user feedback
    /// </summary>
    public class EmailDeliveryResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime? SentAt { get; set; }
        public string RecipientEmail { get; set; } = string.Empty;
        public string EmailType { get; set; } = string.Empty; // "Customer" or "Organizer"
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
        
        // ✅ NEW: Include processing summary for frontend
        public ProcessingSummary ProcessingSummary { get; set; } = new();
    }
}
