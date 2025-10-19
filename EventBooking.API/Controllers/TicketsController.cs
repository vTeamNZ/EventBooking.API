using EventBooking.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventBooking.API.Controllers
{
    [Route("[controller]")]
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
                        result.BookingId,
                        request.SeatNumber, // seat or ticket number
                        request.TicketType, // ticket type
                        null, // event date (not available in controller)
                        null // event location (not available in controller)
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

        /// <summary>
        /// 🔍 Validates a QR code and returns comprehensive ticket information
        /// This endpoint is publicly accessible and does not require authentication
        /// </summary>
        [AllowAnonymous]
        [HttpPost("validate-qr")]
        public async Task<IActionResult> ValidateQRCode([FromBody] QRValidationRequest request)
        {
            try
            {
                _logger.LogInformation("🔍 QR VALIDATION REQUEST - Validating QR code from location: {Location}", 
                    request.ScanLocation ?? "Unknown");

                // Validate request
                if (string.IsNullOrWhiteSpace(request.QRData))
                {
                    return BadRequest(new { success = false, message = "QR data is required" });
                }

                // Get client information for audit trail
                string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
                string? userAgent = HttpContext.Request.Headers["User-Agent"].FirstOrDefault();

                // Perform validation
                var validationResult = await _qrTicketService.ValidateQRCodeAsync(request);

                // Log the entry attempt
                await _qrTicketService.LogQREntryAsync(request, validationResult, ipAddress, userAgent);

                // Return comprehensive response
                var response = new
                {
                    success = validationResult.IsValid,
                    isValid = validationResult.IsValid,
                    status = validationResult.Status,
                    message = validationResult.Message,
                    validatedAt = validationResult.ValidatedAt,
                    qrData = validationResult.QRData,
                    ticket = validationResult.Ticket,
                    eventInfo = validationResult.Event,
                    entryInfo = validationResult.Entry
                };

                if (validationResult.IsValid)
                {
                    _logger.LogInformation("✅ QR VALIDATION SUCCESS - {Status}: {CustomerName} for {EventTitle}", 
                        validationResult.Status, validationResult.Ticket?.CustomerName, validationResult.Event?.Title);
                }
                else
                {
                    _logger.LogWarning("❌ QR VALIDATION FAILED - {Status}: {Message}", 
                        validationResult.Status, validationResult.Message);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔍 QR VALIDATION EXCEPTION - Error validating QR code");
                return StatusCode(500, new 
                { 
                    success = false, 
                    isValid = false,
                    status = "Error",
                    message = "Validation failed due to system error",
                    validatedAt = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// 📊 Gets QR entry statistics for an event (Admin/Organizer only)
        /// </summary>
        [HttpGet("qr-stats/{eventId}")]
        public async Task<IActionResult> GetQRStats(int eventId)
        {
            try
            {
                // This could be extended to provide entry statistics
                // For now, just return a simple response
                return Ok(new { message = "QR stats endpoint - implementation pending", eventId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting QR stats for event {EventId}", eventId);
                return StatusCode(500, new { success = false, message = "Failed to get QR statistics" });
            }
        }
    }
}
