using EventBooking.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventBooking.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketsController : ControllerBase
    {
        private readonly IQRTicketService _qrTicketService;
        private readonly IEmailService _emailService;
        private readonly ILogger<TicketsController> _logger;

        public TicketsController(IQRTicketService qrTicketService, IEmailService emailService, ILogger<TicketsController> logger)
        {
            _qrTicketService = qrTicketService;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpPost("qr-ticket")]
        public async Task<IActionResult> GenerateQRTicket([FromBody] QRTicketRequest request)
        {
            try
            {
                _logger.LogInformation("🎵 API REQUEST - Generating professional concert ticket for {EventName}", request.EventName);
                
                var result = await _qrTicketService.GenerateQRTicketAsync(request);
                
                if (result.Success)
                {
                    _logger.LogInformation("✅ API SUCCESS - Professional concert ticket generated: {TicketPath}", result.TicketPath);
                    return Ok(result);
                }
                else
                {
                    _logger.LogError("❌ API ERROR - Ticket generation failed: {Error}", result.ErrorMessage);
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ API EXCEPTION - Error in QR ticket generation");
                return StatusCode(500, new { success = false, errorMessage = ex.Message });
            }
        }

        [HttpGet("test")]
        public IActionResult TestEndpoint()
        {
            return Ok(new { message = "🎵 Professional Concert Ticket API is working!", timestamp = DateTime.UtcNow });
        }

        /// <summary>
        /// 🎯 Test endpoint for enhanced email with QR code in body
        /// </summary>
        [HttpPost("enhanced-email-ticket")]
        public async Task<IActionResult> GenerateEnhancedEmailTicket([FromBody] QRTicketRequest request)
        {
            try
            {
                _logger.LogInformation("🎯 ENHANCED EMAIL - Generating ticket with QR in email body for {EventName}", request.EventName);
                
                var result = await _qrTicketService.GenerateQRTicketAsync(request);
                
                if (result.Success && !string.IsNullOrEmpty(result.TicketPath))
                {
                    // Read the generated PDF
                    byte[] ticketPdf = System.IO.File.ReadAllBytes(result.TicketPath);
                    
                    // Send enhanced email with QR code in body
                    bool emailSuccess = await _emailService.SendEnhancedTicketEmailAsync(
                        request.BuyerEmail,
                        request.EventName,
                        request.FirstName,
                        ticketPdf,
                        request.FoodOrders,
                        result.EventImageUrl,
                        result.QRCodeImage,
                        result.BookingId
                    );
                    
                    if (emailSuccess)
                    {
                        _logger.LogInformation("🎯 ENHANCED EMAIL SUCCESS - Email sent with QR code in body to {Email}", request.BuyerEmail);
                        return Ok(new 
                        { 
                            success = true, 
                            message = "Enhanced email sent successfully with QR code in body!",
                            ticketPath = result.TicketPath,
                            bookingId = result.BookingId,
                            emailSent = true,
                            features = new[] { "QR Code in Email Body", "Event Image Embedded", "Professional Design", "PDF Backup Attachment" }
                        });
                    }
                    else
                    {
                        _logger.LogWarning("🎯 ENHANCED EMAIL WARNING - Ticket generated but email failed for {Email}", request.BuyerEmail);
                        return Ok(new 
                        { 
                            success = true, 
                            message = "Ticket generated successfully but email failed",
                            ticketPath = result.TicketPath,
                            bookingId = result.BookingId,
                            emailSent = false
                        });
                    }
                }
                else
                {
                    _logger.LogError("🎯 ENHANCED EMAIL ERROR - Ticket generation failed: {Error}", result.ErrorMessage);
                    return BadRequest(new { success = false, errorMessage = result.ErrorMessage });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🎯 ENHANCED EMAIL EXCEPTION - Error in enhanced email ticket generation");
                return StatusCode(500, new { success = false, errorMessage = ex.Message });
            }
        }
    }
}
