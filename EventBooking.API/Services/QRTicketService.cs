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

                // Generate PDF ticket - Using enhanced direct PDF generation
                _logger.LogInformation("🎵 Generating professional concert ticket PDF");
                byte[] pdfTicket = await GenerateProfessionalConcertTicketAsync(
                    request.EventId,
                    request.EventName,
                    request.SeatNumber,
                    request.FirstName,
                    qrCodeImage,
                    request.FoodOrders, // ✅ Pass food orders to PDF generation
                    request.EventImageUrl); // ✅ Pass event flyer URL
                _logger.LogInformation("🎵 Professional concert ticket PDF generated successfully");

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
                            IsDuplicate = false,
                            QRCodeImage = qrCodeImage, // 🎯 Include QR code bytes for enhanced email
                            EventImageUrl = request.EventImageUrl // 🎯 Include event image URL for enhanced email
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
                            IsDuplicate = false,
                            QRCodeImage = qrCodeImage, // 🎯 Include QR code bytes for enhanced email
                            EventImageUrl = request.EventImageUrl // 🎯 Include event image URL for enhanced email
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
                        IsDuplicate = false,
                        QRCodeImage = qrCodeImage, // 🎯 Include QR code bytes for enhanced email
                        EventImageUrl = request.EventImageUrl // 🎯 Include event image URL for enhanced email
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

        /// <summary>
        /// Helper method to get event details from database for enhanced ticket information
        /// </summary>
        private async Task<EventDetails?> GetEventDetailsAsync(string eventId)
        {
            try
            {
                if (!int.TryParse(eventId, out int id))
                {
                    _logger.LogWarning("Invalid event ID format: {EventId}", eventId);
                    return null;
                }

                var eventEntity = await _context.Events
                    .Where(e => e.Id == id)
                    .Select(e => new EventDetails
                    {
                        Date = e.Date,
                        Location = e.Location,
                        Description = e.Description,
                        ImageUrl = e.ImageUrl
                    })
                    .FirstOrDefaultAsync();

                return eventEntity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching event details for event ID: {EventId}", eventId);
                return null;
            }
        }

        /// <summary>
        /// Helper method to get organizer details from database for enhanced ticket information
        /// </summary>
        private async Task<OrganizerDetails?> GetOrganizerDetailsAsync(string eventId)
        {
            try
            {
                if (!int.TryParse(eventId, out int id))
                {
                    _logger.LogWarning("Invalid event ID format: {EventId}", eventId);
                    return null;
                }

                var organizerInfo = await _context.Events
                    .Where(e => e.Id == id)
                    .Select(e => new
                    {
                        Date = e.Date,
                        Location = e.Location,
                        Description = e.Description,
                        ImageUrl = e.ImageUrl,
                        OrganizerName = e.Organizer != null ? e.Organizer.Name : null,
                        OrganizerEmail = e.Organizer != null ? e.Organizer.ContactEmail : null,
                        OrganizerPhone = e.Organizer != null ? e.Organizer.PhoneNumber : null,
                        OrganizerWebsite = e.Organizer != null ? e.Organizer.Website : null,
                        OrganizationName = e.Organizer != null ? e.Organizer.OrganizationName : null
                    })
                    .FirstOrDefaultAsync();

                if (organizerInfo == null) return null;

                _logger.LogDebug("✅ ORGANIZER DATA LOADED: Name={Name}, Email={Email}, Organization={Organization}", 
                    organizerInfo.OrganizerName, organizerInfo.OrganizerEmail, organizerInfo.OrganizationName);

                return new OrganizerDetails
                {
                    Name = organizerInfo.OrganizerName,
                    ContactEmail = organizerInfo.OrganizerEmail,
                    PhoneNumber = organizerInfo.OrganizerPhone,
                    Website = organizerInfo.OrganizerWebsite,
                    OrganizationName = organizerInfo.OrganizationName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching organizer details for event ID: {EventId}", eventId);
                return null;
            }
        }

        /// <summary>
        /// Helper class for event details
        /// </summary>
        private class EventDetails
        {
            public DateTime? Date { get; set; }
            public string? Location { get; set; }
            public string? Description { get; set; }
            public string? ImageUrl { get; set; }
        }

        /// <summary>
        /// Helper class for organizer details
        /// </summary>
        private class OrganizerDetails
        {
            public string? Name { get; set; }
            public string? ContactEmail { get; set; }
            public string? PhoneNumber { get; set; }
            public string? Website { get; set; }
            public string? OrganizationName { get; set; }
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

        public async Task<byte[]> GenerateTicketPdfAsync(string eventId, string eventName, string seatNumber, string firstName, byte[] qrCodeImage, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null)
        {
            _logger.LogInformation("Generating PDF ticket for Event: {EventName}, Seat: {SeatNumber}, Attendee: {FirstName}, FoodItems: {FoodCount}",
                eventName, seatNumber, firstName, foodOrders?.Count ?? 0);

            // Fetch additional event details and organizer information from database
            var eventDetails = await GetEventDetailsAsync(eventId);
            var organizerInfo = await GetOrganizerDetailsAsync(eventId);
            
            // Prioritize database image URL over passed parameter
            var finalEventImageUrl = eventDetails?.ImageUrl ?? eventImageUrl;
            _logger.LogDebug("Event image URL: Database={DbUrl}, Passed={PassedUrl}, Final={FinalUrl}", 
                eventDetails?.ImageUrl, eventImageUrl, finalEventImageUrl);

            using (var stream = new MemoryStream())
            {
                // Create document with professional margins
                Document document = new Document(PageSize.A4, 30, 30, 30, 30);
                PdfWriter writer = PdfWriter.GetInstance(document, stream);
                document.Open();

                // CINEMA HEADER with gradient-style effect
                var cinemaHeaderTable = new PdfPTable(1);
                cinemaHeaderTable.WidthPercentage = 100;
                
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 24, new BaseColor(255, 215, 0)); // Gold
                var headerPara = new Paragraph("� PREMIUM CINEMA TICKET 🎬", headerFont);
                headerPara.Alignment = Element.ALIGN_CENTER;
                headerPara.SpacingBefore = 10f;
                headerPara.SpacingAfter = 10f;
                
                var headerCell = new PdfPCell(headerPara);
                headerCell.BackgroundColor = new BaseColor(139, 0, 0); // Dark red cinema style
                headerCell.BorderWidth = 3f;
                headerCell.BorderColor = new BaseColor(255, 215, 0); // Gold border
                headerCell.Padding = 15f;
                headerCell.HorizontalAlignment = Element.ALIGN_CENTER;
                
                cinemaHeaderTable.AddCell(headerCell);
                cinemaHeaderTable.SpacingAfter = 15f;
                document.Add(cinemaHeaderTable);

                // MAIN CONTENT: Event Image in Cinema Style Frame
                if (!string.IsNullOrEmpty(finalEventImageUrl))
                {
                    try
                    {
                        _logger.LogInformation("Attempting to load event image from: {ImageUrl}", finalEventImageUrl);
                        
                        // Convert relative URL to absolute URL for HttpClient
                        string fullImageUrl = finalEventImageUrl;
                        if (finalEventImageUrl.StartsWith("/"))
                        {
                            fullImageUrl = $"http://localhost:5000{finalEventImageUrl}";
                            _logger.LogInformation("Converting relative URL to absolute: {RelativeUrl} -> {FullUrl}", finalEventImageUrl, fullImageUrl);
                        }
                        
                        var eventImageBytes = await _httpClient.GetByteArrayAsync(fullImageUrl);
                        var eventImage = Image.GetInstance(eventImageBytes);
                        
                        // Cinema-style image frame
                        var imageTable = new PdfPTable(1);
                        imageTable.WidthPercentage = 90;
                        imageTable.HorizontalAlignment = Element.ALIGN_CENTER;
                        
                        // Scale the image for cinema presentation - reduced size to prevent overflow
                        eventImage.ScaleToFit(250f, 150f);
                        eventImage.Alignment = Element.ALIGN_CENTER;
                        
                        var imageCell = new PdfPCell(eventImage);
                        imageCell.BackgroundColor = new BaseColor(0, 0, 0); // Black cinema frame
                        imageCell.BorderWidth = 2f; // Reduced border width
                        imageCell.BorderColor = new BaseColor(255, 215, 0); // Gold frame
                        imageCell.Padding = 6f; // Reduced padding
                        imageCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        
                        imageTable.AddCell(imageCell);
                        imageTable.SpacingAfter = 15f; // Reduced spacing
                        document.Add(imageTable);
                        
                        _logger.LogInformation("Successfully added event image to ticket from: {ImageUrl}", fullImageUrl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load event image from {ImageUrl}, adding cinema placeholder", finalEventImageUrl);
                        
                        // Cinema-style placeholder
                        var placeholderTable = new PdfPTable(1);
                        placeholderTable.WidthPercentage = 90;
                        placeholderTable.HorizontalAlignment = Element.ALIGN_CENTER;
                        
                        var placeholderPara = new Paragraph("🎬 FEATURE PRESENTATION LOADING 🎬", 
                            FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, new BaseColor(255, 215, 0)));
                        placeholderPara.Alignment = Element.ALIGN_CENTER;
                        
                        var placeholderCell = new PdfPCell(placeholderPara);
                        placeholderCell.BackgroundColor = new BaseColor(139, 0, 0);
                        placeholderCell.BorderWidth = 2f; // Reduced border
                        placeholderCell.BorderColor = new BaseColor(255, 215, 0);
                        placeholderCell.Padding = 20f; // Reduced padding
                        placeholderCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        
                        placeholderTable.AddCell(placeholderCell);
                        placeholderTable.SpacingAfter = 15f; // Reduced spacing
                        document.Add(placeholderTable);
                    }
                }

                // EVENT TITLE - Cinema Marquee Style
                var eventTitleTable = new PdfPTable(1);
                eventTitleTable.WidthPercentage = 95;
                eventTitleTable.HorizontalAlignment = Element.ALIGN_CENTER;
                
                var eventTitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 20, new BaseColor(255, 255, 255)); // White text
                var eventTitlePara = new Paragraph($"🎭 {eventName?.ToUpper() ?? "FEATURE PRESENTATION"} 🎭", eventTitleFont);
                eventTitlePara.Alignment = Element.ALIGN_CENTER;
                
                var titleCell = new PdfPCell(eventTitlePara);
                titleCell.BackgroundColor = new BaseColor(25, 25, 112); // Midnight blue
                titleCell.BorderWidth = 2f;
                titleCell.BorderColor = new BaseColor(255, 215, 0);
                titleCell.Padding = 12f;
                titleCell.HorizontalAlignment = Element.ALIGN_CENTER;
                
                eventTitleTable.AddCell(titleCell);
                eventTitleTable.SpacingAfter = 15f;
                document.Add(eventTitleTable);

                // Event Details in Cinema Info Style
                if (eventDetails != null)
                {
                    var detailsTable = new PdfPTable(1);
                    detailsTable.WidthPercentage = 90;
                    detailsTable.HorizontalAlignment = Element.ALIGN_CENTER;
                    
                    var detailsText = "";
                    if (eventDetails.Date.HasValue)
                    {
                        detailsText += $"�️ SHOWTIME: {eventDetails.Date.Value:dddd, MMMM dd, yyyy 'at' h:mm tt}\n";
                    }
                    if (!string.IsNullOrEmpty(eventDetails.Location))
                    {
                        detailsText += $"🏛️ VENUE: {eventDetails.Location}\n";
                    }
                    if (!string.IsNullOrEmpty(eventDetails.Description))
                    {
                        detailsText += $"📝 {eventDetails.Description}";
                    }
                    
                    if (!string.IsNullOrEmpty(detailsText))
                    {
                        var detailFont = FontFactory.GetFont(FontFactory.HELVETICA, 11, new BaseColor(255, 255, 255));
                        var detailsPara = new Paragraph(detailsText.Trim(), detailFont);
                        detailsPara.Alignment = Element.ALIGN_CENTER;
                        
                        var detailsCell = new PdfPCell(detailsPara);
                        detailsCell.BackgroundColor = new BaseColor(47, 79, 79); // Dark slate gray
                        detailsCell.BorderWidth = 1f;
                        detailsCell.BorderColor = new BaseColor(255, 215, 0);
                        detailsCell.Padding = 10f;
                        detailsCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        
                        detailsTable.AddCell(detailsCell);
                        detailsTable.SpacingAfter = 15f;
                        document.Add(detailsTable);
                    }
                }

                // ATTENDEE INFORMATION - VIP Cinema Style
                var attendeeTable = new PdfPTable(2);
                attendeeTable.WidthPercentage = 95;
                attendeeTable.SetWidths(new float[] { 60f, 40f });
                attendeeTable.HorizontalAlignment = Element.ALIGN_CENTER;
                
                // Left side - Attendee info
                var attendeeHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, new BaseColor(255, 215, 0)); // Gold
                var attendeeInfoText = $"🎪 VIP GUEST\n\n";
                attendeeInfoText += $"👤 {firstName ?? "VALUED GUEST"}\n";
                attendeeInfoText += $"🎫 Ticket ID: {eventId ?? "PREMIUM"}";
                
                var attendeeFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, new BaseColor(255, 255, 255));
                var attendeePara = new Paragraph(attendeeInfoText, attendeeFont);
                attendeePara.Alignment = Element.ALIGN_LEFT;
                
                var attendeeCell = new PdfPCell(attendeePara);
                attendeeCell.BackgroundColor = new BaseColor(128, 0, 0); // Dark red
                attendeeCell.BorderWidth = 2f;
                attendeeCell.BorderColor = new BaseColor(255, 215, 0);
                attendeeCell.Padding = 15f;
                attendeeCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                
                // Right side - Seat info with cinema styling
                var seatFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, new BaseColor(255, 255, 255));
                var seatText = $"🪑 SEAT\n\n{seatNumber ?? "GENERAL\nADMISSION"}";
                var seatPara = new Paragraph(seatText, seatFont);
                seatPara.Alignment = Element.ALIGN_CENTER;
                
                var seatCell = new PdfPCell(seatPara);
                seatCell.BackgroundColor = new BaseColor(25, 25, 112); // Midnight blue
                seatCell.BorderWidth = 2f;
                seatCell.BorderColor = new BaseColor(255, 215, 0);
                seatCell.Padding = 15f;
                seatCell.HorizontalAlignment = Element.ALIGN_CENTER;
                seatCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                
                attendeeTable.AddCell(attendeeCell);
                attendeeTable.AddCell(seatCell);
                attendeeTable.SpacingAfter = 20f;
                document.Add(attendeeTable);

                // CONCESSIONS SECTION - Cinema Style Food & Beverages
                if (foodOrders != null && foodOrders.Any())
                {
                    // Food section header with cinema concession styling
                    var concessionHeaderTable = new PdfPTable(1);
                    concessionHeaderTable.WidthPercentage = 95;
                    concessionHeaderTable.HorizontalAlignment = Element.ALIGN_CENTER;
                    
                    var foodHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, new BaseColor(255, 255, 255));
                    var foodSectionTitle = new Paragraph("🍿 CONCESSION PACKAGE 🥤", foodHeaderFont);
                    foodSectionTitle.Alignment = Element.ALIGN_CENTER;
                    
                    var concessionHeaderCell = new PdfPCell(foodSectionTitle);
                    concessionHeaderCell.BackgroundColor = new BaseColor(184, 134, 11); // Cinema concession gold
                    concessionHeaderCell.BorderWidth = 2f;
                    concessionHeaderCell.BorderColor = new BaseColor(255, 215, 0);
                    concessionHeaderCell.Padding = 10f;
                    concessionHeaderCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    
                    concessionHeaderTable.AddCell(concessionHeaderCell);
                    concessionHeaderTable.SpacingAfter = 10f;
                    document.Add(concessionHeaderTable);
                    
                    // Cinema-style food table with dark theme
                    PdfPTable foodTable = new PdfPTable(4);
                    foodTable.WidthPercentage = 95;
                    foodTable.SetWidths(new float[] { 40f, 15f, 20f, 25f });
                    foodTable.HorizontalAlignment = Element.ALIGN_CENTER;
                    
                    // Table headers with cinema styling
                    var tableHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, new BaseColor(255, 255, 255));
                    var headerCells = new[]
                    {
                        new PdfPCell(new Phrase("🍿 CONCESSION ITEM", tableHeaderFont)),
                        new PdfPCell(new Phrase("QTY", tableHeaderFont)),
                        new PdfPCell(new Phrase("PRICE", tableHeaderFont)),
                        new PdfPCell(new Phrase("TOTAL", tableHeaderFont))
                    };
                    
                    foreach (var cell in headerCells)
                    {
                        cell.BackgroundColor = new BaseColor(139, 0, 0); // Dark red
                        cell.BorderColor = new BaseColor(255, 215, 0);
                        cell.BorderWidth = 1f;
                        cell.Padding = 8f;
                        cell.HorizontalAlignment = Element.ALIGN_CENTER;
                        foodTable.AddCell(cell);
                    }
                    
                    decimal totalFoodCost = 0;
                    var cellFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, new BaseColor(255, 255, 255));
                    
                    // Add food items with alternating row colors
                    for (int i = 0; i < foodOrders.Count; i++)
                    {
                        var food = foodOrders[i];
                        var rowColor = i % 2 == 0 ? new BaseColor(47, 79, 79) : new BaseColor(25, 25, 112); // Alternating dark colors
                        
                        var cells = new[]
                        {
                            new PdfPCell(new Phrase(food.Name, cellFont)),
                            new PdfPCell(new Phrase(food.Quantity.ToString(), cellFont)) { HorizontalAlignment = Element.ALIGN_CENTER },
                            new PdfPCell(new Phrase($"${food.UnitPrice:F2}", cellFont)) { HorizontalAlignment = Element.ALIGN_RIGHT },
                            new PdfPCell(new Phrase($"${food.TotalPrice:F2}", cellFont)) { HorizontalAlignment = Element.ALIGN_RIGHT }
                        };
                        
                        foreach (var cell in cells)
                        {
                            cell.BackgroundColor = rowColor;
                            cell.BorderColor = new BaseColor(255, 215, 0);
                            cell.BorderWidth = 0.5f;
                            cell.Padding = 6f;
                            foodTable.AddCell(cell);
                        }
                        
                        totalFoodCost += food.TotalPrice;
                    }
                    
                    // Total row with special cinema styling
                    var totalRowFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, new BaseColor(255, 215, 0));
                    var totalCells = new[]
                    {
                        new PdfPCell(new Phrase("🎬 CONCESSION TOTAL", totalRowFont)) 
                        { 
                            Colspan = 3, 
                            HorizontalAlignment = Element.ALIGN_RIGHT, 
                            BackgroundColor = new BaseColor(0, 0, 0),
                            BorderColor = new BaseColor(255, 215, 0),
                            BorderWidth = 2f,
                            Padding = 8f
                        },
                        new PdfPCell(new Phrase($"${totalFoodCost:F2}", totalRowFont)) 
                        { 
                            HorizontalAlignment = Element.ALIGN_RIGHT, 
                            BackgroundColor = new BaseColor(0, 0, 0),
                            BorderColor = new BaseColor(255, 215, 0),
                            BorderWidth = 2f,
                            Padding = 8f
                        }
                    };
                    
                    foreach (var cell in totalCells)
                    {
                        foodTable.AddCell(cell);
                    }
                    
                    foodTable.SpacingAfter = 20f;
                    document.Add(foodTable);
                }

                // QR CODE SECTION - Cinema Style Entry Pass
                var qrSectionTable = new PdfPTable(2);
                qrSectionTable.WidthPercentage = 95;
                qrSectionTable.SetWidths(new float[] { 65f, 35f });
                qrSectionTable.HorizontalAlignment = Element.ALIGN_CENTER;
                
                // Left side - QR Instructions
                var qrInstructionsFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, new BaseColor(255, 255, 255));
                var instructionsText = "🚪 ENTRY VERIFICATION\n\n";
                instructionsText += "📱 Present this QR code at the venue entrance\n";
                instructionsText += "⚡ Quick scan for instant admission\n";
                instructionsText += "🎬 No paper ticket required";
                
                var instructionsPara = new Paragraph(instructionsText, qrInstructionsFont);
                instructionsPara.Alignment = Element.ALIGN_LEFT;
                
                var instructionsCell = new PdfPCell(instructionsPara);
                instructionsCell.BackgroundColor = new BaseColor(47, 79, 79); // Dark slate gray
                instructionsCell.BorderWidth = 2f;
                instructionsCell.BorderColor = new BaseColor(255, 215, 0);
                instructionsCell.Padding = 15f;
                instructionsCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                
                // Right side - QR Code with cinema frame
                if (qrCodeImage != null && qrCodeImage.Length > 0)
                {
                    try
                    {
                        var qrImage = Image.GetInstance(qrCodeImage);
                        qrImage.ScaleToFit(130f, 130f);
                        qrImage.Alignment = Element.ALIGN_CENTER;
                        
                        var qrCell = new PdfPCell(qrImage);
                        qrCell.BackgroundColor = new BaseColor(255, 255, 255); // White background for QR
                        qrCell.BorderWidth = 3f;
                        qrCell.BorderColor = new BaseColor(255, 215, 0);
                        qrCell.Padding = 10f;
                        qrCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        qrCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        
                        qrSectionTable.AddCell(instructionsCell);
                        qrSectionTable.AddCell(qrCell);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to add QR code to cinema-style PDF");
                        
                        // Fallback QR placeholder
                        var qrPlaceholderPara = new Paragraph("📱\nQR CODE\nUNAVAILABLE", 
                            FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, new BaseColor(255, 255, 255)));
                        qrPlaceholderPara.Alignment = Element.ALIGN_CENTER;
                        
                        var qrPlaceholderCell = new PdfPCell(qrPlaceholderPara);
                        qrPlaceholderCell.BackgroundColor = new BaseColor(139, 0, 0);
                        qrPlaceholderCell.BorderWidth = 3f;
                        qrPlaceholderCell.BorderColor = new BaseColor(255, 215, 0);
                        qrPlaceholderCell.Padding = 30f;
                        qrPlaceholderCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        qrPlaceholderCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                        
                        qrSectionTable.AddCell(instructionsCell);
                        qrSectionTable.AddCell(qrPlaceholderCell);
                    }
                }
                else
                {
                    // No QR code available
                    var noQrPara = new Paragraph("📋\nMANUAL\nCHECK-IN", 
                        FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, new BaseColor(255, 255, 255)));
                    noQrPara.Alignment = Element.ALIGN_CENTER;
                    
                    var noQrCell = new PdfPCell(noQrPara);
                    noQrCell.BackgroundColor = new BaseColor(139, 0, 0);
                    noQrCell.BorderWidth = 3f;
                    noQrCell.BorderColor = new BaseColor(255, 215, 0);
                    noQrCell.Padding = 30f;
                    noQrCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    noQrCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                    
                    qrSectionTable.AddCell(instructionsCell);
                    qrSectionTable.AddCell(noQrCell);
                }
                
                qrSectionTable.SpacingAfter = 20f;
                document.Add(qrSectionTable);

                // ORGANIZER FOOTER - Cinema Credits Style
                if (organizerInfo != null)
                {
                    var footerTable = new PdfPTable(1);
                    footerTable.WidthPercentage = 100;
                    
                    var organizerText = "🎭 PRESENTED BY 🎭\n";
                    organizerText += $"{organizerInfo.Name}";
                    
                    if (!string.IsNullOrEmpty(organizerInfo.OrganizationName))
                    {
                        organizerText += $"\n{organizerInfo.OrganizationName}";
                    }
                    
                    var contactInfo = "";
                    if (!string.IsNullOrEmpty(organizerInfo.ContactEmail))
                    {
                        contactInfo += $"📧 {organizerInfo.ContactEmail}";
                    }
                    if (!string.IsNullOrEmpty(organizerInfo.PhoneNumber))
                    {
                        if (!string.IsNullOrEmpty(contactInfo)) contactInfo += " | ";
                        contactInfo += $"📞 {organizerInfo.PhoneNumber}";
                    }
                    if (!string.IsNullOrEmpty(organizerInfo.Website))
                    {
                        if (!string.IsNullOrEmpty(contactInfo)) contactInfo += " | ";
                        contactInfo += $"🌐 {organizerInfo.Website}";
                    }
                    
                    if (!string.IsNullOrEmpty(contactInfo))
                    {
                        organizerText += $"\n{contactInfo}";
                    }
                    
                    var organizerFont = FontFactory.GetFont(FontFactory.HELVETICA, 10, new BaseColor(255, 215, 0));
                    var organizerPara = new Paragraph(organizerText, organizerFont);
                    organizerPara.Alignment = Element.ALIGN_CENTER;
                    
                    var footerCell = new PdfPCell(organizerPara);
                    footerCell.BackgroundColor = new BaseColor(0, 0, 0); // Black footer
                    footerCell.BorderWidth = 2f;
                    footerCell.BorderColor = new BaseColor(255, 215, 0);
                    footerCell.Padding = 12f;
                    footerCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    
                    footerTable.AddCell(footerCell);
                    footerTable.SpacingBefore = 15f;
                    document.Add(footerTable);
                }

                // Final cinema-style disclaimer
                var disclaimerFont = FontFactory.GetFont(FontFactory.HELVETICA, 8, new BaseColor(169, 169, 169)); // Gray
                var disclaimer = new Paragraph("🎬 This is your official admission ticket. Please arrive 15 minutes before showtime. 🎬", disclaimerFont);
                disclaimer.Alignment = Element.ALIGN_CENTER;
                disclaimer.SpacingBefore = 10f;
                document.Add(disclaimer);

                document.Close();
                _logger.LogInformation("Cinema-style PDF ticket generated successfully with {FoodCount} concession items", foodOrders?.Count ?? 0);
                return stream.ToArray();
            }
        }

        public async Task<byte[]> GenerateProfessionalConcertTicketAsync(string eventId, string eventName, string seatNumber, string firstName, byte[] qrCodeImage, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null)
        {
            _logger.LogInformation("🎵 PROFESSIONAL DESIGN - Generating concert ticket for Event: {EventName}, Seat: {SeatNumber}, Attendee: {FirstName}, FoodItems: {FoodCount}",
                eventName, seatNumber, firstName, foodOrders?.Count ?? 0);

            // Fetch additional event details and organizer information from database
            var eventDetails = await GetEventDetailsAsync(eventId);
            var organizerInfo = await GetOrganizerDetailsAsync(eventId);
            
            // Prioritize database image URL over passed parameter
            var finalEventImageUrl = eventDetails?.ImageUrl ?? eventImageUrl;

            using (var stream = new MemoryStream())
            {
                // Create document with professional margins
                Document document = new Document(PageSize.A4, 25, 25, 25, 25);
                PdfWriter writer = PdfWriter.GetInstance(document, stream);
                document.Open();

                // MAIN TITLE: Event Name (Auto-adjusting font size, no suffix)
                var headerTable = new PdfPTable(1);
                headerTable.WidthPercentage = 100;
                
                var eventTitle = eventName?.ToUpper() ?? "MUSICAL CONCERT"; // Removed "- TICKET" suffix
                
                // Auto-adjust font size based on title length for single line display
                var fontSize = 24f;
                if (eventTitle.Length > 25) fontSize = 22f;
                if (eventTitle.Length > 30) fontSize = 20f;
                if (eventTitle.Length > 40) fontSize = 18f;
                if (eventTitle.Length > 50) fontSize = 16f;
                if (eventTitle.Length > 60) fontSize = 14f;
                
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, fontSize, new BaseColor(78, 205, 196)); // Teal
                var headerPara = new Paragraph(eventTitle, headerFont);
                headerPara.Alignment = Element.ALIGN_CENTER;
                headerPara.SpacingBefore = 8f;  // Reduced spacing
                headerPara.SpacingAfter = 6f;   // Reduced spacing
                
                var headerCell = new PdfPCell(headerPara);
                headerCell.BackgroundColor = new BaseColor(26, 0, 51); // Deep purple
                headerCell.BorderWidth = 3f;
                headerCell.BorderColor = new BaseColor(255, 107, 107); // Coral border
                headerCell.Padding = 10f; // Reduced padding for more space
                headerCell.HorizontalAlignment = Element.ALIGN_CENTER;
                
                headerTable.AddCell(headerCell);
                headerTable.SpacingAfter = 10f; // Reduced spacing
                document.Add(headerTable);

                // MAIN CONTENT SECTION - Two Column Layout (Compact for single page)
                var mainTable = new PdfPTable(2);
                mainTable.WidthPercentage = 100;
                mainTable.SetWidths(new float[] { 55f, 45f });

                // LEFT COLUMN - Event Image (No Border, Full Size)
                var leftCell = new PdfPCell();
                leftCell.BackgroundColor = new BaseColor(248, 249, 250); // Light gray background
                leftCell.BorderWidth = 2f;
                leftCell.BorderColor = new BaseColor(78, 205, 196);
                leftCell.Padding = 15f; // Reduced padding
                leftCell.VerticalAlignment = Element.ALIGN_TOP;

                // Event Image Section (No border, full expansion)
                if (!string.IsNullOrEmpty(finalEventImageUrl))
                {
                    try
                    {
                        byte[] eventImageBytes;
                        
                        // Handle different image sources
                        if (finalEventImageUrl.StartsWith("http://") || finalEventImageUrl.StartsWith("https://"))
                        {
                            // HTTP/HTTPS URL - use HttpClient
                            eventImageBytes = await _httpClient.GetByteArrayAsync(finalEventImageUrl);
                        }
                        else if (finalEventImageUrl.StartsWith("/"))
                        {
                            // Relative path - convert to localhost URL
                            string fullImageUrl = $"http://localhost:5000{finalEventImageUrl}";
                            eventImageBytes = await _httpClient.GetByteArrayAsync(fullImageUrl);
                        }
                        else if (Path.IsPathRooted(finalEventImageUrl) && File.Exists(finalEventImageUrl))
                        {
                            // Local file path - read directly from file system
                            eventImageBytes = await File.ReadAllBytesAsync(finalEventImageUrl);
                        }
                        else
                        {
                            throw new FileNotFoundException($"Image not found: {finalEventImageUrl}");
                        }
                        
                        var eventImage = Image.GetInstance(eventImageBytes);
                        
                        // Maximum size image to fill panel while maintaining aspect ratio (Increased to use most of left panel space)
                        eventImage.ScaleToFit(280f, 400f); // Wider and much taller to fill the left panel
                        eventImage.Alignment = Element.ALIGN_CENTER;
                        eventImage.SpacingAfter = 10f;
                        
                        // Add image directly without table/border
                        leftCell.AddElement(eventImage);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load event image, using placeholder");
                        
                        var placeholderPara = new Paragraph("🎼 EVENT POSTER 🎼", 
                            FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, new BaseColor(78, 205, 196)));
                        placeholderPara.Alignment = Element.ALIGN_CENTER;
                        placeholderPara.SpacingAfter = 10f;
                        leftCell.AddElement(placeholderPara);
                    }
                }

                // RIGHT COLUMN - Attendee Details, Food Orders, and QR Code (Compact)
                var rightCell = new PdfPCell();
                rightCell.BackgroundColor = new BaseColor(255, 255, 255); // White background
                rightCell.BorderWidth = 2f;
                rightCell.BorderColor = new BaseColor(255, 107, 107);
                rightCell.Padding = 15f; // Reduced padding
                rightCell.VerticalAlignment = Element.ALIGN_TOP;

                // VIP Badge (Compact)
                var vipTable = new PdfPTable(1);
                vipTable.WidthPercentage = 100;
                var vipFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, BaseColor.WHITE);
                var vipPara = new Paragraph("CONCERT GUEST", vipFont);
                vipPara.Alignment = Element.ALIGN_CENTER;
                
                var vipCell = new PdfPCell(vipPara);
                vipCell.BackgroundColor = new BaseColor(255, 107, 107);
                vipCell.BorderWidth = 0f;
                vipCell.Padding = 6f;
                vipCell.HorizontalAlignment = Element.ALIGN_CENTER;
                vipTable.AddCell(vipCell);
                vipTable.SpacingAfter = 10f;
                rightCell.AddElement(vipTable);

                // Attendee Name (Compact)
                var nameFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, new BaseColor(45, 55, 72));
                var namePara = new Paragraph((firstName ?? "VALUED GUEST").ToUpper(), nameFont);
                namePara.Alignment = Element.ALIGN_CENTER;
                namePara.SpacingAfter = 10f;
                rightCell.AddElement(namePara);

                // Seat Information (Compact)
                var seatTable = new PdfPTable(2);
                seatTable.WidthPercentage = 100;
                
                var seatLabelFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, new BaseColor(78, 205, 196));
                var seatValueFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, new BaseColor(45, 55, 72));
                
                // Section
                var sectionCell = new PdfPCell();
                sectionCell.BorderWidth = 0f;
                sectionCell.Padding = 8f;
                sectionCell.HorizontalAlignment = Element.ALIGN_CENTER;
                var sectionPara = new Paragraph();
                sectionPara.Add(new Chunk("SECTION\n", seatLabelFont));
                sectionPara.Add(new Chunk("VIP", seatValueFont));
                sectionPara.Alignment = Element.ALIGN_CENTER;
                sectionCell.AddElement(sectionPara);
                
                // Seat
                var seatInfoCell = new PdfPCell();
                seatInfoCell.BorderWidth = 0f;
                seatInfoCell.Padding = 8f;
                seatInfoCell.HorizontalAlignment = Element.ALIGN_CENTER;
                var seatPara = new Paragraph();
                seatPara.Add(new Chunk("SEAT\n", seatLabelFont));
                seatPara.Add(new Chunk(seatNumber ?? "GA", seatValueFont));
                seatPara.Alignment = Element.ALIGN_CENTER;
                seatInfoCell.AddElement(seatPara);
                
                seatTable.AddCell(sectionCell);
                seatTable.AddCell(seatInfoCell);
                seatTable.SpacingAfter = 12f;
                rightCell.AddElement(seatTable);

                // Food Orders Section (Compact)
                if (foodOrders != null && foodOrders.Any())
                {
                    var merchHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, new BaseColor(78, 205, 196));
                    var merchHeader = new Paragraph("🛍️ MERCHANDISE", merchHeaderFont);
                    merchHeader.Alignment = Element.ALIGN_CENTER;
                    merchHeader.SpacingBefore = 3f;
                    merchHeader.SpacingAfter = 6f;
                    rightCell.AddElement(merchHeader);
                    
                    var merchTable = new PdfPTable(2);
                    merchTable.WidthPercentage = 100;
                    merchTable.SetWidths(new float[] { 70f, 30f });
                    
                    decimal totalMerch = 0;
                    var itemFont = FontFactory.GetFont(FontFactory.HELVETICA, 8, new BaseColor(55, 65, 81));
                    
                    foreach (var item in foodOrders)
                    {
                        var itemText = $"{item.Name} (x{item.Quantity})";
                        merchTable.AddCell(new PdfPCell(new Phrase(itemText, itemFont)) { 
                            Padding = 3f, 
                            BorderWidth = 0f 
                        });
                        merchTable.AddCell(new PdfPCell(new Phrase($"${item.TotalPrice:F2}", itemFont)) 
                        { 
                            Padding = 3f, 
                            HorizontalAlignment = Element.ALIGN_RIGHT,
                            BorderWidth = 0f 
                        });
                        totalMerch += item.TotalPrice;
                    }
                    
                    // Total row
                    var totalFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, new BaseColor(78, 205, 196));
                    merchTable.AddCell(new PdfPCell(new Phrase("TOTAL", totalFont)) 
                    { 
                        Padding = 4f, 
                        HorizontalAlignment = Element.ALIGN_RIGHT,
                        BackgroundColor = new BaseColor(248, 249, 250),
                        BorderWidth = 1f,
                        BorderColor = new BaseColor(78, 205, 196)
                    });
                    merchTable.AddCell(new PdfPCell(new Phrase($"${totalMerch:F2}", totalFont)) 
                    { 
                        Padding = 4f, 
                        HorizontalAlignment = Element.ALIGN_RIGHT,
                        BackgroundColor = new BaseColor(248, 249, 250),
                        BorderWidth = 1f,
                        BorderColor = new BaseColor(78, 205, 196)
                    });
                    
                    merchTable.SpacingAfter = 12f;
                    rightCell.AddElement(merchTable);
                }

                // QR Code Section (Compact)
                var qrTitleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, new BaseColor(78, 205, 196));
                var qrTitle = new Paragraph("🎫 ENTRY CODE", qrTitleFont);
                qrTitle.Alignment = Element.ALIGN_CENTER;
                qrTitle.SpacingAfter = 6f;
                rightCell.AddElement(qrTitle);

                if (qrCodeImage != null && qrCodeImage.Length > 0)
                {
                    try
                    {
                        var qrImage = Image.GetInstance(qrCodeImage);
                        qrImage.ScaleToFit(90f, 90f); // Smaller QR code
                        qrImage.Alignment = Element.ALIGN_CENTER;
                        
                        var qrTable = new PdfPTable(1);
                        qrTable.WidthPercentage = 100;
                        var qrImageCell = new PdfPCell(qrImage);
                        qrImageCell.BorderWidth = 2f;
                        qrImageCell.BorderColor = new BaseColor(255, 107, 107);
                        qrImageCell.Padding = 6f;
                        qrImageCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        qrTable.AddCell(qrImageCell);
                        qrTable.SpacingAfter = 6f;
                        rightCell.AddElement(qrTable);
                        
                        var instructionsFont = FontFactory.GetFont(FontFactory.HELVETICA, 7, new BaseColor(107, 114, 128));
                        var instructions = new Paragraph("Scan at venue entrance", instructionsFont);
                        instructions.Alignment = Element.ALIGN_CENTER;
                        rightCell.AddElement(instructions);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to add QR code to PDF");
                        
                        var qrPlaceholder = new Paragraph("QR CODE\nGENERATED", 
                            FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, new BaseColor(78, 205, 196)));
                        qrPlaceholder.Alignment = Element.ALIGN_CENTER;
                        rightCell.AddElement(qrPlaceholder);
                    }
                }

                mainTable.AddCell(leftCell);
                mainTable.AddCell(rightCell);
                mainTable.SpacingAfter = 12f; // Reduced spacing
                document.Add(mainTable);

                // EVENT DETAILS PANEL (Show time, Venue, Details) - Spanning across two columns (Compact)
                var eventDetailsTable = new PdfPTable(1);
                eventDetailsTable.WidthPercentage = 100;
                
                var eventDetailsCell = new PdfPCell();
                eventDetailsCell.BackgroundColor = new BaseColor(245, 247, 250); // Very light blue-gray
                eventDetailsCell.BorderWidth = 2f;
                eventDetailsCell.BorderColor = new BaseColor(78, 205, 196);
                eventDetailsCell.Padding = 10f; // Reduced padding
                
                if (eventDetails != null)
                {
                    var infoFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, new BaseColor(55, 65, 81)); // Smaller font
                    var infoBoldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9, new BaseColor(55, 65, 81)); // Smaller font
                    
                    var eventInfoTable = new PdfPTable(3);
                    eventInfoTable.WidthPercentage = 100;
                    eventInfoTable.SetWidths(new float[] { 33f, 33f, 34f });
                    
                    // Show Time
                    if (eventDetails.Date.HasValue)
                    {
                        var dateCell = new PdfPCell();
                        dateCell.BorderWidth = 0f;
                        dateCell.Padding = 5f; // Reduced padding
                        var datePara = new Paragraph();
                        datePara.Add(new Chunk("🎵 SHOW TIME\n", infoBoldFont));
                        datePara.Add(new Chunk(eventDetails.Date.Value.ToString("dddd, MMMM dd, yyyy\nh:mm tt"), infoFont));
                        datePara.Alignment = Element.ALIGN_CENTER;
                        dateCell.AddElement(datePara);
                        eventInfoTable.AddCell(dateCell);
                    }
                    else
                    {
                        eventInfoTable.AddCell(new PdfPCell(new Phrase("")) { BorderWidth = 0f });
                    }
                    
                    // Venue
                    if (!string.IsNullOrEmpty(eventDetails.Location))
                    {
                        var locationCell = new PdfPCell();
                        locationCell.BorderWidth = 0f;
                        locationCell.Padding = 5f; // Reduced padding
                        var locationPara = new Paragraph();
                        locationPara.Add(new Chunk("🏟️ VENUE\n", infoBoldFont));
                        locationPara.Add(new Chunk(eventDetails.Location, infoFont));
                        locationPara.Alignment = Element.ALIGN_CENTER;
                        locationCell.AddElement(locationPara);
                        eventInfoTable.AddCell(locationCell);
                    }
                    else
                    {
                        eventInfoTable.AddCell(new PdfPCell(new Phrase("")) { BorderWidth = 0f });
                    }
                    
                    // Details
                    if (!string.IsNullOrEmpty(eventDetails.Description))
                    {
                        var descCell = new PdfPCell();
                        descCell.BorderWidth = 0f;
                        descCell.Padding = 5f; // Reduced padding
                        var descPara = new Paragraph();
                        descPara.Add(new Chunk("🎤 DETAILS\n", infoBoldFont));
                        var truncatedDesc = eventDetails.Description.Length > 50 ? // Shorter description
                                          eventDetails.Description.Substring(0, 50) + "..." : 
                                          eventDetails.Description;
                        descPara.Add(new Chunk(truncatedDesc, infoFont));
                        descPara.Alignment = Element.ALIGN_CENTER;
                        descCell.AddElement(descPara);
                        eventInfoTable.AddCell(descCell);
                    }
                    else
                    {
                        eventInfoTable.AddCell(new PdfPCell(new Phrase("")) { BorderWidth = 0f });
                    }
                    
                    eventDetailsCell.AddElement(eventInfoTable);
                }
                
                eventDetailsTable.AddCell(eventDetailsCell);
                eventDetailsTable.SpacingAfter = 8f; // Reduced spacing
                document.Add(eventDetailsTable);

                // ORGANIZER INFORMATION (Pushed further down, Compact)
                if (organizerInfo != null)
                {
                    var footerTable = new PdfPTable(1);
                    footerTable.WidthPercentage = 100;
                    
                    var organizerText = $"🎭 Presented by {organizerInfo.Name}";
                    if (!string.IsNullOrEmpty(organizerInfo.OrganizationName))
                    {
                        organizerText += $" | {organizerInfo.OrganizationName}";
                    }
                    
                    var contactInfo = "";
                    if (!string.IsNullOrEmpty(organizerInfo.ContactEmail))
                        contactInfo += $"📧 {organizerInfo.ContactEmail}";
                    if (!string.IsNullOrEmpty(organizerInfo.PhoneNumber))
                    {
                        if (!string.IsNullOrEmpty(contactInfo)) contactInfo += " | ";
                        contactInfo += $"📞 {organizerInfo.PhoneNumber}";
                    }
                    if (!string.IsNullOrEmpty(organizerInfo.Website))
                    {
                        if (!string.IsNullOrEmpty(contactInfo)) contactInfo += " | ";
                        contactInfo += $"🌐 {organizerInfo.Website}";
                    }
                    
                    var footerFont = FontFactory.GetFont(FontFactory.HELVETICA, 8, new BaseColor(78, 205, 196)); // Smaller font
                    var footerPara = new Paragraph($"{organizerText}\n{contactInfo}", footerFont);
                    footerPara.Alignment = Element.ALIGN_CENTER;
                    
                    var footerCell = new PdfPCell(footerPara);
                    footerCell.BackgroundColor = new BaseColor(248, 249, 250);
                    footerCell.BorderWidth = 2f;
                    footerCell.BorderColor = new BaseColor(78, 205, 196);
                    footerCell.Padding = 8f; // Reduced padding
                    footerCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    
                    footerTable.AddCell(footerCell);
                    footerTable.SpacingBefore = 5f; // Reduced spacing
                    document.Add(footerTable);
                }

                // Final Disclaimer
                var disclaimerFont = FontFactory.GetFont(FontFactory.HELVETICA, 8, new BaseColor(107, 114, 128));
                var disclaimer = new Paragraph("🎵 This is your official concert admission ticket. Please arrive 30 minutes before show time. 🎵", disclaimerFont);
                disclaimer.Alignment = Element.ALIGN_CENTER;
                disclaimer.SpacingBefore = 10f;
                document.Add(disclaimer);

                document.Close();
                _logger.LogInformation("🎵 PROFESSIONAL DESIGN - Concert ticket PDF generated successfully with {FoodCount} merchandise items", foodOrders?.Count ?? 0);
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
