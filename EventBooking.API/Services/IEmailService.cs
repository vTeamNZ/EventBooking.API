namespace EventBooking.API.Services
{
    public interface IEmailService
    {
        Task<bool> SendTicketEmailAsync(string toEmail, string eventName, string firstName, byte[] ticketPdf, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null, byte[]? qrCodeImage = null, string? bookingId = null);
        Task<bool> SendOrganizerNotificationAsync(string organizerEmail, string eventName, string firstName, string buyerEmail, byte[] ticketPdf, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null);
        Task<bool> SendEmailWithAttachmentAsync(string toEmail, string subject, string htmlBody, byte[] attachment, string attachmentFileName);
        Task<bool> SendEnhancedTicketEmailAsync(string toEmail, string eventName, string firstName, byte[] ticketPdf, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null, byte[]? qrCodeImage = null, string? bookingId = null);
    }
}
