using EventBooking.API.Models;

namespace EventBooking.API.Services
{
    public interface IQRTicketService
    {
        Task<QRTicketResult> GenerateQRTicketAsync(QRTicketRequest request);
        byte[] GenerateQrCode(string eventId, string eventName, string seatNumber, string firstName, string paymentGuid);
        Task<byte[]> GenerateTicketPdfAsync(string eventId, string eventName, string seatNumber, string firstName, byte[] qrCodeImage, List<FoodOrderInfo>? foodOrders = null);
        string SaveTicketLocally(byte[] pdfTicket, string eventId, string eventName, string firstName, string paymentGuid);
        List<string> ListStoredTickets();
        bool DeleteStoredTicket(string fileName);
    }

    public class QRTicketRequest
    {
        public string EventId { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        public string SeatNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string PaymentGuid { get; set; } = string.Empty;
        public string BuyerEmail { get; set; } = string.Empty;
        public string OrganizerEmail { get; set; } = string.Empty;
        public int? BookingId { get; set; } // Link to main Bookings table
        public List<FoodOrderInfo> FoodOrders { get; set; } = new(); // ✅ Individual food orders for this ticket
    }

    public class FoodOrderInfo
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string? Description { get; set; }
    }

    public class QRTicketResult
    {
        public bool Success { get; set; }
        public string? TicketPath { get; set; }
        public string? BookingId { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsDuplicate { get; set; }
    }
}
