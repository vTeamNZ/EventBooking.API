using EventBooking.API.Data;
using EventBooking.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using EventBooking.API.DTOs;

namespace EventBooking.API.Services
{
    public interface IOrganizerStripeService
    {
        Task<OrganizerStripeData> GetOptimizedStripeDataAsync(int eventId);
        Task<(List<BookingDetailViewDTO> bookings, int totalCount)> GetStripeBookingsWithCountAsync(int eventId, int page = 1, int pageSize = 50, string? search = null);
        Task<List<BookingDetailViewDTO>> GetStripeBookingsAsync(int eventId, int page = 1, int pageSize = 50, string? search = null);
        Task<List<string>> GetStripeSeatNumbersAsync(int eventId);
    }

    public class OrganizerStripeService : IOrganizerStripeService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<OrganizerStripeService> _logger;
        private readonly IConfiguration _configuration;

        public OrganizerStripeService(AppDbContext context, ILogger<OrganizerStripeService> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<OrganizerStripeData> GetOptimizedStripeDataAsync(int eventId)
        {
            var result = new OrganizerStripeData
            {
                EventId = eventId,
                Bookings = new List<BookingDetailViewDTO>(),
                SeatNumbers = new List<string>(),
                TotalSessions = 0,
                RelevantSessions = 0
            };

            try
            {
                var eventItem = await _context.Events.FindAsync(eventId);
                if (eventItem == null)
                {
                    return result;
                }

                result.EventTitle = eventItem.Title;

                var stripeSecretKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? 
                                    _configuration["Stripe:SecretKey"];

                if (string.IsNullOrEmpty(stripeSecretKey))
                {
                    return result;
                }

                DateTime? firstBookingDate = null;
                try
                {
                    var firstBooking = await _context.Bookings
                        .Where(b => b.EventId == eventId)
                        .MinAsync(b => (DateTime?)b.CreatedAt);
                    
                    if (firstBooking.HasValue)
                    {
                        firstBookingDate = firstBooking.Value.Date;
                    }
                }
                catch
                {
                    // If no bookings found, firstBookingDate remains null
                }

                Stripe.StripeConfiguration.ApiKey = stripeSecretKey;
                var sessionService = new Stripe.Checkout.SessionService();

                var allSessions = new List<Stripe.Checkout.Session>();
                var hasMore = true;
                string? startingAfter = null;
                const int maxSessionsFallback = 1000;

                while (hasMore)
                {
                    var options = new Stripe.Checkout.SessionListOptions
                    {
                        Limit = 100,
                        StartingAfter = startingAfter
                    };

                    if (firstBookingDate.HasValue)
                    {
                        options.Created = new Stripe.DateRangeOptions
                        {
                            GreaterThanOrEqual = firstBookingDate.Value
                        };
                    }

                    var sessionList = await sessionService.ListAsync(options);
                    allSessions.AddRange(sessionList.Data);

                    hasMore = sessionList.HasMore;
                    if (hasMore && sessionList.Data.Any())
                    {
                        startingAfter = sessionList.Data.Last().Id;
                        
                        if (!firstBookingDate.HasValue && allSessions.Count >= maxSessionsFallback)
                        {
                            break;
                        }
                    }
                    else
                    {
                        hasMore = false;
                    }
                }

                result.TotalSessions = allSessions.Count;

                var eventTitle = eventItem.Title;
                var relevantSessions = allSessions
                    .Where(s => s.Metadata != null && 
                               s.Metadata.ContainsKey("eventTitle") && 
                               s.Metadata["eventTitle"].Equals(eventTitle, StringComparison.OrdinalIgnoreCase) &&
                               s.PaymentStatus == "paid")
                    .ToList();

                result.RelevantSessions = relevantSessions.Count;

                var seatNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var bookings = new List<BookingDetailViewDTO>();

                foreach (var session in relevantSessions)
                {
                    var booking = MapStripeSessionToBookingDetail(session);
                    bookings.Add(booking);

                    // Extract seat numbers from selectedSeats metadata (semicolon-separated)
                    if (session.Metadata != null && session.Metadata.ContainsKey("selectedSeats"))
                    {
                        var selectedSeats = session.Metadata["selectedSeats"];
                        if (!string.IsNullOrEmpty(selectedSeats))
                        {
                            var seats = selectedSeats.Split(';', StringSplitOptions.RemoveEmptyEntries);
                            foreach (var seat in seats)
                            {
                                if (!string.IsNullOrEmpty(seat.Trim()))
                                {
                                    seatNumbers.Add(seat.Trim());
                                }
                            }
                        }
                    }
                }

                result.Bookings = bookings.OrderByDescending(b => b.BookedTime).ToList();
                result.SeatNumbers = seatNumbers.OrderBy(s => s).ToList();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting optimized Stripe data for EventId={EventId}", eventId);
                return result;
            }
        }

        public async Task<(List<BookingDetailViewDTO> bookings, int totalCount)> GetStripeBookingsWithCountAsync(int eventId, int page = 1, int pageSize = 50, string? search = null)
        {
            var stripeData = await GetOptimizedStripeDataAsync(eventId);
            var allBookings = stripeData.Bookings;
            
            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                allBookings = allBookings.Where(booking =>
                    (!string.IsNullOrEmpty(booking.FirstName) && booking.FirstName.ToLower().Contains(searchLower)) ||
                    (!string.IsNullOrEmpty(booking.LastName) && booking.LastName.ToLower().Contains(searchLower)) ||
                    (!string.IsNullOrEmpty(booking.Email) && booking.Email.ToLower().Contains(searchLower)) ||
                    (!string.IsNullOrEmpty(booking.PaymentId) && booking.PaymentId.ToLower().Contains(searchLower)))
                    .ToList();
            }

            var totalCount = allBookings.Count;
            var paginatedBookings = allBookings
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return (paginatedBookings, totalCount);
        }

