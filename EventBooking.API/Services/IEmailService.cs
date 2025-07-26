namespace EventBooking.API.Services
{
    public interface IEmailService
    {
        Task<bool> SendOrganizerNotificationAsync(string organizerEmail, string eventName, string firstName, string buyerEmail, byte[] ticketPdf, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null);
        Task<bool> SendEmailWithAttachmentAsync(string toEmail, string subject, string htmlBody, byte[] attachment, string attachmentFileName);
        Task<bool> SendEnhancedTicketEmailAsync(string toEmail, string eventName, string firstName, byte[] ticketPdf, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null, byte[]? qrCodeImage = null, string? bookingId = null);
        
        // 🎯 NEW: Consolidated booking email methods
        Task<bool> SendConsolidatedBookingEmailAsync(string toEmail, string eventName, string firstName, List<(byte[] PdfData, string FileName)> ticketAttachments, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null, string? bookingReference = null);
        Task<bool> SendConsolidatedOrganizerNotificationAsync(string organizerEmail, string eventName, string firstName, string buyerEmail, List<(byte[] PdfData, string FileName)> ticketAttachments, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null, string? bookingReference = null);
    }
}
