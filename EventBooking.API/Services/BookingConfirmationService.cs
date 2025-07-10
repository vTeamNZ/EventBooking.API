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
        Task<bool> ProcessPaymentSuccessAsync(string sessionId, string paymentIntentId);
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

        public async Task<bool> ProcessPaymentSuccessAsync(string sessionId, string paymentIntentId)
        {
            try
            {
                _logger.LogInformation("Processing payment success for session: {SessionId}", sessionId);

                // Get session from Stripe to extract metadata
                var sessionService = new Stripe.Checkout.SessionService();
                var session = await sessionService.GetAsync(sessionId);

                if (session.PaymentStatus != "paid")
                {
                    _logger.LogWarning("Payment not successful for session: {SessionId}", sessionId);
                    return false;
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
                    return false;
                }

                // Get event details
                var eventEntity = await _context.Events
                    .Include(e => e.Organizer)
                    .FirstOrDefaultAsync(e => e.Id == eventId);

                if (eventEntity == null)
                {
                    _logger.LogError("Event not found: {EventId}", eventId);
                    return false;
                }

                // Get selected seats from metadata - THIS IS THE CRITICAL PART
                session.Metadata.TryGetValue("selectedSeats", out var selectedSeatsString);
                
                _logger.LogInformation("SEAT BOOKING - Selected seats string from metadata: {SelectedSeats}", selectedSeatsString);
                
                List<string> selectedSeats;
                try
                {
                    if (string.IsNullOrEmpty(selectedSeatsString))
                    {
                        selectedSeats = new List<string>();
                    }
                    else
                    {
                        // Split by semicolon since seats are stored using string.Join(";", request.SelectedSeats)
                        var seatsArray = selectedSeatsString.Split(';', StringSplitOptions.RemoveEmptyEntries);
                        selectedSeats = seatsArray.ToList();
                    }
                        
                    if (selectedSeats != null && selectedSeats.Any())
                    {
                        _logger.LogInformation("SEAT BOOKING - Successfully parsed {Count} selected seats: {Seats}", 
                            selectedSeats.Count, string.Join(", ", selectedSeats));
                    }
                    else
                    {
                        _logger.LogWarning("SEAT BOOKING - No seats parsed from metadata string");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SEAT BOOKING - Error parsing selected seats string: {SeatsString}", selectedSeatsString);
                    return false;
                }

                if (selectedSeats == null || !selectedSeats.Any())
                {
                    _logger.LogWarning("No selected seats found in session metadata");
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

                // If no seats found in selectedSeats, try to extract from ticketDetails
                if ((selectedSeats == null || !selectedSeats.Any()) && ticketDetails != null && ticketDetails.Any())
                {
                    _logger.LogInformation("SEAT BOOKING - No seats in selectedSeats, attempting to extract from ticketDetails");
                    
                    selectedSeats = new List<string>();
                    foreach (var ticket in ticketDetails)
                    {
                        if (ticket.TryGetValue("Type", out var typeObj) && typeObj != null)
                        {
                            var type = typeObj.ToString();
                            _logger.LogInformation("SEAT BOOKING - Processing ticket type: {Type}", type);
                            
                            // Check if this is a seat ticket (multiple possible formats)
                            if (type?.Contains("Seat") == true)
                            {
                                // Handle format: "Seat (K1)" - single seat
                                if (type.StartsWith("Seat (") && type.EndsWith(")"))
                                {
                                    var seatNumber = type.Substring(6, type.Length - 7); // Remove "Seat (" and ")"
                                    selectedSeats.Add(seatNumber);
                                    _logger.LogInformation("SEAT BOOKING - Extracted single seat {SeatNumber} from ticketDetails", seatNumber);
                                }
                                // Handle format: "Seats(M10,M13)" - multiple seats
                                else if (type.StartsWith("Seats(") && type.EndsWith(")"))
                                {
                                    var seatsString = type.Substring(6, type.Length - 7); // Remove "Seats(" and ")"
                                    var seatNumbers = seatsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim())
                                        .ToList();
                                    selectedSeats.AddRange(seatNumbers);
                                    _logger.LogInformation("SEAT BOOKING - Extracted multiple seats {SeatNumbers} from ticketDetails", string.Join(", ", seatNumbers));
                                }
                                // Handle format: "Seats (M10, M13)" - multiple seats with space
                                else if (type.StartsWith("Seats (") && type.EndsWith(")"))
                                {
                                    var seatsString = type.Substring(7, type.Length - 8); // Remove "Seats (" and ")"
                                    var seatNumbers = seatsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim())
                                        .ToList();
                                    selectedSeats.AddRange(seatNumbers);
                                    _logger.LogInformation("SEAT BOOKING - Extracted multiple seats with space {SeatNumbers} from ticketDetails", string.Join(", ", seatNumbers));
                                }
                            }
                        }
                    }
                    
                    _logger.LogInformation("SEAT BOOKING - Extracted {Count} seats from ticketDetails: {Seats}", 
                        selectedSeats.Count, string.Join(", ", selectedSeats));
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

                // Update seat status for each selected seat
                if (selectedSeats != null && selectedSeats.Any())
                {
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
                    
                    // Call QR Code Generator API to handle EventBookings table and email generation
                    await CallQRCodeGeneratorAPI(eventId, eventTitle ?? eventEntity.Title, selectedSeats, firstName ?? "Guest", paymentIntentId, session.CustomerEmail ?? "", eventEntity.Organizer?.ContactEmail ?? "");
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Payment success processed for session: {SessionId}", sessionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment success for session: {SessionId}", sessionId);
                return false;
            }
        }

        private async Task CallQRCodeGeneratorAPI(int eventId, string eventName, List<string> seatNumbers, string firstName, string paymentGuid, string buyerEmail, string organizerEmail)
        {
            var maxRetries = 3;
            var currentRetry = 0;
            var baseDelay = TimeSpan.FromSeconds(1);
            
            _logger.LogInformation("Calling QR Code API for event {EventId} with {Count} seats", eventId, seatNumbers.Count);

            while (currentRetry < maxRetries)
            {
                try
                {
                    var qrApiUrl = _configuration["QRCodeGeneratorAPI:BaseUrl"]?.TrimEnd('/');
                    if (string.IsNullOrEmpty(qrApiUrl))
                    {
                        _logger.LogWarning("QR Code Generator API URL not configured");
                        return;
                    }

                    foreach (var seatNumber in seatNumbers)
                    {
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
    
                        var jsonContent = JsonSerializer.Serialize(eTicketRequest);
                        var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");
    
                        _logger.LogInformation("Sending request to QR Code API for seat {SeatNumber}", seatNumber);
                        var response = await _httpClient.PostAsync($"{qrApiUrl}/api/etickets/generate", content);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var result = await response.Content.ReadFromJsonAsync<ETicketResponse>();
                            if (result?.TicketPath != null)
                            {
                                _logger.LogInformation("QR Code generated successfully for seat: {SeatNumber}, TicketPath: {TicketPath}", seatNumber, result.TicketPath);
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Failed to generate QR Code for seat: {SeatNumber}. Status: {StatusCode}", 
                                seatNumber, response.StatusCode);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calling QR Code Generator API for seats, attempt {Attempt}", currentRetry + 1);
                }

                currentRetry++;
                if (currentRetry < maxRetries)
                {
                    await Task.Delay(baseDelay * (1 << currentRetry)); // Exponential backoff
                }
            }
        }

        private class ETicketResponse
        {
            public string? TicketPath { get; set; }
        }
    }

    public class TicketLineItem
    {
        public string Type { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
}
