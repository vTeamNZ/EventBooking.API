using EventBooking.API.Data;
using EventBooking.API.Models;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.Net.Http;
using System.Text.Json;

namespace EventBooking.API.Services
{
    public class QRTicketService : IQRTicketService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<QRTicketService> _logger;
        private readonly string _ticketStoragePath;
        private static readonly HttpClient _httpClient = new HttpClient();

        public QRTicketService(
            AppDbContext context,
            IConfiguration configuration,
            ILogger<QRTicketService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _ticketStoragePath = _configuration["TicketStorage:LocalPath"] ?? 
                                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TicketStorage");
            
            _logger.LogInformation("QRTicketService initialized with storage path: {StoragePath}", _ticketStoragePath);
            
            // Ensure the storage directory exists
            if (!Directory.Exists(_ticketStoragePath))
            {
                _logger.LogInformation("Creating ticket storage directory: {StoragePath}", _ticketStoragePath);
                Directory.CreateDirectory(_ticketStoragePath);
            }
        }

        public async Task<QRTicketResult> GenerateQRTicketAsync(QRTicketRequest request)
        {
            _logger.LogInformation("🎯 NEW ARCHITECTURE - Starting QR ticket generation for Event: {EventName} (ID: {EventID}), Attendee: {FirstName}, Seat: {SeatNo}, BookingId: {BookingId}",
                request.EventName, request.EventId, request.FirstName, request.SeatNumber, request.BookingId);

            try
            {
                // 🎯 NEW ARCHITECTURE - Check for existing ticket in BookingLineItems table
                var existingTicketLineItem = await _context.BookingLineItems
                    .Include(bli => bli.Booking)
                    .FirstOrDefaultAsync(bli => 
                        bli.BookingId == request.BookingId && 
                        bli.ItemType == "Ticket" && 
                        !string.IsNullOrEmpty(bli.QRCode));

                if (existingTicketLineItem != null)
                {
                    _logger.LogWarning("🎯 NEW ARCHITECTURE - Duplicate QR ticket generation attempt detected for BookingId: {BookingId}, Seat: {SeatNo}. Returning existing QR code: {QRCode}",
                        request.BookingId, request.SeatNumber, existingTicketLineItem.QRCode);
                    
                    // Try to find the existing ticket file path
                    var existingTicketPath = FindExistingTicketPath(request.EventId, request.EventName, request.FirstName, request.PaymentGuid);
                    
                    return new QRTicketResult
                    { 
                        Success = true,
                        TicketPath = existingTicketPath ?? "",
                        BookingId = existingTicketLineItem.QRCode,
                        IsDuplicate = true
                    };
                }

                // Generate QR Code
                _logger.LogInformation("Generating QR Code for ticket");
                byte[] qrCodeImage = GenerateQrCode(
                    request.EventId,
                    request.EventName,
                    request.SeatNumber,
                    request.FirstName,
                    request.PaymentGuid);
                _logger.LogInformation("QR Code generated successfully");

                // Generate PDF ticket
                _logger.LogInformation("Generating PDF ticket with food orders");
                byte[] pdfTicket = await GenerateTicketPdfAsync(
                    request.EventId,
                    request.EventName,
                    request.SeatNumber,
                    request.FirstName,
                    qrCodeImage,
                    request.FoodOrders); // ✅ Pass food orders to PDF generation
                _logger.LogInformation("PDF ticket generated successfully");

                // Save ticket locally
                _logger.LogInformation("Saving ticket to local storage");
                string localTicketPath = SaveTicketLocally(
                    pdfTicket,
                    request.EventId,
                    request.EventName,
                    request.FirstName,
                    request.PaymentGuid);
                _logger.LogInformation("Ticket saved locally at: {LocalPath}", localTicketPath);
                
                // 🎯 NEW ARCHITECTURE - Update BookingLineItem with QR code information
                _logger.LogInformation("🎯 NEW ARCHITECTURE - Updating BookingLineItem with QR code for BookingId: {BookingId}", request.BookingId);
                
                // Find the appropriate BookingLineItem for this ticket
                var ticketLineItem = await _context.BookingLineItems
                    .FirstOrDefaultAsync(bli => 
                        bli.BookingId == request.BookingId && 
                        bli.ItemType == "Ticket" && 
                        (string.IsNullOrEmpty(bli.QRCode) || bli.QRCode == ""));

                if (ticketLineItem != null)
                {
                    // Generate a unique QR identifier
                    var qrIdentifier = $"QR_{request.BookingId}_{ticketLineItem.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}";
                    
                    ticketLineItem.QRCode = qrIdentifier;
                    ticketLineItem.Status = "Active";
                    
                    // Update the ItemDetails with ticket path and QR info
                    var existingDetails = string.IsNullOrEmpty(ticketLineItem.ItemDetails) ? "{}" : ticketLineItem.ItemDetails;
                    var detailsDict = JsonSerializer.Deserialize<Dictionary<string, object>>(existingDetails) ?? new Dictionary<string, object>();
                    
                    detailsDict["ticketPath"] = localTicketPath;
                    detailsDict["qrGenerated"] = DateTime.UtcNow;
                    detailsDict["seatNumber"] = request.SeatNumber;
                    detailsDict["attendeeName"] = request.FirstName;
                    
                    ticketLineItem.ItemDetails = JsonSerializer.Serialize(detailsDict);

                    try
                    {
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("🎯 NEW ARCHITECTURE - Successfully updated BookingLineItem {LineItemId} with QR code: {QRCode}", 
                            ticketLineItem.Id, qrIdentifier);
                        
                        return new QRTicketResult
                        { 
                            Success = true,
                            TicketPath = localTicketPath,
                            BookingId = qrIdentifier,
                            IsDuplicate = false
                        };
                    }
                    catch (DbUpdateException ex)
                    {
                        _logger.LogError(ex, "🎯 NEW ARCHITECTURE - Error updating BookingLineItem with QR code");
                        // Fall back to using PaymentGuid as identifier
                        return new QRTicketResult
                        { 
                            Success = true,
                            TicketPath = localTicketPath,
                            BookingId = request.PaymentGuid,
                            IsDuplicate = false
                        };
                    }
                }
                else
                {
                    _logger.LogWarning("🎯 NEW ARCHITECTURE - Could not find BookingLineItem for BookingId: {BookingId}. Using PaymentGuid as fallback.", request.BookingId);
                    
                    // Fallback - still return success but use PaymentGuid
                    return new QRTicketResult
                    { 
                        Success = true,
                        TicketPath = localTicketPath,
                        BookingId = request.PaymentGuid,
                        IsDuplicate = false
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🎯 NEW ARCHITECTURE - Error generating QR ticket for Event: {EventName}, Attendee: {FirstName}",
                    request.EventName, request.FirstName);
                return new QRTicketResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 🎯 NEW ARCHITECTURE - Helper method to find existing ticket path for duplicate detection
        /// </summary>
        private string? FindExistingTicketPath(string eventId, string eventName, string firstName, string paymentGuid)
        {
            try
            {
                // Try to find an existing ticket file based on naming convention
                var expectedFileName = $"Ticket_{eventId}_{eventName}_{firstName}_{paymentGuid}.pdf"
                    .Replace(" ", "_")
                    .Replace(":", "")
                    .Replace("/", "_")
                    .Replace("\\", "_");
                
                var expectedPath = Path.Combine(_ticketStoragePath, expectedFileName);
                
                if (File.Exists(expectedPath))
                {
                    _logger.LogInformation("🎯 NEW ARCHITECTURE - Found existing ticket file: {TicketPath}", expectedPath);
                    return expectedPath;
                }
                
                _logger.LogInformation("🎯 NEW ARCHITECTURE - No existing ticket file found for: {ExpectedPath}", expectedPath);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🎯 NEW ARCHITECTURE - Error searching for existing ticket file");
                return null;
            }
        }

        public byte[] GenerateQrCode(string eventId, string eventName, string seatNumber, string firstName, string paymentGuid)
        {
            _logger.LogInformation("Generating QR code for Event: {EventName} (ID: {EventID}), Seat: {SeatNo}, Attendee: {FirstName}",
                eventName, eventId, seatNumber, firstName);
            
            // Concatenate the data
            string qrData = $"EventID: {eventId}, Event: {eventName}, Seat: {seatNumber}, Name: {firstName}, ID: {paymentGuid}";
            _logger.LogDebug("QR code data: {QRData}", qrData);
            
            // Generate QR code
            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
            PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeImage = qrCode.GetGraphic(20);
            
            _logger.LogInformation("QR code generated successfully");
            return qrCodeImage;
        }

        public async Task<byte[]> GenerateTicketPdfAsync(string eventId, string eventName, string seatNumber, string firstName, byte[] qrCodeImage, List<FoodOrderInfo>? foodOrders = null)
        {
            _logger.LogInformation("Generating PDF ticket for Event: {EventName}, Seat: {SeatNumber}, Attendee: {FirstName}, FoodItems: {FoodCount}",
                eventName, seatNumber, firstName, foodOrders?.Count ?? 0);

            using (var stream = new MemoryStream())
            {
                // Create document
                Document document = new Document(PageSize.A4, 50, 50, 50, 50);
                PdfWriter writer = PdfWriter.GetInstance(document, stream);

                document.Open();

                // Add title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 24, BaseColor.DARK_GRAY);
                var title = new Paragraph("🎫 EVENT TICKET", titleFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingAfter = 20
                };
                document.Add(title);

                // Add event details
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.BLACK);
                var bodyFont = FontFactory.GetFont(FontFactory.HELVETICA, 12, BaseColor.BLACK);
                var foodHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.DARK_GRAY);
                var foodItemFont = FontFactory.GetFont(FontFactory.HELVETICA, 11, BaseColor.BLACK);

                document.Add(new Paragraph($"Event: {eventName}", headerFont) { SpacingAfter = 10 });
                document.Add(new Paragraph($"Attendee: {firstName}", bodyFont) { SpacingAfter = 5 });
                document.Add(new Paragraph($"Seat: {seatNumber}", bodyFont) { SpacingAfter = 5 });
                document.Add(new Paragraph($"Event ID: {eventId}", bodyFont) { SpacingAfter = 15 });

                // ✅ ADD FOOD ORDERS SECTION
                if (foodOrders != null && foodOrders.Any())
                {
                    document.Add(new Paragraph("🍕 Your Food Orders:", foodHeaderFont) { SpacingBefore = 10, SpacingAfter = 8 });
                    
                    decimal totalFoodCost = 0;
                    foreach (var food in foodOrders)
                    {
                        var foodLine = $"• {food.Quantity}x {food.Name} - ${food.UnitPrice:F2} each";
                        if (food.Quantity > 1)
                        {
                            foodLine += $" (Total: ${food.TotalPrice:F2})";
                        }
                        
                        document.Add(new Paragraph(foodLine, foodItemFont) { SpacingAfter = 3, IndentationLeft = 10 });
                        totalFoodCost += food.TotalPrice;
                    }
                    
                    if (totalFoodCost > 0)
                    {
                        var totalFoodLine = new Paragraph($"Food Total: ${totalFoodCost:F2}", bodyFont)
                        {
                            SpacingAfter = 15,
                            SpacingBefore = 5,
                            IndentationLeft = 10
                        };
                        totalFoodLine.Font.SetStyle(Font.BOLD);
                        document.Add(totalFoodLine);
                    }
                }

                // Add QR code
                if (qrCodeImage != null && qrCodeImage.Length > 0)
                {
                    try
                    {
                        var qrImage = Image.GetInstance(qrCodeImage);
                        qrImage.ScaleToFit(200f, 200f);
                        qrImage.Alignment = Element.ALIGN_CENTER;
                        qrImage.SpacingBefore = 10;
                        document.Add(qrImage);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to add QR code image to PDF");
                        document.Add(new Paragraph("QR Code generation failed", bodyFont));
                    }
                }

                // Add footer with food pickup instructions if food orders exist
                var footerText = "\\nPresent this ticket at the venue entrance.";
                if (foodOrders != null && foodOrders.Any())
                {
                    footerText += "\\nFood orders will be available for pickup at the concession stand.";
                }
                
                document.Add(new Paragraph(footerText, bodyFont)
                {
                    Alignment = Element.ALIGN_CENTER,
                    SpacingBefore = 20
                });

                document.Close();
                _logger.LogInformation("PDF ticket generated successfully with {FoodCount} food items", foodOrders?.Count ?? 0);
                return stream.ToArray();
            }
        }

        public string SaveTicketLocally(byte[] pdfTicket, string eventId, string eventName, string firstName, string paymentGuid)
        {
            try
            {
                // Create filename with timestamp
                string sanitizedEventName = string.Join("_", eventName.Split(Path.GetInvalidFileNameChars()));
                string sanitizedFirstName = string.Join("_", firstName.Split(Path.GetInvalidFileNameChars()));
                string fileName = $"eTicket_{sanitizedEventName}_{sanitizedFirstName}_{paymentGuid}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string filePath = Path.Combine(_ticketStoragePath, fileName);

                // Save the file
                File.WriteAllBytes(filePath, pdfTicket);
                
                _logger.LogInformation("Ticket saved successfully at: {FilePath}", filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving ticket locally");
                throw;
            }
        }

        public List<string> ListStoredTickets()
        {
            try
            {
                if (!Directory.Exists(_ticketStoragePath))
                {
                    return new List<string>();
                }

                return Directory.GetFiles(_ticketStoragePath, "*.pdf")
                    .Select(Path.GetFileName)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing stored tickets");
                return new List<string>();
            }
        }

        public bool DeleteStoredTicket(string fileName)
        {
            try
            {
                string filePath = Path.Combine(_ticketStoragePath, fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Ticket deleted successfully: {FileName}", fileName);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting ticket: {FileName}", fileName);
                return false;
            }
        }
    }
}
