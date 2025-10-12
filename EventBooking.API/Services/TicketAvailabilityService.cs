using EventBooking.API.Data;
using EventBooking.API.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

/// <summary>
/// 🎯 TICKET AVAILABILITY SERVICE v6 - FIXED DOUBLE COUNTING ISSUE
/// 
/// PROBLEM FIXED: Previous version was double-counting organizer tickets
/// - BookingLineItems contained BOTH Stripe and Organizer bookings
/// - OrganizerTicketPayments contained ONLY Organizer tickets  
/// - Adding both = Stripe + Organizer + Organizer (DOUBLE COUNT!)
/// 
/// SOLUTION: Separate calculation using Dashboard Tab logic
/// - Stripe tickets: From Stripe API (matches Dashboard Tab 02)
/// - Organizer tickets: From OrganizerTicketPayments table (matches Dashboard Tab 03)
/// - Total = Stripe + Organizer (NO DOUBLE COUNTING)
/// 
/// This ensures accurate ticket availability for General Admission and Hybrid events.
/// </summary>

namespace EventBooking.API.Services
{
    public interface ITicketAvailabilityService
    {
        Task<int> GetTicketsSoldAsync(int ticketTypeId);
        Task<int> GetStripeTicketsSoldAsync(int ticketTypeId);
        Task<int> GetOrganizerTicketsSoldAsync(int ticketTypeId);
        Task<int> GetTicketsAvailableAsync(int ticketTypeId);
        Task<bool> IsTicketTypeAvailableAsync(int ticketTypeId, int requestedQuantity);
        Task<Dictionary<int, int>> GetTicketAvailabilityForEventAsync(int eventId);
        Task<Dictionary<int, TicketAvailabilityDetail>> GetEventTicketAvailabilityOptimizedAsync(int eventId);
        Task<Dictionary<int, int>> GetStripeTicketsForEventAsync(int eventId);
    }

    public class TicketAvailabilityService : ITicketAvailabilityService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<TicketAvailabilityService> _logger;
        private readonly IConfiguration _configuration;

