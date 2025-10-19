using EventBooking.API.Models;
using System;

namespace EventBooking.API.Services
{
    public interface IQRTicketService
    {
        Task<QRTicketResult> GenerateQRTicketAsync(QRTicketRequest request);
        byte[] GenerateQrCode(string eventId, string eventName, string seatNumber, string firstName, string paymentGuid);
        
        /// <summary>
        /// Generates a professional concert ticket with enhanced styling and features.
        /// This is the preferred ticket generation method for all use cases.
        /// </summary>
        Task<byte[]> GenerateProfessionalConcertTicketAsync(string eventId, string eventName, string seatNumber, string firstName, byte[] qrCodeImage, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null, string? ticketType = null, string? bookingReference = null, bool isOrganizerBooking = false);
        string SaveTicketLocally(byte[] pdfTicket, string eventId, string eventName, string firstName, string paymentGuid, string seatNumber);
        List<string> ListStoredTickets();
        bool DeleteStoredTicket(string fileName);
        
        /// <summary>
        /// Validates a QR code and returns comprehensive ticket information
        /// </summary>
        Task<QRValidationResponse> ValidateQRCodeAsync(QRValidationRequest request);
        
        /// <summary>
        /// Parses QR data string into components
        /// </summary>
        QRDataComponents ParseQRData(string qrData);
        
        /// <summary>
        /// Records an entry attempt for audit trail
        /// </summary>
        Task LogQREntryAsync(QRValidationRequest request, QRValidationResponse response, string? ipAddress = null, string? userAgent = null);
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
        public string? EventImageUrl { get; set; } // ✅ Event flyer/image URL for professional appearance
        public string? TicketType { get; set; } // ✅ Ticket type (VIP, Premium, Standard, etc.)
        public string? BookingReference { get; set; } // ✅ Booking reference number
    }

    public class FoodOrderInfo
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public string? Description { get; set; }
        public string? SeatAssignment { get; set; } // 🎯 CRITICAL: Track which seat this food item is assigned to
    }

    public class QRTicketResult
    {
        public bool Success { get; set; }
        public string? TicketPath { get; set; }
        public string? BookingId { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsDuplicate { get; set; }
        public byte[]? QRCodeImage { get; set; } 
        public string? EventImageUrl { get; set; }
    }

    // QR Validation Models
    public class QRValidationRequest
    {
        public string QRData { get; set; } = string.Empty;
        public string? ScanLocation { get; set; }
        public string? ScanNotes { get; set; }
    }

    public class QRValidationResponse
    {
        public bool Success { get; set; }
        public bool IsValid { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public QRDataComponents? QRData { get; set; }
        public TicketValidationInfo? Ticket { get; set; }
        public EventValidationInfo? Event { get; set; }
        public EntryValidationInfo? Entry { get; set; }
        public DateTime ValidatedAt { get; set; }
    }

    public class QRDataComponents
    {
        public string EventID { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        public string SeatNumber { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string PaymentGUID { get; set; } = string.Empty;
        public string RawData { get; set; } = string.Empty;
        public bool IsParsed { get; set; }
    }

    public class TicketValidationInfo
    {
        public int? BookingId { get; set; }
        public int? LineItemId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string SeatNumber { get; set; } = string.Empty;
        public string TicketType { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public DateTime BookingDate { get; set; }
        public string QRCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<FoodOrderInfo> FoodOrders { get; set; } = new();
    }

    public class EventValidationInfo
    {
        public int EventId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime? Date { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string OrganizerName { get; set; } = string.Empty;
        public string OrganizerEmail { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
    }

    public class EntryValidationInfo
    {
        public bool HasPreviousEntry { get; set; }
        public DateTime? FirstEntryTime { get; set; }
        public DateTime? LastEntryTime { get; set; }
        public int EntryCount { get; set; }
        public string? LastScanLocation { get; set; }
        public bool AllowReEntry { get; set; } = true;
    }
}
