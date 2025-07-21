using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace EventBooking.API.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _smtpServer = _configuration["Email:SmtpServer"] ?? throw new ArgumentException("Email:SmtpServer not configured");
            _smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            _smtpUsername = _configuration["Email:Username"] ?? throw new ArgumentException("Email:Username not configured");
            _smtpPassword = _configuration["Email:Password"] ?? throw new ArgumentException("Email:Password not configured");
            _fromEmail = _configuration["Email:FromEmail"] ?? "support@kiwilanka.co.nz";
            _fromName = _configuration["Email:FromName"] ?? "KiwiLanka Events";
        }

        public async Task<bool> SendTicketEmailAsync(string toEmail, string eventName, string firstName, byte[] ticketPdf, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null)
        {
            var subject = $"Your eTicket for {eventName}";
            var htmlBody = GenerateBuyerEmailHtml(eventName, firstName, foodOrders, eventImageUrl);
            var attachmentFileName = $"eTicket_{eventName}_{firstName}.pdf";

            return await SendEmailWithAttachmentAsync(toEmail, subject, htmlBody, ticketPdf, attachmentFileName);
        }

        public async Task<bool> SendOrganizerNotificationAsync(string organizerEmail, string eventName, string firstName, string buyerEmail, byte[] ticketPdf, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null)
        {
            var subject = $"New Booking for {eventName}";
            var htmlBody = GenerateOrganizerEmailHtml(eventName, firstName, buyerEmail, foodOrders, eventImageUrl);
            var attachmentFileName = $"eTicket_{eventName}_{firstName}.pdf";

            return await SendEmailWithAttachmentAsync(organizerEmail, subject, htmlBody, ticketPdf, attachmentFileName);
        }

        public async Task<bool> SendEmailWithAttachmentAsync(string toEmail, string subject, string htmlBody, byte[] attachment, string attachmentFileName)
        {
            try
            {
                _logger.LogInformation("Sending email to {ToEmail} with subject: {Subject}", toEmail, subject);

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_fromName, _fromEmail));
                message.To.Add(new MailboxAddress("", toEmail));
                message.Subject = subject;

                // Create body part
                var bodyBuilder = new BodyBuilder();
                bodyBuilder.HtmlBody = htmlBody;

                // Add attachment
                if (attachment != null && attachment.Length > 0)
                {
                    bodyBuilder.Attachments.Add(attachmentFileName, attachment);
                    _logger.LogDebug("Added attachment: {FileName}, Size: {Size} bytes", attachmentFileName, attachment.Length);
                }

                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    // Connect to the SMTP server
                    await client.ConnectAsync(_smtpServer, _smtpPort, SecureSocketOptions.StartTls);
                    
                    // Authenticate
                    await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
                    
                    // Send the email
                    await client.SendAsync(message);
                    
                    // Disconnect
                    await client.DisconnectAsync(true);
                }

                _logger.LogInformation("Email sent successfully to {ToEmail}", toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {ToEmail}", toEmail);
                return false;
            }
        }

        private string GenerateBuyerEmailHtml(string eventName, string firstName, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null)
        {
            // Generate food orders HTML section
            var foodOrdersHtml = "";
            if (foodOrders != null && foodOrders.Any())
            {
                var foodItemsHtml = string.Join("", foodOrders.Select(food => 
                    $"<li>{food.Quantity}x {food.Name} - ${food.UnitPrice:F2} each" + 
                    (food.Quantity > 1 ? $" (Total: ${food.TotalPrice:F2})" : "") + "</li>"));
                
                var totalFoodCost = foodOrders.Sum(f => f.TotalPrice);
                
                foodOrdersHtml = $@"
            <div class='ticket-info' style='border-left-color: #ff6b35;'>
                <h3>🍕 Your Food Orders</h3>
                <ul style='margin: 0; padding-left: 20px;'>
                    {foodItemsHtml}
                </ul>
                <p style='margin-top: 10px; font-weight: bold;'>Food Total: ${totalFoodCost:F2}</p>
                <p style='font-size: 11px; color: #666; margin-top: 8px;'>
                    <em>Food orders will be available for pickup at the concession stand during the event.</em>
                </p>
            </div>";
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Your Event Ticket</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 20px; }}
        .footer {{ background: #333; color: white; padding: 15px; text-align: center; border-radius: 0 0 8px 8px; }}
        .ticket-info {{ background: white; padding: 15px; margin: 10px 0; border-left: 4px solid #667eea; }}
        .button {{ display: inline-block; background: #667eea; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px; margin: 10px 0; }}
        .event-image {{ max-width: 100%; height: auto; border-radius: 8px; margin: 15px 0; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎫 Your Event Ticket</h1>
            <p>KiwiLanka Events</p>
        </div>
        <div class='content'>
            <h2>Hi {firstName}!</h2>
            <p>Thank you for your booking! Your ticket for <strong>{eventName}</strong> is attached to this email.</p>
            
            {(!string.IsNullOrEmpty(eventImageUrl) ? $"<img src='{eventImageUrl}' alt='{eventName}' class='event-image' />" : "")}
            
            <div class='ticket-info'>
                <h3>📅 Event Details</h3>
                <p><strong>Event:</strong> {eventName}</p>
                <p><strong>Attendee:</strong> {firstName}</p>
                <p><strong>Status:</strong> ✅ Confirmed</p>
            </div>
            
            {foodOrdersHtml}
            
            <h3>📱 Important Instructions:</h3>
            <ul>
                <li>Present your attached ticket (PDF) at the venue entrance</li>
                <li>The QR code on your ticket will be scanned for entry</li>
                <li>Please arrive 30 minutes before the event starts</li>
                <li>Bring a valid photo ID</li>
                {(foodOrders != null && foodOrders.Any() ? "<li><strong>Food pickup:</strong> Visit the concession stand with your ticket to collect your food orders</li>" : "")}
            </ul>
            
            <p>We look forward to seeing you at the event!</p>
        </div>
        <div class='footer'>
            <p>© 2025 KiwiLanka Events | support@kiwilanka.co.nz</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GenerateOrganizerEmailHtml(string eventName, string firstName, string buyerEmail, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null)
        {
            // Generate food orders HTML section for organizer
            var foodOrdersHtml = "";
            if (foodOrders != null && foodOrders.Any())
            {
                var foodItemsHtml = string.Join("", foodOrders.Select(food => 
                    $"<li>{food.Quantity}x {food.Name} - ${food.UnitPrice:F2} each" + 
                    (food.Quantity > 1 ? $" (Total: ${food.TotalPrice:F2})" : "") + "</li>"));
                
                var totalFoodCost = foodOrders.Sum(f => f.TotalPrice);
                
                foodOrdersHtml = $@"
            <div class='booking-info' style='border-left-color: #ff6b35;'>
                <h3>🍕 Food Orders from {firstName}</h3>
                <ul style='margin: 0; padding-left: 20px;'>
                    {foodItemsHtml}
                </ul>
                <p style='margin-top: 10px; font-weight: bold;'>Food Revenue: ${totalFoodCost:F2}</p>
                <p style='font-size: 11px; color: #666; margin-top: 8px;'>
                    <em>Ensure these items are available at the concession stand for pickup.</em>
                </p>
            </div>";
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>New Booking Notification</title>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #28a745 0%, #20c997 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 20px; }}
        .footer {{ background: #333; color: white; padding: 15px; text-align: center; border-radius: 0 0 8px 8px; }}
        .booking-info {{ background: white; padding: 15px; margin: 10px 0; border-left: 4px solid #28a745; }}
        .event-image {{ max-width: 100%; height: auto; border-radius: 8px; margin: 15px 0; box-shadow: 0 2px 8px rgba(0,0,0,0.1); }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎉 New Booking Received!</h1>
            <p>KiwiLanka Events - Organizer Portal</p>
        </div>
        <div class='content'>
            <h2>Booking Confirmation</h2>
            <p>You have received a new booking for your event.</p>
            
            {(!string.IsNullOrEmpty(eventImageUrl) ? $"<img src='{eventImageUrl}' alt='{eventName}' class='event-image' />" : "")}
            
            <div class='booking-info'>
                <h3>📊 Booking Details</h3>
                <p><strong>Event:</strong> {eventName}</p>
                <p><strong>Attendee:</strong> {firstName}</p>
                <p><strong>Booking Time:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</p>
                <p><strong>Status:</strong> ✅ Confirmed & Paid</p>
            </div>
            
            {foodOrdersHtml}
            
            <h3>📋 Next Steps:</h3>
            <ul>
                <li>The attendee has been sent their ticket automatically</li>
                <li>A copy of the ticket is attached to this email</li>
                <li>Update your event attendance records</li>
                <li>Prepare for the additional attendee at your venue</li>
                {(foodOrders != null && foodOrders.Any() ? "<li><strong>Food preparation:</strong> Ensure ordered food items are available at the concession stand</li>" : "")}
            </ul>
            
            <p>Thank you for using KiwiLanka Events!</p>
        </div>
        <div class='footer'>
            <p>© 2025 KiwiLanka Events | support@kiwilanka.co.nz</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}
