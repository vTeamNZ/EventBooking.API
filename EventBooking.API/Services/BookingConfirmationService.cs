using EventBooking.API.Data;
using EventBooking.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Stripe;
using Stripe.Checkout;

namespace EventBooking.API.Services
{
    public interface IBookingConfirmationService
    {
        Task<BookingConfirmationResult> ProcessPaymentSuccessAsync(string sessionId, string paymentIntentId);
    }

    public class BookingConfirmationService : IBookingConfirmationService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<BookingConfirmationService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public BookingConfirmationService(
            AppDbContext context,
            ILogger<BookingConfirmationService> logger,
            IConfiguration configuration,
            HttpClient httpClient)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _httpClient = httpClient;
        }

        public async Task<BookingConfirmationResult> ProcessPaymentSuccessAsync(string sessionId, string paymentIntentId)
        {
            var result = new BookingConfirmationResult();
            
            try
            {
                _logger.LogInformation("Processing payment success for session: {SessionId}", sessionId);

                // Get session from Stripe to extract metadata
                var sessionService = new Stripe.Checkout.SessionService();
                var session = await sessionService.GetAsync(sessionId);

                if (session.PaymentStatus != "paid")
                {
                    _logger.LogWarning("Payment not successful for session: {SessionId}", sessionId);
                    result.Success = false;
                    result.ErrorMessage = "Payment status is not paid";
                    return result;
                }

                // Extract metadata
                session.Metadata.TryGetValue("eventId", out var eventIdStr);
                session.Metadata.TryGetValue("eventTitle", out var eventTitle);
                session.Metadata.TryGetValue("ticketDetails", out var ticketDetailsJson);
                session.Metadata.TryGetValue("customerFirstName", out var firstName);
                session.Metadata.TryGetValue("customerLastName", out var lastName);
                session.Metadata.TryGetValue("customerMobile", out var mobile);

                if (!int.TryParse(eventIdStr, out var eventId))
                {
                    _logger.LogError("Invalid event ID in session metadata: {EventId}", eventIdStr);
                    result.Success = false;
                    result.ErrorMessage = "Invalid event ID in metadata";
                    return result;
                }

                // Get event details
                var eventEntity = await _context.Events
                    .Include(e => e.Organizer)
                    .FirstOrDefaultAsync(e => e.Id == eventId);

                if (eventEntity == null)
                {
                    _logger.LogError("Event not found: {EventId}", eventId);
                    result.Success = false;
                    result.ErrorMessage = "Event not found";
                    return result;
                }

                // Handle seat processing based on event type
                List<string> selectedSeats = new List<string>();
                
                if (eventEntity.SeatSelectionMode == SeatSelectionMode.EventHall)
                {
                    // For allocated seating events, get selected seats from metadata
                    session.Metadata.TryGetValue("selectedSeats", out var selectedSeatsString);
                    
                    _logger.LogInformation("ALLOCATED SEATING - Selected seats string from metadata: {SelectedSeats}", selectedSeatsString);
                    
                    try
                    {
                        if (!string.IsNullOrEmpty(selectedSeatsString))
                        {
                            // Split by semicolon since seats are stored using string.Join(";", request.SelectedSeats)
                            var seatsArray = selectedSeatsString.Split(';', StringSplitOptions.RemoveEmptyEntries);
                            selectedSeats = seatsArray.ToList();
                        }
                            
                        if (selectedSeats.Any())
                        {
                            _logger.LogInformation("ALLOCATED SEATING - Successfully parsed {Count} selected seats: {Seats}", 
                                selectedSeats.Count, string.Join(", ", selectedSeats));
                        }
                        else
                        {
                            _logger.LogError("ALLOCATED SEATING - No seats found in metadata for allocated seating event");
                            result.Success = false;
                            result.ErrorMessage = "No seats selected for allocated seating event";
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "ALLOCATED SEATING - Error parsing selected seats: {SeatsString}", selectedSeatsString);
                        result.Success = false;
                        result.ErrorMessage = "Error processing seat selection";
                        return result;
                    }
                }
                else if (eventEntity.SeatSelectionMode == SeatSelectionMode.GeneralAdmission)
                {
                    // For general admission events, no specific seats are needed
                    _logger.LogInformation("GENERAL ADMISSION - Processing general admission booking (no specific seats)");
                    selectedSeats = new List<string>(); // Empty list is fine for general admission
                }
                else
                {
                    _logger.LogError("Unknown seat selection mode: {Mode}", eventEntity.SeatSelectionMode);
                    result.Success = false;
                    result.ErrorMessage = "Unknown event seating configuration";
                    return result;
                }

                // Parse ticket details
                var ticketDetails = new List<Dictionary<string, object>>();
                
                try 
                {
                    if (!string.IsNullOrEmpty(ticketDetailsJson))
                    {
                        ticketDetails = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(ticketDetailsJson);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deserializing ticket details JSON: {TicketDetails}", ticketDetailsJson);
                }

                // Create one booking record for all seats
                var booking = new Booking
                {
                    EventId = eventId,
                    CreatedAt = DateTime.UtcNow,
                    TotalAmount = (decimal)(session.AmountTotal ?? 0) / 100 // Convert from cents
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                // Process ticket types
                if (ticketDetails != null && ticketDetails.Any())
                {
                    foreach (var ticket in ticketDetails)
                    {
                        if (ticket.TryGetValue("Type", out var typeObj) && typeObj != null)
                        {
                            var type = typeObj.ToString();
                            var ticketType = await _context.TicketTypes
                                .FirstOrDefaultAsync(tt => tt.EventId == eventId && tt.Type == type);

                            if (ticketType != null && ticket.TryGetValue("Quantity", out var quantityObj) && quantityObj != null)
                            {
                                int quantity;
                                if (int.TryParse(quantityObj.ToString(), out quantity))
                                {
                                    var bookingTicket = new BookingTicket
                                    {
                                        BookingId = booking.Id,
                                        TicketTypeId = ticketType.Id,
                                        Quantity = quantity
                                    };

                                    _context.BookingTickets.Add(bookingTicket);
                                }
                            }
                        }
                    }
                }

                // Handle QR generation based on event type
                var qrResults = new List<QRGenerationResult>();
                
                if (eventEntity.SeatSelectionMode == SeatSelectionMode.EventHall && selectedSeats.Any())
                {
                    // ALLOCATED SEATING: Update seat status and generate QR per seat
                    _logger.LogInformation("ALLOCATED SEATING - Processing {Count} specific seats", selectedSeats.Count);
                    
                    // Get all selected seats in one query for efficiency
                    var seats = await _context.Seats
                        .Where(s => s.EventId == eventId && selectedSeats.Contains(s.SeatNumber))
                        .ToListAsync();

                    _logger.LogInformation("Found {SeatsCount} seats matching selected seat numbers", seats.Count);

                    if (seats.Count != selectedSeats.Count)
                    {
                        _logger.LogWarning(
                            "Not all selected seats were found. Selected: {SelectedCount}, Found: {FoundCount}",
                            selectedSeats.Count, seats.Count);
                    }

                    // Update seat statuses to booked
                    foreach (var seat in seats)
                    {
                        if (seat.Status != SeatStatus.Reserved && seat.Status != SeatStatus.Available)
                        {
                            _logger.LogWarning("Seat {SeatNumber} is not available or reserved, status: {Status}",
                                seat.SeatNumber, seat.Status);
                            continue;
                        }

                        // Update seat status to booked
                        seat.Status = SeatStatus.Booked;
                        seat.ReservedBy = session.CustomerEmail;
                        seat.ReservedUntil = DateTime.UtcNow.AddDays(1); // Set expiry for cleanup if needed
                        
                        _logger.LogInformation("Updated seat {SeatNumber} to Booked status", seat.SeatNumber);
                    }
                    
                    // Save all seat updates
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Updated {Count} seats to Booked status", seats.Count);
                    
                    // Generate QR code for each seat
                    foreach (var seatNumber in selectedSeats)
                    {
                        try 
                        {
                            var qrResult = await CallQRCodeGeneratorAPI(
                                eventId, 
                                eventTitle ?? eventEntity.Title, 
                                seatNumber, 
                                firstName ?? "Guest", 
                                paymentIntentId, 
                                session.CustomerEmail ?? "", 
                                eventEntity.Organizer?.ContactEmail ?? ""
                            );
                            
                            qrResults.Add(new QRGenerationResult 
                            {
                                SeatNumber = seatNumber,
                                Success = qrResult.Success,
                                TicketPath = qrResult.TicketPath,
                                BookingId = qrResult.BookingId,
                                ErrorMessage = qrResult.ErrorMessage
                            });
                        }
                        catch (Exception qrEx)
                        {
                            _logger.LogError(qrEx, "QR generation failed for seat {Seat}", seatNumber);
                            qrResults.Add(new QRGenerationResult 
                            {
                                SeatNumber = seatNumber,
                                Success = false,
                                ErrorMessage = qrEx.Message
                            });
                        }
                    }
                }
                else if (eventEntity.SeatSelectionMode == SeatSelectionMode.GeneralAdmission)
                {
                    // GENERAL ADMISSION: Generate QR tickets based on ticket quantity
                    _logger.LogInformation("GENERAL ADMISSION - Processing general admission tickets");
                    
                    // For general admission, generate QR tickets based on ticket details
                    if (ticketDetails != null && ticketDetails.Any())
                    {
                        foreach (var ticket in ticketDetails)
                        {
                            if (ticket.TryGetValue("Type", out var typeObj) && 
                                ticket.TryGetValue("Quantity", out var quantityObj) && 
                                typeObj != null && quantityObj != null)
                            {
                                var ticketType = typeObj.ToString();
                                if (int.TryParse(quantityObj.ToString(), out var quantity))
                                {
                                    _logger.LogInformation("GENERAL ADMISSION - Generating {Quantity} tickets for type: {Type}", quantity, ticketType);
                                    
                                    // Generate QR code for each ticket quantity
                                    for (int i = 1; i <= quantity; i++)
                                    {
                                        try 
                                        {
                                            var ticketIdentifier = $"{ticketType}-{i}";
                                            var qrResult = await CallQRCodeGeneratorAPI(
                                                eventId, 
                                                eventTitle ?? eventEntity.Title, 
                                                ticketIdentifier, // Use ticket type + number instead of seat
                                                firstName ?? "Guest", 
                                                paymentIntentId, 
                                                session.CustomerEmail ?? "", 
                                                eventEntity.Organizer?.ContactEmail ?? ""
                                            );
                                            
                                            qrResults.Add(new QRGenerationResult 
                                            {
                                                SeatNumber = ticketIdentifier, // Will contain ticket type info
                                                Success = qrResult.Success,
                                                TicketPath = qrResult.TicketPath,
                                                BookingId = qrResult.BookingId,
                                                ErrorMessage = qrResult.ErrorMessage
                                            });
                                        }
                                        catch (Exception qrEx)
                                        {
                                            _logger.LogError(qrEx, "QR generation failed for general admission ticket {Type}-{Number}", ticketType, i);
                                            qrResults.Add(new QRGenerationResult 
                                            {
                                                SeatNumber = $"{ticketType}-{i}",
                                                Success = false,
                                                ErrorMessage = qrEx.Message
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("GENERAL ADMISSION - No ticket details found for general admission event");
                    }
                }

                await _context.SaveChangesAsync();
                
                // ✅ Build successful result with all data
                result.Success = true;
                result.EventTitle = eventTitle ?? eventEntity.Title;
                result.CustomerName = $"{firstName} {lastName}".Trim();
                result.CustomerEmail = session.CustomerEmail ?? "";
                
                // Set booked seats based on event type
                if (eventEntity.SeatSelectionMode == SeatSelectionMode.EventHall)
                {
                    result.BookedSeats = selectedSeats; // Actual seat numbers for allocated seating
                }
                else if (eventEntity.SeatSelectionMode == SeatSelectionMode.GeneralAdmission)
                {
                    // For general admission, list the ticket identifiers
                    result.BookedSeats = qrResults.Select(qr => qr.SeatNumber).ToList();
                }
                else
                {
                    result.BookedSeats = new List<string>();
                }
                
                result.AmountTotal = (decimal)(session.AmountTotal ?? 0) / 100;
                result.TicketReference = paymentIntentId?.Replace("pi_", "") ?? "";
                result.QRResults = qrResults;
                result.BookingId = booking.Id;
                
                _logger.LogInformation("Payment success processed for session: {SessionId}, booking ID: {BookingId}, event type: {EventType}", 
                    sessionId, booking.Id, eventEntity.SeatSelectionMode);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment success for session: {SessionId}", sessionId);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        // ✅ Enhanced QR API call with proper result handling
        private async Task<QRApiResult> CallQRCodeGeneratorAPI(
            int eventId, 
            string eventName, 
            string seatNumber, 
            string firstName, 
            string paymentGuid, 
            string buyerEmail, 
            string organizerEmail)
        {
            try
            {
                var qrApiUrl = _configuration["QRCodeGeneratorAPI:BaseUrl"]?.TrimEnd('/');
                if (string.IsNullOrEmpty(qrApiUrl))
                {
                    _logger.LogWarning("QR Code Generator API URL not configured");
                    return new QRApiResult 
                    { 
                        Success = false, 
                        ErrorMessage = "QR API URL not configured" 
                    };
                }

                var eTicketRequest = new
                {
                    EventID = eventId.ToString(),
                    EventName = eventName,
                    SeatNo = seatNumber,
                    FirstName = firstName,
                    PaymentGUID = paymentGuid,
                    BuyerEmail = buyerEmail,
                    OrganizerEmail = organizerEmail
                };

                _logger.LogInformation("Calling QR API for seat {Seat} with data: {@Request}", 
                    seatNumber, eTicketRequest);

                var response = await _httpClient.PostAsJsonAsync(
                    $"{qrApiUrl}/etickets/generate", 
                    eTicketRequest
                );

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var qrResult = JsonSerializer.Deserialize<QRApiResponse>(responseContent);
                    
                    _logger.LogInformation("QR API success for seat {Seat}: {@Result}", seatNumber, qrResult);
                    
                    return new QRApiResult 
                    {
                        Success = true,
                        TicketPath = qrResult?.TicketPath,
                        BookingId = qrResult?.BookingId
                    };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("QR API call failed for seat {Seat} with status {Status}: {Error}", 
                        seatNumber, response.StatusCode, errorContent);
                        
                    return new QRApiResult 
                    {
                        Success = false,
                        ErrorMessage = $"QR API failed: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception calling QR API for seat {Seat}", seatNumber);
                return new QRApiResult 
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }

    public class TicketLineItem
    {
        public string Type { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