        public TicketAvailabilityService(AppDbContext context, ILogger<TicketAvailabilityService> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// 🎯 FIXED ARCHITECTURE v6 - Get total tickets sold for a specific ticket type
        /// SEPARATES Stripe tickets from Organizer tickets to avoid double counting
        /// Reuses the same logic as Dashboard Tab 02 (Stripe) + Tab 03 (Organizer)
        /// </summary>
        public async Task<int> GetTicketsSoldAsync(int ticketTypeId)
        {
            var stripeTickets = await GetStripeTicketsSoldAsync(ticketTypeId);
            var organizerTickets = await GetOrganizerTicketsSoldAsync(ticketTypeId);
            var totalSold = stripeTickets + organizerTickets;
            
            _logger.LogInformation("🎯 FIXED v6 (No Double Counting) - GetTicketsSoldAsync: TicketTypeId={TicketTypeId}, StripeTickets={StripeTickets}, OrganizerTickets={OrganizerTickets}, TotalSold={TotalSold}", 
                ticketTypeId, stripeTickets, organizerTickets, totalSold);
            
            return totalSold;
        }

        /// <summary>
        /// 🎯 NEW METHOD - Get Stripe-paid tickets for a specific ticket type
        /// Uses Stripe API to get accurate count (same logic as Dashboard Tab 02)
        /// </summary>
        public async Task<int> GetStripeTicketsSoldAsync(int ticketTypeId)
        {
            try
            {
                // Get ticket type and event details
                var ticketType = await _context.TicketTypes
                    .Include(tt => tt.Event)
                    .FirstOrDefaultAsync(tt => tt.Id == ticketTypeId);

                if (ticketType?.Event == null)
                {
                    _logger.LogWarning("🎯 STRIPE COUNT - TicketType {TicketTypeId} or Event not found", ticketTypeId);
                    return 0;
                }

                // Get Stripe configuration
                var stripeSecretKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? 
                                    _configuration["Stripe:SecretKey"];

                if (string.IsNullOrEmpty(stripeSecretKey))
                {
                    _logger.LogWarning("🎯 STRIPE COUNT - Stripe secret key not configured");
                    return 0;
                }

                // Get event booking date range for efficient Stripe API calls
                DateTime? firstBookingDate = null;
                try
                {
                    var firstBooking = await _context.Bookings
                        .Where(b => b.EventId == ticketType.EventId)
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

                // Initialize Stripe and fetch sessions (same logic as Dashboard Tab 02)
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

                // Filter sessions by event title and paid status
                var eventTitle = ticketType.Event.Title;
                var relevantSessions = allSessions
                    .Where(s => s.Metadata != null && 
                               s.Metadata.ContainsKey("eventTitle") && 
                               s.Metadata["eventTitle"].Equals(eventTitle, StringComparison.OrdinalIgnoreCase) &&
                               s.PaymentStatus == "paid")
                    .ToList();

                // Count tickets for this specific ticket type by matching price
                var ticketCount = 0;
                foreach (var session in relevantSessions)
                {
                    if (session.Metadata != null && session.Metadata.ContainsKey("ticketDetails"))
                    {
                        try
                        {
                            var ticketDetailsJson = session.Metadata["ticketDetails"];
                            using var document = JsonDocument.Parse(ticketDetailsJson);
                            var ticketDetails = document.RootElement;

                            if (ticketDetails.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var ticketElement in ticketDetails.EnumerateArray())
                                {
                                    if (ticketElement.TryGetProperty("Type", out var typeProperty) &&
                                        ticketElement.TryGetProperty("Quantity", out var quantityProperty) &&
                                        ticketElement.TryGetProperty("UnitPrice", out var unitPriceProperty))
                                    {
                                        var unitPrice = unitPriceProperty.GetDecimal();
                                        var quantity = quantityProperty.GetInt32();
                                        
                                        // Match by ticket price (same logic as Dashboard Tab 02)
                                        if (unitPrice == ticketType.Price)
                                        {
                                            ticketCount += quantity;
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("🎯 STRIPE COUNT - Error parsing session {SessionId}: {Error}", session.Id, ex.Message);
                        }
                    }
                }

                _logger.LogInformation("🎯 STRIPE COUNT - TicketTypeId={TicketTypeId}, Price={Price}, StripeTickets={Count}, SessionsChecked={SessionCount}", 
                    ticketTypeId, ticketType.Price, ticketCount, relevantSessions.Count);

                return ticketCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🎯 STRIPE COUNT - Error getting Stripe tickets for TicketTypeId={TicketTypeId}", ticketTypeId);
                return 0;
            }
        }

        /// <summary>
        /// 🎯 NEW METHOD - Get organizer-issued tickets for a specific ticket type
        /// Uses OrganizerTicketPayments table (same logic as Dashboard Tab 03)
        /// UPDATED: Excludes cancelled tickets from count
        /// </summary>
        public async Task<int> GetOrganizerTicketsSoldAsync(int ticketTypeId)
        {
            try
            {
                var organizerTicketCount = await _context.OrganizerTicketPayments
                    .Where(otp => otp.TicketTypeId == ticketTypeId && 
                                  (otp.Status == null || otp.Status == "Active")) // Exclude cancelled tickets
                    .CountAsync();

                _logger.LogInformation("🎯 ORGANIZER COUNT - TicketTypeId={TicketTypeId}, OrganizerTickets={Count} (excluding cancelled)", 
                    ticketTypeId, organizerTicketCount);

                return organizerTicketCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🎯 ORGANIZER COUNT - Error getting organizer tickets for TicketTypeId={TicketTypeId}", ticketTypeId);
                return 0;
            }
        }

        /// <summary>
        /// 🎯 UPDATED ARCHITECTURE v6 - Get the number of tickets available for a specific ticket type
        /// Uses separated Stripe + Organizer counts to avoid double counting
        /// </summary>
        public async Task<int> GetTicketsAvailableAsync(int ticketTypeId)
        {
            var ticketType = await _context.TicketTypes
                .FirstOrDefaultAsync(tt => tt.Id == ticketTypeId);

            if (ticketType == null)
            {
                _logger.LogWarning("🎯 AVAILABILITY v6 - GetTicketsAvailableAsync: TicketTypeId={TicketTypeId} not found", 
                    ticketTypeId);
                return 0;
            }

            // Use MaxTickets for capacity (applies to both seated and standing tickets)
            if (ticketType.MaxTickets == null)
            {
                _logger.LogInformation("🎯 AVAILABILITY v6 - GetTicketsAvailableAsync: TicketTypeId={TicketTypeId}, MaxTickets=null, returning -1 (unlimited)", 
                    ticketTypeId);
                // For allocated seating or ticket types without limits, return -1 to indicate unlimited
                return -1;
            }

            var sold = await GetTicketsSoldAsync(ticketTypeId); // Uses separated Stripe + Organizer counts
            var available = ticketType.MaxTickets.Value - sold;
            
            _logger.LogInformation("🎯 AVAILABILITY v6 - GetTicketsAvailableAsync: TicketTypeId={TicketTypeId}, MaxTickets={MaxTickets}, Sold={Sold}, Available={Available}", 
                ticketTypeId, ticketType.MaxTickets.Value, sold, available);
            
            return Math.Max(0, available);
        }

        /// <summary>
        /// 🎯 UPDATED v6 - Check if a specific quantity of tickets is available for a ticket type
        /// Uses separated Stripe + Organizer counts
        /// </summary>
        public async Task<bool> IsTicketTypeAvailableAsync(int ticketTypeId, int requestedQuantity)
        {
            var available = await GetTicketsAvailableAsync(ticketTypeId);
            
            // If available is -1, it means no limit (allocated seating)
            if (available == -1) return true;
            
            return available >= requestedQuantity;
        }

        /// <summary>
        /// 🎯 UPDATED v6 - Get ticket availability for all ticket types in an event
        /// Uses separated Stripe + Organizer counts for each ticket type
        /// </summary>
        public async Task<Dictionary<int, int>> GetTicketAvailabilityForEventAsync(int eventId)
        {
            var ticketTypes = await _context.TicketTypes
                .Where(tt => tt.EventId == eventId)
                .ToListAsync();

            var availability = new Dictionary<int, int>();

            foreach (var ticketType in ticketTypes)
            {
                var available = await GetTicketsAvailableAsync(ticketType.Id);
                availability[ticketType.Id] = available;
            }

            return availability;
        }

        /// <summary>
        /// 🚀 NEW OPTIMIZED v7 - Get Stripe tickets for ALL ticket types in an event with ONE API call
        /// Major performance improvement: 1 Stripe API call instead of N calls (where N = number of ticket types)
        /// </summary>
        public async Task<Dictionary<int, int>> GetStripeTicketsForEventAsync(int eventId)
        {
            var result = new Dictionary<int, int>();
            
            try
            {
                // Get all ticket types for this event
                var ticketTypes = await _context.TicketTypes
                    .Where(tt => tt.EventId == eventId)
                    .Include(tt => tt.Event)
                    .ToListAsync();

                if (!ticketTypes.Any())
                {
                    _logger.LogWarning("🚀 OPTIMIZED STRIPE - No ticket types found for EventId={EventId}", eventId);
                    return result;
                }

                var eventObj = ticketTypes.First().Event;
                if (eventObj == null)
                {
                    _logger.LogWarning("🚀 OPTIMIZED STRIPE - Event not found for EventId={EventId}", eventId);
                    return result;
                }

                // Initialize result dictionary with all ticket types
                foreach (var ticketType in ticketTypes)
                {
                    result[ticketType.Id] = 0;
                }

                // Get Stripe configuration
                var stripeSecretKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? 
                                    _configuration["Stripe:SecretKey"];

                if (string.IsNullOrEmpty(stripeSecretKey))
                {
                    _logger.LogWarning("🚀 OPTIMIZED STRIPE - Stripe secret key not configured");
                    return result;
                }

                // Get event booking date range for efficient Stripe API calls
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

                // 🚀 SINGLE STRIPE API CALL for the entire event
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

                // Filter sessions by event title and paid status
                var eventTitle = eventObj.Title;
                var relevantSessions = allSessions
                    .Where(s => s.Metadata != null && 
                               s.Metadata.ContainsKey("eventTitle") && 
                               s.Metadata["eventTitle"].Equals(eventTitle, StringComparison.OrdinalIgnoreCase) &&
                               s.PaymentStatus == "paid")
                    .ToList();

                // 🚀 PROCESS ALL TICKET TYPES AT ONCE from the single API response
                foreach (var session in relevantSessions)
                {
                    if (session.Metadata != null && session.Metadata.ContainsKey("ticketDetails"))
                    {
                        try
                        {
                            var ticketDetailsJson = session.Metadata["ticketDetails"];
                            using var document = JsonDocument.Parse(ticketDetailsJson);
                            var ticketDetails = document.RootElement;

                            if (ticketDetails.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var ticketElement in ticketDetails.EnumerateArray())
                                {
                                    if (ticketElement.TryGetProperty("Type", out var typeProperty) &&
                                        ticketElement.TryGetProperty("Quantity", out var quantityProperty) &&
                                        ticketElement.TryGetProperty("UnitPrice", out var unitPriceProperty))
                                    {
                                        var unitPrice = unitPriceProperty.GetDecimal();
                                        var quantity = quantityProperty.GetInt32();
                                        
                                        // Match by ticket price across ALL ticket types
                                        foreach (var ticketType in ticketTypes)
                                        {
                                            if (unitPrice == ticketType.Price)
                                            {
                                                result[ticketType.Id] += quantity;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("🚀 OPTIMIZED STRIPE - Error parsing session {SessionId}: {Error}", session.Id, ex.Message);
                        }
                    }
                }

                _logger.LogInformation("🚀 OPTIMIZED STRIPE - EventId={EventId}, TicketTypes={TicketTypeCount}, SessionsChecked={SessionCount}, Results={Results}", 
                    eventId, ticketTypes.Count, relevantSessions.Count, string.Join(", ", result.Select(r => $"TT{r.Key}:{r.Value}")));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚀 OPTIMIZED STRIPE - Error getting Stripe tickets for EventId={EventId}", eventId);
                return result;
            }
        }

        /// <summary>
        /// 🚀 NEW OPTIMIZED v7 - Get complete availability for all ticket types in an event with optimized Stripe calls
        /// Uses single Stripe API call + batch organizer queries for maximum performance
        /// </summary>
        public async Task<Dictionary<int, TicketAvailabilityDetail>> GetEventTicketAvailabilityOptimizedAsync(int eventId)
        {
            var result = new Dictionary<int, TicketAvailabilityDetail>();
            
            try
            {
                // Get all ticket types for this event
                var ticketTypes = await _context.TicketTypes
                    .Where(tt => tt.EventId == eventId)
                    .ToListAsync();

                if (!ticketTypes.Any())
                {
                    _logger.LogWarning("🚀 OPTIMIZED AVAILABILITY - No ticket types found for EventId={EventId}", eventId);
                    return result;
                }

                // 🚀 SINGLE STRIPE API CALL for all ticket types
                var stripeTicketCounts = await GetStripeTicketsForEventAsync(eventId);

                // 🚀 SINGLE DATABASE QUERY for all organizer tickets
                var ticketTypeIds = ticketTypes.Select(tt => tt.Id).ToList();
                var organizerTicketCounts = await _context.OrganizerTicketPayments
                    .Where(otp => ticketTypeIds.Contains(otp.TicketTypeId))
                    .GroupBy(otp => otp.TicketTypeId)
                    .Select(g => new { TicketTypeId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.TicketTypeId, x => x.Count);

                // 🚀 PROCESS ALL TICKET TYPES in memory
                foreach (var ticketType in ticketTypes)
                {
                    var stripeCount = stripeTicketCounts.GetValueOrDefault(ticketType.Id, 0);
                    var organizerCount = organizerTicketCounts.GetValueOrDefault(ticketType.Id, 0);
                    var totalSold = stripeCount + organizerCount;

                    int available;
                    bool hasLimit;
                    
                    if (ticketType.MaxTickets == null)
                    {
                        available = -1; // Unlimited
                        hasLimit = false;
                    }
                    else
                    {
                        available = Math.Max(0, ticketType.MaxTickets.Value - totalSold);
                        hasLimit = true;
                    }

                    result[ticketType.Id] = new TicketAvailabilityDetail
                    {
                        TicketTypeId = ticketType.Id,
                        Available = available,
                        Sold = totalSold,
                        StripeTickets = stripeCount,
                        OrganizerTickets = organizerCount,
                        HasLimit = hasLimit
                    };
                }

                _logger.LogInformation("🚀 OPTIMIZED AVAILABILITY - EventId={EventId}, TicketTypes={Count}, StripeApiCalls=1, DatabaseQueries=2", 
                    eventId, ticketTypes.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🚀 OPTIMIZED AVAILABILITY - Error getting optimized availability for EventId={EventId}", eventId);
                return result;
            }
        }
    }

    /// <summary>
    /// 🚀 NEW DTO - Enhanced ticket availability detail with separated counts
    /// </summary>
    public class TicketAvailabilityDetail
    {
        public int TicketTypeId { get; set; }
        public int Available { get; set; }  // -1 means unlimited
        public int Sold { get; set; }       // Total sold (Stripe + Organizer)
        public int StripeTickets { get; set; }    // Stripe-paid tickets only
        public int OrganizerTickets { get; set; } // Organizer-issued tickets only
        public bool HasLimit { get; set; }
    }
}