        public async Task<List<BookingDetailViewDTO>> GetStripeBookingsAsync(int eventId, int page = 1, int pageSize = 50, string? search = null)
        {
            var stripeData = await GetOptimizedStripeDataAsync(eventId);
            var allBookings = stripeData.Bookings;
            
            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                allBookings = allBookings.Where(booking =>
                    (!string.IsNullOrEmpty(booking.FirstName) && booking.FirstName.ToLower().Contains(searchLower)) ||
                    (!string.IsNullOrEmpty(booking.LastName) && booking.LastName.ToLower().Contains(searchLower)) ||
                    (!string.IsNullOrEmpty(booking.Email) && booking.Email.ToLower().Contains(searchLower)) ||
                    (!string.IsNullOrEmpty(booking.PaymentId) && booking.PaymentId.ToLower().Contains(searchLower)))
                    .ToList();
            }

            var paginatedBookings = allBookings
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return paginatedBookings;
        }

        public async Task<List<string>> GetStripeSeatNumbersAsync(int eventId)
        {
            var stripeData = await GetOptimizedStripeDataAsync(eventId);
            return stripeData.SeatNumbers;
        }

        private BookingDetailViewDTO MapStripeSessionToBookingDetail(Stripe.Checkout.Session session)
        {
            // Extract customer information from metadata (using correct keys from original implementation)
            session.Metadata.TryGetValue("customerFirstName", out var firstName);
            session.Metadata.TryGetValue("customerLastName", out var lastName);
            session.Metadata.TryGetValue("customerMobile", out var mobile);
            session.Metadata.TryGetValue("ticketDetails", out var ticketDetailsJson);
            session.Metadata.TryGetValue("selectedSeats", out var selectedSeats);

            var email = session.CustomerEmail ?? session.Metadata.GetValueOrDefault("customerEmail", "");

            // Parse ticket details from JSON metadata
            var ticketDetails = new List<TicketTypeDetailDTO>();
            var totalTickets = 0;

            if (!string.IsNullOrEmpty(ticketDetailsJson))
            {
                try
                {
                    using var document = JsonDocument.Parse(ticketDetailsJson);
                    var ticketArray = document.RootElement;

                    if (ticketArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var ticketElement in ticketArray.EnumerateArray())
                        {
                            var typeName = ticketElement.TryGetProperty("Type", out var typeProperty) 
                                ? typeProperty.GetString() ?? "Unknown" 
                                : "Unknown";
                            
                            var quantity = ticketElement.TryGetProperty("Quantity", out var quantityProperty) 
                                ? quantityProperty.GetInt32() 
                                : 0;
                            
                            var unitPrice = ticketElement.TryGetProperty("UnitPrice", out var priceProperty) 
                                ? priceProperty.GetDecimal() 
                                : 0m;

                            totalTickets += quantity;

                            ticketDetails.Add(new TicketTypeDetailDTO
                            {
                                TicketTypeName = typeName,
                                Quantity = quantity,
                                UnitPrice = unitPrice,
                                SeatInfo = "" // Seats will be extracted separately
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse ticket details from Stripe session {SessionId}", session.Id);
                }
            }

            // Extract seat information from selectedSeats metadata
            var seatInfo = "";
            if (!string.IsNullOrEmpty(selectedSeats))
            {
                var seats = selectedSeats.Split(';', StringSplitOptions.RemoveEmptyEntries);
                seatInfo = string.Join(", ", seats);
            }

            // If we have seat info and ticket details, add it to the first ticket type
            if (!string.IsNullOrEmpty(seatInfo) && ticketDetails.Any())
            {
                ticketDetails[0].SeatInfo = seatInfo;
            }

            // Fallback for customer name if not in metadata
            if (string.IsNullOrEmpty(firstName) && string.IsNullOrEmpty(lastName))
            {
                var customerName = session.CustomerDetails?.Name ?? session.Metadata?.GetValueOrDefault("customerName") ?? "";
                if (!string.IsNullOrEmpty(customerName))
                {
                    var nameParts = customerName.Split(' ', 2);
                    firstName = nameParts[0];
                    lastName = nameParts.Length > 1 ? nameParts[1] : "";
                }
            }

            return new BookingDetailViewDTO
            {
                BookingId = 0, // Stripe sessions don't have booking IDs
                PaymentId = session.PaymentIntentId ?? session.Id,
                FirstName = firstName ?? "",
                LastName = lastName ?? "",
                Email = HashEmail(email), // Hash for privacy
                Mobile = mobile ?? "",
                BookedTime = session.Created,
                PaymentStatus = "Paid",
                TotalAmount = (decimal)(session.AmountTotal ?? 0) / 100, // Convert from cents
                TotalTickets = totalTickets > 0 ? totalTickets : 1,
                TicketDetails = ticketDetails,
                IsPaid = true,
                IsOrganizerBooking = false
            };
        }

        private static string HashEmail(string email)
        {
            try
            {
                if (string.IsNullOrEmpty(email))
                    return "";

                // Find the @ symbol
                var atIndex = email.IndexOf('@');
                if (atIndex <= 0)
                    return email; // Return original if no @ found or @ is at the beginning

                var localPart = email.Substring(0, atIndex);
                var domainPart = email.Substring(atIndex);

                // If local part is too short, just return the original
                if (localPart.Length <= 4)
                    return email;

                // Take first 2 and last 2 characters of local part
                var firstTwo = localPart.Substring(0, 2);
                var lastTwo = localPart.Substring(localPart.Length - 2);
                
                // Create asterisks for the middle part (minimum 5 asterisks)
                var middleLength = Math.Max(5, localPart.Length - 4);
                var asterisks = new string('*', middleLength);

                return $"{firstTwo}{asterisks}{lastTwo}{domainPart}";
            }
            catch
            {
                // Return original email if any error occurs
                return email;
            }
        }
    }

    public class OrganizerStripeData
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = "";
        public List<BookingDetailViewDTO> Bookings { get; set; } = new();
        public List<string> SeatNumbers { get; set; } = new();
        public int TotalSessions { get; set; }
        public int RelevantSessions { get; set; }
    }
}
