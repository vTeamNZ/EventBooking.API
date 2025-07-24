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

        public async Task<bool> SendTicketEmailAsync(string toEmail, string eventName, string firstName, byte[] ticketPdf, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null, byte[]? qrCodeImage = null, string? bookingId = null)
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

        /// <summary>
        /// 🎯 Enhanced email with QR code in body and event image (No wallet integration)
        /// </summary>
        public async Task<bool> SendEnhancedTicketEmailAsync(string toEmail, string eventName, string firstName, byte[] ticketPdf, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null, byte[]? qrCodeImage = null, string? bookingId = null)
        {
            try
            {
                _logger.LogInformation("🎯 Sending enhanced email with QR code to {ToEmail} for event: {EventName}", toEmail, eventName);

                var subject = $"🎫 Your eTicket for {eventName} - QR Code Included";
                
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_fromName, _fromEmail));
                message.To.Add(new MailboxAddress("", toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder();
                
                // Generate enhanced HTML with embedded QR code and event image
                var htmlBody = GenerateEnhancedEmailHtml(eventName, firstName, foodOrders, eventImageUrl, bookingId);
                bodyBuilder.HtmlBody = htmlBody;

                // Embed QR code image if provided
                if (qrCodeImage != null && qrCodeImage.Length > 0)
                {
                    var qrAttachment = bodyBuilder.LinkedResources.Add("qrcode.png", qrCodeImage);
                    qrAttachment.ContentId = "qrcode-embedded";
                    _logger.LogDebug("🎯 Added QR code as embedded image: {Size} bytes", qrCodeImage.Length);
                }

                // Embed event image if provided
                if (!string.IsNullOrEmpty(eventImageUrl) && eventImageUrl.StartsWith("http"))
                {
                    try
                    {
                        using var httpClient = new HttpClient();
                        var eventImageBytes = await httpClient.GetByteArrayAsync(eventImageUrl);
                        var eventImageAttachment = bodyBuilder.LinkedResources.Add("event-image.jpg", eventImageBytes);
                        eventImageAttachment.ContentId = "event-image-embedded";
                        _logger.LogDebug("🎯 Added event image as embedded image: {Size} bytes", eventImageBytes.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "🎯 Failed to embed event image from URL: {EventImageUrl}", eventImageUrl);
                    }
                }

                // Add PDF ticket as attachment
                if (ticketPdf != null && ticketPdf.Length > 0)
                {
                    var attachmentFileName = $"eTicket_{eventName}_{firstName}.pdf";
                    bodyBuilder.Attachments.Add(attachmentFileName, ticketPdf);
                    _logger.LogDebug("🎯 Added PDF ticket attachment: {FileName}, Size: {Size} bytes", attachmentFileName, ticketPdf.Length);
                }

                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    await client.ConnectAsync(_smtpServer, _smtpPort, SecureSocketOptions.StartTls);
                    await client.AuthenticateAsync(_smtpUsername, _smtpPassword);
                    await client.SendAsync(message);
                    await client.DisconnectAsync(true);
                }

                _logger.LogInformation("🎯 Enhanced email sent successfully to {ToEmail}", toEmail);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🎯 Failed to send enhanced email to {ToEmail}", toEmail);
                return false;
            }
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

        /// <summary>
        /// 🎯 Enhanced email template with embedded QR code and event image (No wallet buttons)
        /// </summary>
        private string GenerateEnhancedEmailHtml(string eventName, string firstName, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null, string? bookingId = null)
        {
            // Generate food orders HTML section
            var foodOrdersHtml = "";
            if (foodOrders != null && foodOrders.Any())
            {
                var foodItemsHtml = string.Join("", foodOrders.Select(food => 
                    $"<li style='margin: 5px 0;'>{food.Quantity}x {food.Name} - ${food.UnitPrice:F2} each" + 
                    (food.Quantity > 1 ? $" (Total: ${food.TotalPrice:F2})" : "") + "</li>"));
                
                var totalFoodCost = foodOrders.Sum(f => f.TotalPrice);
                
                foodOrdersHtml = $@"
            <div class='food-section' style='background: linear-gradient(135deg, #ff6b6b 0%, #ee5a24 100%); color: white; padding: 20px; margin: 20px 0; border-radius: 12px; box-shadow: 0 4px 15px rgba(255,107,107,0.3);'>
                <h3 style='margin: 0 0 15px 0; font-size: 18px;'>🍕 Your Food Orders</h3>
                <ul style='margin: 0; padding-left: 20px; list-style-type: none;'>
                    {foodItemsHtml}
                </ul>
                <div style='margin-top: 15px; padding-top: 15px; border-top: 1px solid rgba(255,255,255,0.3); font-weight: bold; font-size: 16px;'>
                    Food Total: ${totalFoodCost:F2}
                </div>
                <p style='font-size: 12px; margin-top: 12px; opacity: 0.9;'>
                    <em>💡 Food orders will be available for pickup at the concession stand during the event.</em>
                </p>
            </div>";
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Your Event Ticket - {eventName}</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 0; background: #f5f7fa; }}
        .container {{ max-width: 650px; margin: 0 auto; background: white; box-shadow: 0 8px 32px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #4ecdc4 0%, #44a08d 100%); color: white; padding: 30px 20px; text-align: center; position: relative; overflow: hidden; }}
        .header::before {{ content: ''; position: absolute; top: -50%; left: -50%; width: 200%; height: 200%; background: url('data:image/svg+xml,<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100""><circle cx=""50"" cy=""50"" r=""2"" fill=""rgba(255,255,255,0.1)""/></svg>') repeat; animation: float 20s infinite linear; }}
        .header h1 {{ margin: 0; font-size: 28px; font-weight: 300; position: relative; z-index: 1; }}
        .header p {{ margin: 5px 0 0 0; font-size: 14px; opacity: 0.9; position: relative; z-index: 1; }}
        .content {{ padding: 30px; }}
        .event-banner {{ text-align: center; margin: 0 0 30px 0; }}
        .event-image {{ max-width: 100%; height: 300px; object-fit: cover; border-radius: 12px; box-shadow: 0 8px 25px rgba(0,0,0,0.15); }}
        .qr-section {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 25px; margin: 25px 0; border-radius: 15px; text-align: center; box-shadow: 0 6px 20px rgba(102,126,234,0.3); }}
        .qr-code {{ width: 200px; height: 200px; margin: 15px auto; background: white; padding: 15px; border-radius: 12px; box-shadow: 0 4px 15px rgba(0,0,0,0.2); }}
        .ticket-info {{ background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%); padding: 20px; margin: 20px 0; border-radius: 12px; border-left: 5px solid #4ecdc4; }}
        .instructions {{ background: linear-gradient(135deg, #fff3cd 0%, #ffeaa7 100%); padding: 20px; margin: 20px 0; border-radius: 12px; border-left: 5px solid #ffc107; }}
        .footer {{ background: #2c3e50; color: white; padding: 25px; text-align: center; }}
        @keyframes float {{ 0% {{ transform: translateX(-100px); }} 100% {{ transform: translateX(100px); }} }}
        @media (max-width: 600px) {{ .qr-code {{ width: 150px; height: 150px; }} }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>🎫 Your Event Ticket</h1>
            <p>KiwiLanka Events - Digital Ticket System</p>
        </div>
        
        <div class='content'>
            <h2 style='color: #4ecdc4; margin: 0 0 20px 0; font-size: 24px;'>Hi {firstName}! 🎉</h2>
            <p style='font-size: 16px; margin-bottom: 25px;'>Your ticket for <strong style='color: #667eea;'>{eventName}</strong> is ready! Use the QR code below for quick entry:</p>
            
            {(!string.IsNullOrEmpty(eventImageUrl) ? $@"
            <div class='event-banner'>
                <img src='cid:event-image-embedded' alt='{eventName}' class='event-image' />
            </div>" : "")}
            
            <div class='qr-section'>
                <h3 style='margin: 0 0 10px 0; font-size: 20px;'>📱 Quick Entry QR Code</h3>
                <p style='margin: 0 0 15px 0; opacity: 0.9;'>Scan this at the venue entrance</p>
                <div class='qr-code'>
                    <img src='cid:qrcode-embedded' alt='Entry QR Code' style='width: 100%; height: 100%; object-fit: contain;' />
                </div>
                <p style='margin: 15px 0 0 0; font-size: 12px; opacity: 0.8;'>Booking ID: {bookingId ?? "N/A"}</p>
            </div>
            
            <div class='ticket-info'>
                <h3 style='margin: 0 0 15px 0; color: #4ecdc4;'>📅 Event Details</h3>
                <p style='margin: 5px 0;'><strong>Event:</strong> {eventName}</p>
                <p style='margin: 5px 0;'><strong>Attendee:</strong> {firstName}</p>
                <p style='margin: 5px 0;'><strong>Status:</strong> <span style='color: #28a745; font-weight: bold;'>✅ Confirmed</span></p>
                <p style='margin: 5px 0;'><strong>Booking ID:</strong> {bookingId ?? "N/A"}</p>
            </div>
            
            {foodOrdersHtml}
            
            <div class='instructions'>
                <h3 style='margin: 0 0 15px 0; color: #856404;'>📋 Important Instructions</h3>
                <ul style='margin: 0; padding-left: 20px;'>
                    <li>🎫 <strong>Quick entry:</strong> Use the QR code above or show the attached PDF</li>
                    <li>⏰ <strong>Arrival:</strong> Please arrive 30 minutes before the event starts</li>
                    <li>🆔 <strong>ID Required:</strong> Bring a valid photo ID for verification</li>
                    <li>📱 <strong>Digital access:</strong> The QR code above works even without internet at the venue</li>
                    {(foodOrders != null && foodOrders.Any() ? "<li>🍕 <strong>Food pickup:</strong> Visit the concession stand with your ticket to collect your food orders</li>" : "")}
                </ul>
            </div>
            
            <p style='text-align: center; font-size: 16px; color: #667eea; margin: 30px 0;'>
                <strong>We can't wait to see you at the event! 🎉</strong>
            </p>
        </div>
        
        <div class='footer'>
            <p style='margin: 0; font-size: 14px;'>© 2025 KiwiLanka Events | support@kiwilanka.co.nz</p>
            <p style='margin: 5px 0 0 0; font-size: 12px; opacity: 0.8;'>Digital Ticketing Platform</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}
