using EventBooking.API.Data;
using EventBooking.API.Models;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System;
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
                // 🎯 SIMPLIFIED - No duplicate checking, generate fresh ticket for each request
                _logger.LogInformation("🎯 SIMPLIFIED APPROACH - Generating fresh QR ticket for BookingId: {BookingId}, Seat/Ticket: {SeatNo}", 
                    request.BookingId, request.SeatNumber);

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
                    request.EventImageUrl, // ✅ Pass event flyer URL
                    request.TicketType, // ✅ Pass ticket type
                    request.BookingReference, // ✅ Pass booking reference
                    false); // ✅ Regular user booking, not organizer booking
                _logger.LogInformation("🎵 Professional concert ticket PDF generated successfully");

                // Save ticket locally
                _logger.LogInformation("Saving ticket to local storage");
                string localTicketPath = SaveTicketLocally(
                    pdfTicket,
                    request.EventId,
                    request.EventName,
                    request.FirstName,
                    request.PaymentGuid,
                    request.SeatNumber); // 🎯 CRITICAL FIX: Include seat number to prevent filename collisions
                _logger.LogInformation("Ticket saved locally at: {LocalPath}", localTicketPath);
                
                // 🎯 NEW ARCHITECTURE - Update BookingLineItem with QR code information
                _logger.LogInformation("🎯 NEW ARCHITECTURE - Updating BookingLineItem with QR code for BookingId: {BookingId}, Seat: {SeatNumber}", request.BookingId, request.SeatNumber);
                
                // 🎯 CRITICAL FIX: Find the SPECIFIC BookingLineItem for this seat/ticket
                BookingLineItem? ticketLineItem = null;
                
                // For allocated seating, match by seat details
                if (request.SeatNumber.Length <= 3 && char.IsLetter(request.SeatNumber[0])) // Format like "F9", "G10"
                {
                    ticketLineItem = await _context.BookingLineItems
                        .FirstOrDefaultAsync(bli => 
                            bli.BookingId == request.BookingId && 
                            bli.ItemType == "Ticket" && 
                            bli.SeatDetails.Contains(request.SeatNumber));
                    
                    _logger.LogInformation("🎯 ALLOCATED SEATING - Looking for seat {SeatNumber} in BookingLineItems", request.SeatNumber);
                }
                else // For general admission, match by ticket type
                {
                    // Extract ticket type from SeatNumber (e.g., "Standard01-1" -> "Standard01")
                    var ticketType = request.SeatNumber.Contains("-") ? request.SeatNumber.Split('-')[0] : request.SeatNumber;
                    
                    ticketLineItem = await _context.BookingLineItems
                        .FirstOrDefaultAsync(bli => 
                            bli.BookingId == request.BookingId && 
                            bli.ItemType == "Ticket" && 
                            bli.ItemName == ticketType &&
                            (string.IsNullOrEmpty(bli.QRCode) || bli.QRCode == ""));
                    
                    _logger.LogInformation("🎯 GENERAL ADMISSION - Looking for ticket type {TicketType} in BookingLineItems", ticketType);
                }

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
                    ErrorMessage = ex.Message,
                    QRCodeImage = null, // No QR code in error case
                    EventImageUrl = request.EventImageUrl // Still include event image URL if available
                };
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

        public async Task<byte[]> GenerateProfessionalConcertTicketAsync(string eventId, string eventName, string seatNumber, string firstName, byte[] qrCodeImage, List<FoodOrderInfo>? foodOrders = null, string? eventImageUrl = null, string? ticketType = null, string? bookingReference = null, bool isOrganizerBooking = false)
        {
            _logger.LogInformation("🎵 PROFESSIONAL DESIGN - Generating concert ticket for Event: {EventName}, Seat: {SeatNumber}, Attendee: {FirstName}, TicketType: {TicketType}, BookingRef: {BookingRef}, FoodItems: {FoodCount}, IsOrganizerBooking: {IsOrganizerBooking}",
                eventName, seatNumber, firstName, ticketType ?? "Standard", bookingReference ?? "N/A", foodOrders?.Count ?? 0, isOrganizerBooking);

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
                            // Relative path - convert to configured base URL
                            var baseUrl = _configuration["ApplicationSettings:BaseUrl"] 
                                         ?? _configuration["QRTickets:BaseUrl"] 
                                         ?? "http://localhost:5000"; // Fallback
                            string fullImageUrl = $"{baseUrl.TrimEnd('/')}{finalEventImageUrl}";
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

                // Guest Badge (Compact) - Different text for organizer vs user tickets
                var guestTable = new PdfPTable(1);
                guestTable.WidthPercentage = 100;
                var guestFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, BaseColor.WHITE);
                
                // Check if this is an organizer-generated ticket
                bool isOrganizerTicket = isOrganizerBooking;
                var guestText = isOrganizerTicket ? "ORGANIZER GUEST" : "CONCERT GUEST";
                
                var guestPara = new Paragraph(guestText, guestFont);
                guestPara.Alignment = Element.ALIGN_CENTER;
                
                var guestCell = new PdfPCell(guestPara);
                guestCell.BackgroundColor = new BaseColor(255, 107, 107);
                guestCell.BorderWidth = 0f;
                guestCell.Padding = 6f;
                guestCell.HorizontalAlignment = Element.ALIGN_CENTER;
                guestTable.AddCell(guestCell);
                guestTable.SpacingAfter = 10f;
                rightCell.AddElement(guestTable);

                // Attendee Name (Compact)
                var nameFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, new BaseColor(45, 55, 72));
                var namePara = new Paragraph((firstName ?? "VALUED GUEST").ToUpper(), nameFont);
                namePara.Alignment = Element.ALIGN_CENTER;
                namePara.SpacingAfter = 8f;
                rightCell.AddElement(namePara);

                // Ticket Information (Compact) - 2x2 Grid
                var ticketInfoTable = new PdfPTable(2);
                ticketInfoTable.WidthPercentage = 100;
                
                var labelFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, new BaseColor(78, 205, 196));
                var valueFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, new BaseColor(45, 55, 72));
                
                // Seat Number
                var seatCell = new PdfPCell();
                seatCell.BorderWidth = 0f;
                seatCell.Padding = 8f;
                seatCell.HorizontalAlignment = Element.ALIGN_CENTER;
                var seatPara = new Paragraph();
                
                // Check if this is a General Admission ticket 
                // Multiple patterns: booking-based (B123-1), ticket-type based (Adult-1), or parentheses format ((Early B.)-1)
                var seatLabel = "SEAT";
                if (!string.IsNullOrEmpty(seatNumber))
                {
                    if (seatNumber.StartsWith("B") && seatNumber.Contains("-"))
                    {
                        // This is a booking-based identifier for General Admission (organizer booking)
                        seatLabel = "TICKET";
                    }
                    else if (seatNumber.Contains("-") && !char.IsDigit(seatNumber[0]))
                    {
                        // This is a ticket-type based identifier for General Admission (user purchase)
                        // Examples: Adult-1, Student-2, Child-1, VIP-3, (Early B.)-1, (4-12 years)-2
                        seatLabel = "TICKET";
                    }
                    else if (seatNumber.Length <= 3 && char.IsLetter(seatNumber[0]) && char.IsDigit(seatNumber[seatNumber.Length - 1]))
                    {
                        // This is allocated seating (e.g., F9, G10, A1) - keep as "SEAT"
                        seatLabel = "SEAT";
                    }
                    else if (seatNumber.Contains("-"))
                    {
                        // Any other pattern with dash should be General Admission ticket
                        seatLabel = "TICKET";
                    }
                }
                
                seatPara.Add(new Chunk($"{seatLabel}\n", labelFont));
                seatPara.Add(new Chunk(seatNumber ?? "GA", valueFont));
                seatPara.Alignment = Element.ALIGN_CENTER;
                seatCell.AddElement(seatPara);
                
                // Ticket Type
                var typeCell = new PdfPCell();
                typeCell.BorderWidth = 0f;
                typeCell.Padding = 8f;
                typeCell.HorizontalAlignment = Element.ALIGN_CENTER;
                var typePara = new Paragraph();
                typePara.Add(new Chunk("TYPE\n", labelFont));
                typePara.Add(new Chunk(ticketType ?? "STANDARD", valueFont));
                typePara.Alignment = Element.ALIGN_CENTER;
                typeCell.AddElement(typePara);
                
                ticketInfoTable.AddCell(seatCell);
                ticketInfoTable.AddCell(typeCell);
                ticketInfoTable.SpacingAfter = 8f;
                rightCell.AddElement(ticketInfoTable);

                // Booking Reference (Full Width)
                if (!string.IsNullOrEmpty(bookingReference))
                {
                    var bookingRefTable = new PdfPTable(1);
                    bookingRefTable.WidthPercentage = 100;
                    
                    var bookingRefCell = new PdfPCell();
                    bookingRefCell.BorderWidth = 0f;
                    bookingRefCell.Padding = 8f;
                    bookingRefCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    var bookingRefPara = new Paragraph();
                    bookingRefPara.Add(new Chunk("BOOKING REFERENCE\n", labelFont));
                    bookingRefPara.Add(new Chunk(bookingReference, 
                        FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, new BaseColor(45, 55, 72))));
                    bookingRefPara.Alignment = Element.ALIGN_CENTER;
                    bookingRefCell.AddElement(bookingRefPara);
                    
                    bookingRefTable.AddCell(bookingRefCell);
                    bookingRefTable.SpacingAfter = 12f;
                    rightCell.AddElement(bookingRefTable);
                }

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

        public string SaveTicketLocally(byte[] pdfTicket, string eventId, string eventName, string firstName, string paymentGuid, string seatNumber = "")
        {
            try
            {
                // Create filename with timestamp and seat number to prevent collisions
                string sanitizedEventName = string.Join("_", eventName.Split(Path.GetInvalidFileNameChars()));
                string sanitizedFirstName = string.Join("_", firstName.Split(Path.GetInvalidFileNameChars()));
                string sanitizedSeatNumber = string.Join("_", seatNumber.Split(Path.GetInvalidFileNameChars()));
                
                // 🎯 CRITICAL FIX: Include seat number and milliseconds to ensure unique filenames
                string fileName = $"eTicket_{sanitizedEventName}_{sanitizedFirstName}_{paymentGuid}_{sanitizedSeatNumber}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.pdf";
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

        #region QR Code Validation Methods

        /// <summary>
        /// Validates a QR code and returns comprehensive ticket information
        /// </summary>
        public async Task<QRValidationResponse> ValidateQRCodeAsync(QRValidationRequest request)
        {
            _logger.LogInformation("🔍 QR VALIDATION - Starting validation for QR data: {QRData}", request.QRData);

            var response = new QRValidationResponse
            {
                ValidatedAt = DateTime.UtcNow,
                Status = "Processing"
            };

            try
            {
                // Step 1: Parse QR Data
                response.QRData = ParseQRData(request.QRData);
                
                if (!response.QRData.IsParsed)
                {
                    response.IsValid = false;
                    response.Status = "Invalid";
                    response.Message = "QR code format is invalid or unrecognized";
                    _logger.LogWarning("❌ QR VALIDATION - Invalid QR format: {QRData}", request.QRData);
                    return response;
                }

                _logger.LogInformation("✅ QR PARSING - Parsed EventID: {EventID}, Seat: {Seat}, Name: {Name}, PaymentGUID: {PaymentGUID}",
                    response.QRData.EventID, response.QRData.SeatNumber, response.QRData.FirstName, response.QRData.PaymentGUID);

                // Step 2: Validate Event exists and is active
                var eventInfo = await GetEventInfoAsync(response.QRData.EventID);
                response.Event = eventInfo;

                if (eventInfo == null)
                {
                    response.IsValid = false;
                    response.Status = "NotFound";
                    response.Message = $"Event '{response.QRData.EventID}' not found";
                    _logger.LogWarning("❌ QR VALIDATION - Event not found: {EventID}", response.QRData.EventID);
                    return response;
                }

                // Step 3: Find ticket in database
                var ticketInfo = await GetTicketInfoAsync(response.QRData);
                response.Ticket = ticketInfo;

                if (ticketInfo == null)
                {
                    response.IsValid = false;
                    response.Status = "NotFound";
                    response.Message = "Ticket not found in database";
                    _logger.LogWarning("❌ QR VALIDATION - Ticket not found for PaymentGUID: {PaymentGUID}, Seat: {Seat}",
                        response.QRData.PaymentGUID, response.QRData.SeatNumber);
                    return response;
                }

                // Step 4: Check entry history
                var entryInfo = await GetEntryInfoAsync(request.QRData);
                response.Entry = entryInfo;

                // Step 5: Determine validation result
                if (ticketInfo.Status != "Active")
                {
                    response.IsValid = false;
                    response.Status = "Inactive";
                    response.Message = $"Ticket status is {ticketInfo.Status}";
                    _logger.LogWarning("❌ QR VALIDATION - Ticket inactive: Status={Status}", ticketInfo.Status);
                    return response;
                }

                if (eventInfo.Date.HasValue && eventInfo.Date.Value.Date < DateTime.Now.Date)
                {
                    response.IsValid = false;
                    response.Status = "Expired";
                    response.Message = "Event has already passed";
                    _logger.LogWarning("❌ QR VALIDATION - Event expired: EventDate={EventDate}", eventInfo.Date);
                    return response;
                }

                // Allow re-entry by default, but provide entry tracking info
                response.IsValid = true;
                response.Status = entryInfo.HasPreviousEntry ? "ValidReEntry" : "Valid";
                response.Message = entryInfo.HasPreviousEntry 
                    ? $"Valid ticket - Re-entry #{entryInfo.EntryCount + 1} (Last entry: {entryInfo.LastEntryTime:HH:mm})"
                    : "Valid ticket - First entry";

                _logger.LogInformation("✅ QR VALIDATION - Success: {Status}, Customer: {Customer}, Event: {Event}",
                    response.Status, ticketInfo.CustomerName, eventInfo.Title);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ QR VALIDATION - Exception during validation");
                response.IsValid = false;
                response.Status = "Error";
                response.Message = "Validation failed due to system error";
                return response;
            }
        }

        /// <summary>
        /// Parses QR data string into components
        /// Expected format: "EventID: {eventId}, Event: {eventName}, Seat: {seatNumber}, Name: {firstName}, ID: {paymentGuid}"
        /// </summary>
        public QRDataComponents ParseQRData(string qrData)
        {
            var components = new QRDataComponents
            {
                RawData = qrData,
                IsParsed = false
            };

            try
            {
                if (string.IsNullOrWhiteSpace(qrData))
                {
                    _logger.LogWarning("QR data is null or empty");
                    return components;
                }

                // Parse the expected format: "EventID: {eventId}, Event: {eventName}, Seat: {seatNumber}, Name: {firstName}, ID: {paymentGuid}"
                var parts = qrData.Split(',');
                if (parts.Length != 5)
                {
                    _logger.LogWarning("QR data does not have expected 5 parts: {PartCount}", parts.Length);
                    return components;
                }

                foreach (var part in parts)
                {
                    var keyValue = part.Trim().Split(':', 2);
                    if (keyValue.Length != 2) continue;

                    var key = keyValue[0].Trim();
                    var value = keyValue[1].Trim();

                    switch (key.ToUpper())
                    {
                        case "EVENTID":
                            components.EventID = value;
                            break;
                        case "EVENT":
                            components.EventName = value;
                            break;
                        case "SEAT":
                            components.SeatNumber = value;
                            break;
                        case "NAME":
                            components.FirstName = value;
                            break;
                        case "ID":
                            components.PaymentGUID = value;
                            break;
                    }
                }

                // Validate required fields are present
                components.IsParsed = !string.IsNullOrEmpty(components.EventID) &&
                                    !string.IsNullOrEmpty(components.EventName) &&
                                    !string.IsNullOrEmpty(components.SeatNumber) &&
                                    !string.IsNullOrEmpty(components.FirstName) &&
                                    !string.IsNullOrEmpty(components.PaymentGUID);

                _logger.LogDebug("QR parsing result: Parsed={IsParsed}, EventID={EventID}, Name={Name}",
                    components.IsParsed, components.EventID, components.FirstName);

                return components;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing QR data: {QRData}", qrData);
                return components;
            }
        }

        /// <summary>
        /// Records an entry attempt for audit trail
        /// </summary>
        public async Task LogQREntryAsync(QRValidationRequest request, QRValidationResponse response, string? ipAddress = null, string? userAgent = null)
        {
            try
            {
                var entryLog = new QREntryLog
                {
                    QRData = request.QRData,
                    EventID = response.QRData?.EventID,
                    PaymentGUID = response.QRData?.PaymentGUID,
                    SeatNumber = response.QRData?.SeatNumber,
                    AttendeeeName = response.QRData?.FirstName,
                    ScanTime = DateTime.UtcNow,
                    ScanLocation = request.ScanLocation,
                    ValidationResult = response.Status,
                    ScanNotes = request.ScanNotes,
                    IpAddress = ipAddress,
                    UserAgent = userAgent
                };

                _context.QREntryLogs.Add(entryLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation("📝 QR ENTRY LOG - Recorded: Event={EventID}, Status={Status}, Location={Location}",
                    entryLog.EventID, entryLog.ValidationResult, entryLog.ScanLocation);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging QR entry");
                // Don't throw - logging failure shouldn't break validation
            }
        }

        #region Private Helper Methods for Validation

        /// <summary>
        /// Gets event information from database
        /// </summary>
        private async Task<EventValidationInfo?> GetEventInfoAsync(string eventIdStr)
        {
            try
            {
                if (!int.TryParse(eventIdStr, out int eventId))
                {
                    _logger.LogWarning("Invalid event ID format: {EventID}", eventIdStr);
                    return null;
                }

                var eventEntity = await _context.Events
                    .Include(e => e.Organizer)
                    .Where(e => e.Id == eventId)
                    .Select(e => new EventValidationInfo
                    {
                        EventId = e.Id,
                        Title = e.Title,
                        Description = e.Description ?? "",
                        Date = e.Date,
                        Location = e.Location ?? "",
                        Status = e.Status.ToString(),
                        OrganizerName = e.Organizer != null ? e.Organizer.Name : "",
                        OrganizerEmail = e.Organizer != null ? e.Organizer.ContactEmail : "",
                        ImageUrl = e.ImageUrl ?? ""
                    })
                    .FirstOrDefaultAsync();

                return eventEntity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching event info for ID: {EventID}", eventIdStr);
                return null;
            }
        }

        /// <summary>
        /// Gets ticket information from database
        /// </summary>
        private async Task<TicketValidationInfo?> GetTicketInfoAsync(QRDataComponents qrData)
        {
            try
            {
                // First try to find in BookingLineItems (new system)
                var bookingLineItem = await _context.BookingLineItems
                    .Include(bli => bli.Booking)
                    .Where(bli => 
                        (bli.QRCode == qrData.PaymentGUID || bli.Booking.PaymentIntentId == qrData.PaymentGUID) &&
                        (bli.SeatDetails.Contains(qrData.SeatNumber) || bli.ItemName.Contains(qrData.SeatNumber)))
                    .FirstOrDefaultAsync();

                if (bookingLineItem != null)
                {
                    var ticketInfo = new TicketValidationInfo
                    {
                        BookingId = bookingLineItem.BookingId,
                        LineItemId = bookingLineItem.Id,
                        CustomerName = $"{bookingLineItem.Booking.CustomerFirstName} {bookingLineItem.Booking.CustomerLastName}".Trim(),
                        CustomerEmail = bookingLineItem.Booking.CustomerEmail,
                        SeatNumber = qrData.SeatNumber,
                        TicketType = bookingLineItem.ItemName,
                        Price = bookingLineItem.UnitPrice,
                        PaymentStatus = bookingLineItem.Booking.PaymentStatus,
                        BookingDate = bookingLineItem.CreatedAt,
                        QRCode = bookingLineItem.QRCode ?? "",
                        Status = bookingLineItem.Status ?? "Active"
                    };

                    // Get food orders for this ticket
                    var foodOrders = await _context.BookingLineItems
                        .Where(bli => bli.BookingId == bookingLineItem.BookingId && 
                                     bli.ItemType == "Food" && 
                                     (bli.SeatDetails.Contains(qrData.SeatNumber) || string.IsNullOrEmpty(bli.SeatDetails)))
                        .Select(bli => new FoodOrderInfo
                        {
                            Name = bli.ItemName,
                            Quantity = bli.Quantity,
                            UnitPrice = bli.UnitPrice,
                            TotalPrice = bli.TotalPrice,
                            Description = bli.ItemDetails,
                            SeatAssignment = bli.SeatDetails
                        })
                        .ToListAsync();

                    ticketInfo.FoodOrders = foodOrders;
                    return ticketInfo;
                }

                // Fallback: try to find in EventBookings (legacy system)
                var eventBooking = await _context.EventBookings
                    .Where(eb => eb.PaymentGUID == qrData.PaymentGUID && eb.SeatNo == qrData.SeatNumber)
                    .FirstOrDefaultAsync();

                if (eventBooking != null)
                {
                    return new TicketValidationInfo
                    {
                        CustomerName = eventBooking.FirstName,
                        CustomerEmail = eventBooking.BuyerEmail,
                        SeatNumber = eventBooking.SeatNo,
                        TicketType = "Standard",
                        PaymentStatus = "Completed",
                        BookingDate = eventBooking.CreatedAt,
                        QRCode = eventBooking.PaymentGUID,
                        Status = "Active"
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching ticket info for PaymentGUID: {PaymentGUID}", qrData.PaymentGUID);
                return null;
            }
        }

        /// <summary>
        /// Gets entry history for this ticket
        /// </summary>
        private async Task<EntryValidationInfo> GetEntryInfoAsync(string qrData)
        {
            try
            {
                var entryLogs = await _context.QREntryLogs
                    .Where(log => log.QRData == qrData && log.ValidationResult == "Valid" || log.ValidationResult == "ValidReEntry")
                    .OrderBy(log => log.ScanTime)
                    .ToListAsync();

                return new EntryValidationInfo
                {
                    HasPreviousEntry = entryLogs.Any(),
                    FirstEntryTime = entryLogs.FirstOrDefault()?.ScanTime,
                    LastEntryTime = entryLogs.LastOrDefault()?.ScanTime,
                    EntryCount = entryLogs.Count,
                    LastScanLocation = entryLogs.LastOrDefault()?.ScanLocation,
                    AllowReEntry = true // Configure this based on business rules
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching entry info for QR data");
                return new EntryValidationInfo { AllowReEntry = true };
            }
        }

        #endregion

        #endregion
    }
}
