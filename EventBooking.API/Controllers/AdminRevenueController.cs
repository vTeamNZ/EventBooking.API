using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventBooking.API.Data;
using EventBooking.API.DTOs;
using EventBooking.API.Services;
using System.Globalization;

namespace EventBooking.API.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("admin/revenue")]
    public class AdminRevenueController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AdminRevenueController> _logger;
        private readonly IProcessingFeeService _processingFeeService;

        public AdminRevenueController(
            AppDbContext context,
            ILogger<AdminRevenueController> logger,
            IProcessingFeeService processingFeeService)
        {
            _context = context;
            _logger = logger;
            _processingFeeService = processingFeeService;
        }

        // GET: admin/revenue/processing-fees-summary
        [HttpGet("processing-fees-summary")]
        public async Task<ActionResult<ProcessingFeeRevenueDTO>> GetProcessingFeesSummary([FromQuery] AdminRevenueFilterDTO? filter = null)
        {
            try
            {
                _logger.LogInformation("Fetching processing fees summary for admin dashboard");

                // Set default filter dates if not provided
                filter ??= new AdminRevenueFilterDTO();
                var startDate = filter.StartDate ?? DateTime.UtcNow.AddYears(-1);
                var endDate = filter.EndDate ?? DateTime.UtcNow;

                // Get all bookings with processing fees
                var bookingsQuery = _context.Bookings
                    .Include(b => b.Event)
                        .ThenInclude(e => e.Organizer)
                    .Where(b => b.ProcessingFee > 0 && 
                               b.CreatedAt >= startDate && 
                               b.CreatedAt <= endDate);

                // Apply additional filters
                if (filter.OrganizerId.HasValue)
                {
                    bookingsQuery = bookingsQuery.Where(b => b.Event.OrganizerId == filter.OrganizerId.Value);
                }

                if (filter.EventId.HasValue)
                {
                    bookingsQuery = bookingsQuery.Where(b => b.EventId == filter.EventId.Value);
                }

                var bookingsWithFees = await bookingsQuery.ToListAsync();

                // Calculate summary metrics
                var totalFeesCollected = bookingsWithFees.Sum(b => b.ProcessingFee);
                var totalBookingsWithFees = bookingsWithFees.Count;
                var averageProcessingFee = totalBookingsWithFees > 0 ? totalFeesCollected / totalBookingsWithFees : 0;

                // Calculate this month's fees
                var currentMonth = DateTime.UtcNow.Date.AddDays(1 - DateTime.UtcNow.Day);
                var thisMonthFees = bookingsWithFees
                    .Where(b => b.CreatedAt >= currentMonth)
                    .Sum(b => b.ProcessingFee);

                // Calculate last month's fees
                var lastMonth = currentMonth.AddMonths(-1);
                var lastMonthEnd = currentMonth.AddDays(-1);
                var lastMonthFees = bookingsWithFees
                    .Where(b => b.CreatedAt >= lastMonth && b.CreatedAt <= lastMonthEnd)
                    .Sum(b => b.ProcessingFee);

                // Calculate growth
                var monthOverMonthGrowth = lastMonthFees > 0 
                    ? ((thisMonthFees - lastMonthFees) / lastMonthFees) * 100 
                    : (thisMonthFees > 0 ? 100 : 0);

                // Get events with processing fees enabled
                var eventsWithFeesEnabled = await _context.Events
                    .Where(e => e.ProcessingFeeEnabled)
                    .CountAsync();

                // Build event breakdown
                var eventBreakdown = await BuildEventBreakdown(filter);

                // Build trend data
                var trendData = await BuildTrendData(startDate, endDate, filter);

                // Build recommendations
                var recommendations = await BuildFeeStructureRecommendations();

                var result = new ProcessingFeeRevenueDTO
                {
                    TotalProcessingFeesCollected = totalFeesCollected,
                    TotalBookingsWithFees = totalBookingsWithFees,
                    TotalEventsWithFeesEnabled = eventsWithFeesEnabled,
                    AverageProcessingFee = averageProcessingFee,
                    ThisMonthFees = thisMonthFees,
                    LastMonthFees = lastMonthFees,
                    MonthOverMonthGrowth = monthOverMonthGrowth,
                    EventBreakdown = eventBreakdown,
                    TrendData = trendData,
                    Recommendations = recommendations
                };

                _logger.LogInformation("Successfully retrieved processing fees summary: ${TotalFees} from {BookingCount} bookings", 
                    totalFeesCollected, totalBookingsWithFees);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving processing fees summary");
                return StatusCode(500, "Error retrieving processing fees summary");
            }
        }

        // GET: admin/revenue/historical-analysis
        [HttpGet("historical-analysis")]
        public async Task<ActionResult<ProcessingFeeHistoricalAnalysisDTO>> GetHistoricalAnalysis([FromQuery] AdminRevenueFilterDTO? filter = null)
        {
            try
            {
                _logger.LogInformation("Fetching historical analysis for admin revenue dashboard");

                // Set default filter dates for historical analysis (last 2 years)
                filter ??= new AdminRevenueFilterDTO();
                var startDate = filter.StartDate ?? DateTime.UtcNow.AddYears(-2);
                var endDate = filter.EndDate ?? DateTime.UtcNow;

                var historicalData = await BuildHistoricalAnalysis(startDate, endDate, filter);

                return Ok(historicalData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving historical analysis");
                return StatusCode(500, "Error retrieving historical analysis");
            }
        }

        // GET: admin/revenue/event-fee-configuration
        [HttpGet("event-fee-configuration")]
        public async Task<ActionResult<object>> GetEventFeeConfiguration([FromQuery] AdminRevenueFilterDTO? filter = null)
        {
            try
            {
                _logger.LogInformation("Fetching event fee configuration for admin dashboard");

                // Set default filter dates if not provided
                filter ??= new AdminRevenueFilterDTO();
                var startDate = filter.StartDate ?? DateTime.UtcNow.AddYears(-1);
                var endDate = filter.EndDate ?? DateTime.UtcNow;

                // Get event configurations with detailed metrics
                var events = await _context.Events
                    .Include(e => e.Organizer)
                    .Where(e => e.Date >= startDate && e.Date <= endDate)
                    .ToListAsync();

                // Apply additional filters
                if (filter.OrganizerId.HasValue)
                {
                    events = events.Where(e => e.OrganizerId == filter.OrganizerId.Value).ToList();
                }

                if (filter.ProcessingFeeEnabled.HasValue)
                {
                    events = events.Where(e => e.ProcessingFeeEnabled == filter.ProcessingFeeEnabled.Value).ToList();
                }

                // Get booking data separately for each event
                var eventIds = events.Select(e => e.Id).ToList();
                var eventBookingData = await _context.Bookings
                    .Where(b => eventIds.Contains(b.EventId))
                    .GroupBy(b => b.EventId)
                    .Select(g => new
                    {
                        EventId = g.Key,
                        TotalBookings = g.Count(),
                        TotalRevenue = g.Sum(b => b.TotalAmount),
                        TotalProcessingFees = g.Where(b => b.ProcessingFee > 0).Sum(b => b.ProcessingFee),
                        LastBookingDate = g.Max(b => b.CreatedAt)
                    })
                    .ToDictionaryAsync(x => x.EventId);

                var eventConfigurations = events.Select(e => new
                {
                    EventId = e.Id,
                    EventTitle = e.Title,
                    EventDate = e.Date,
                    OrganizerName = e.Organizer?.Name ?? "Unknown",
                    ProcessingFeeEnabled = e.ProcessingFeeEnabled,
                    FeePercentage = e.ProcessingFeePercentage,
                    FixedFee = e.ProcessingFeeFixedAmount,
                    TotalBookings = eventBookingData.ContainsKey(e.Id) ? eventBookingData[e.Id].TotalBookings : 0,
                    TotalRevenue = eventBookingData.ContainsKey(e.Id) ? eventBookingData[e.Id].TotalRevenue : 0,
                    TotalProcessingFees = eventBookingData.ContainsKey(e.Id) ? eventBookingData[e.Id].TotalProcessingFees : 0,
                    Status = e.Status.ToString(),
                    LastUpdated = eventBookingData.ContainsKey(e.Id) ? eventBookingData[e.Id].LastBookingDate : e.Date ?? DateTime.UtcNow
                }).ToList();

                // Get fee recommendations
                var recommendations = await BuildFeeStructureRecommendations();
                
                var feeRecommendations = new List<object>
                {
                    new 
                    {
                        CurrentFeePercentage = recommendations.OptimalFeeStructure.RecommendedPercentage,
                        CurrentFixedFee = recommendations.OptimalFeeStructure.RecommendedFixedAmount,
                        RecommendedFeePercentage = recommendations.OptimalFeeStructure.RecommendedPercentage + 0.15m,
                        RecommendedFixedFee = recommendations.OptimalFeeStructure.RecommendedFixedAmount + 0.05m,
                        ProjectedRevenueIncrease = recommendations.OptimalFeeStructure.PotentialAdditionalRevenue,
                        Reasoning = recommendations.OptimalFeeStructure.Reasoning,
                        ConfidenceScore = 85
                    }
                };

                return Ok(new
                {
                    EventConfigurations = eventConfigurations,
                    FeeRecommendations = feeRecommendations
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving event fee configuration");
                return StatusCode(500, "Error retrieving event fee configuration");
            }
        }

        #region Private Helper Methods

        private async Task<List<EventProcessingFeeDTO>> BuildEventBreakdown(AdminRevenueFilterDTO filter)
        {
            var query = from e in _context.Events
                        join b in _context.Bookings on e.Id equals b.EventId into bookings
                        from booking in bookings.DefaultIfEmpty()
                        where e.ProcessingFeeEnabled
                        group booking by new { e.Id, e.Title, e.Date, e.OrganizerId, e.ProcessingFeePercentage, e.ProcessingFeeFixedAmount, OrganizerName = e.Organizer.Name, e.Status } into g
                        select new EventProcessingFeeDTO
                        {
                            EventId = g.Key.Id,
                            EventTitle = g.Key.Title,
                            EventDate = g.Key.Date,
                            OrganizerName = g.Key.OrganizerName ?? "Unknown",
                            OrganizerId = g.Key.OrganizerId ?? 0,
                            ProcessingFeeEnabled = true,
                            ProcessingFeePercentage = g.Key.ProcessingFeePercentage,
                            ProcessingFeeFixedAmount = g.Key.ProcessingFeeFixedAmount,
                            BookingCount = g.Count(b => b != null),
                            BookingsWithFees = g.Count(b => b != null && b.ProcessingFee > 0),
                            TotalFeesCollected = g.Where(b => b != null && b.ProcessingFee > 0).Sum(b => b.ProcessingFee),
                            NetEventRevenue = g.Where(b => b != null).Sum(b => b.TotalAmount - b.ProcessingFee),
                            TotalEventRevenue = g.Where(b => b != null).Sum(b => b.TotalAmount),
                            LastBookingDate = g.Where(b => b != null).Max(b => (DateTime?)b.CreatedAt),
                            EventStatus = g.Key.Status.ToString()
                        };

            var eventBreakdown = await query.ToListAsync();

            // Calculate additional metrics
            foreach (var eventData in eventBreakdown)
            {
                eventData.AverageOrderValue = eventData.BookingCount > 0 
                    ? eventData.TotalEventRevenue / eventData.BookingCount 
                    : 0;
                
                eventData.FeeConversionRate = eventData.BookingCount > 0 
                    ? (decimal)eventData.BookingsWithFees / eventData.BookingCount * 100 
                    : 0;
            }

            // Apply sorting
            return filter.SortDirection?.ToLower() == "asc" 
                ? SortEventBreakdownAscending(eventBreakdown, filter.SortBy) 
                : SortEventBreakdownDescending(eventBreakdown, filter.SortBy);
        }

        private List<EventProcessingFeeDTO> SortEventBreakdownDescending(List<EventProcessingFeeDTO> events, string sortBy)
        {
            return sortBy?.ToLower() switch
            {
                "bookincount" => events.OrderByDescending(e => e.BookingCount).ToList(),
                "eventtitle" => events.OrderByDescending(e => e.EventTitle).ToList(),
                "eventdate" => events.OrderByDescending(e => e.EventDate).ToList(),
                "organizername" => events.OrderByDescending(e => e.OrganizerName).ToList(),
                _ => events.OrderByDescending(e => e.TotalFeesCollected).ToList()
            };
        }

        private List<EventProcessingFeeDTO> SortEventBreakdownAscending(List<EventProcessingFeeDTO> events, string sortBy)
        {
            return sortBy?.ToLower() switch
            {
                "bookingcount" => events.OrderBy(e => e.BookingCount).ToList(),
                "eventtitle" => events.OrderBy(e => e.EventTitle).ToList(),
                "eventdate" => events.OrderBy(e => e.EventDate).ToList(),
                "organizername" => events.OrderBy(e => e.OrganizerName).ToList(),
                _ => events.OrderBy(e => e.TotalFeesCollected).ToList()
            };
        }

        private async Task<List<ProcessingFeeTrendDTO>> BuildTrendData(DateTime startDate, DateTime endDate, AdminRevenueFilterDTO filter)
        {
            // Build daily trend data for the specified period
            var bookingsQuery = _context.Bookings
                .Include(b => b.Event)
                .Where(b => b.ProcessingFee > 0 && 
                           b.CreatedAt >= startDate && 
                           b.CreatedAt <= endDate);

            if (filter.OrganizerId.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(b => b.Event.OrganizerId == filter.OrganizerId.Value);
            }

            if (filter.EventId.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(b => b.EventId == filter.EventId.Value);
            }

            var bookingsWithFees = await bookingsQuery.ToListAsync();

            // Group by date and calculate daily metrics
            var trendData = bookingsWithFees
                .GroupBy(b => b.CreatedAt.Date)
                .Select(g => new ProcessingFeeTrendDTO
                {
                    Date = g.Key,
                    DailyFeesCollected = g.Sum(b => b.ProcessingFee),
                    BookingsWithFees = g.Count(),
                    AverageFeeAmount = g.Average(b => b.ProcessingFee),
                    EventsActive = g.Select(b => b.EventId).Distinct().Count()
                })
                .OrderBy(t => t.Date)
                .ToList();

            return trendData;
        }

        private async Task<FeeStructureRecommendationDTO> BuildFeeStructureRecommendations()
        {
            // Get all processing fee structures currently in use
            var feeStructures = await _context.Events
                .Where(e => e.ProcessingFeeEnabled)
                .Select(e => new { e.ProcessingFeePercentage, e.ProcessingFeeFixedAmount })
                .Distinct()
                .ToListAsync();

            var performanceList = new List<FeeStructurePerformanceDTO>();

            foreach (var structure in feeStructures)
            {
                var eventsWithThisStructure = await _context.Events
                    .Include(e => e.Organizer)
                    .Where(e => e.ProcessingFeeEnabled && 
                               e.ProcessingFeePercentage == structure.ProcessingFeePercentage &&
                               e.ProcessingFeeFixedAmount == structure.ProcessingFeeFixedAmount)
                    .ToListAsync();

                var bookingsForStructure = await _context.Bookings
                    .Where(b => eventsWithThisStructure.Select(e => e.Id).Contains(b.EventId))
                    .ToListAsync();

                var bookingsWithFees = bookingsForStructure.Where(b => b.ProcessingFee > 0).ToList();

                if (bookingsForStructure.Any())
                {
                    var performance = new FeeStructurePerformanceDTO
                    {
                        FeePercentage = structure.ProcessingFeePercentage,
                        FixedAmount = structure.ProcessingFeeFixedAmount,
                        EventsUsing = eventsWithThisStructure.Count,
                        TotalBookings = bookingsForStructure.Count,
                        TotalFeesCollected = bookingsWithFees.Sum(b => b.ProcessingFee),
                        AverageOrderValue = bookingsForStructure.Average(b => b.TotalAmount),
                        ConversionRate = bookingsForStructure.Count > 0 ? (decimal)bookingsWithFees.Count / bookingsForStructure.Count * 100 : 0,
                        RevenuePerBooking = bookingsForStructure.Count > 0 ? bookingsWithFees.Sum(b => b.ProcessingFee) / bookingsForStructure.Count : 0
                    };

                    // Assign performance rating
                    performance.PerformanceRating = CalculatePerformanceRating(performance);
                    performanceList.Add(performance);
                }
            }

            // Generate recommendations
            var recommendations = GenerateRecommendations(performanceList);
            var optimalStructure = DetermineOptimalFeeStructure(performanceList);
            var platformAverage = performanceList.Count > 0 ? performanceList.Average(p => p.ConversionRate) : 0;

            return new FeeStructureRecommendationDTO
            {
                FeeStructurePerformance = performanceList.OrderByDescending(p => p.RevenuePerBooking).ToList(),
                OptimalFeeStructure = optimalStructure,
                Recommendations = recommendations,
                PlatformAverageConversionRate = platformAverage
            };
        }

        private async Task<ProcessingFeeHistoricalAnalysisDTO> BuildHistoricalAnalysis(DateTime startDate, DateTime endDate, AdminRevenueFilterDTO filter)
        {
            var bookingsQuery = _context.Bookings
                .Include(b => b.Event)
                .Where(b => b.ProcessingFee > 0 && 
                           b.CreatedAt >= startDate && 
                           b.CreatedAt <= endDate);

            if (filter.OrganizerId.HasValue)
            {
                bookingsQuery = bookingsQuery.Where(b => b.Event.OrganizerId == filter.OrganizerId.Value);
            }

            var bookingsWithFees = await bookingsQuery.ToListAsync();

            // Monthly trends
            var monthlyTrends = bookingsWithFees
                .GroupBy(b => new { b.CreatedAt.Year, b.CreatedAt.Month })
                .Select(g => new MonthlyRevenueDTO
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(g.Key.Month),
                    ProcessingFeesCollected = g.Sum(b => b.ProcessingFee),
                    BookingsWithFees = g.Count(),
                    ActiveEvents = g.Select(b => b.EventId).Distinct().Count()
                })
                .OrderBy(m => m.Year).ThenBy(m => m.Month)
                .ToList();

            // Calculate growth percentages for monthly data
            for (int i = 1; i < monthlyTrends.Count; i++)
            {
                var current = monthlyTrends[i];
                var previous = monthlyTrends[i - 1];
                
                current.GrowthPercentage = previous.ProcessingFeesCollected > 0 
                    ? ((current.ProcessingFeesCollected - previous.ProcessingFeesCollected) / previous.ProcessingFeesCollected) * 100
                    : (current.ProcessingFeesCollected > 0 ? 100 : 0);
            }

            // Quarterly trends
            var quarterlyTrends = bookingsWithFees
                .GroupBy(b => new { b.CreatedAt.Year, Quarter = (b.CreatedAt.Month - 1) / 3 + 1 })
                .Select(g => new QuarterlyRevenueDTO
                {
                    Year = g.Key.Year,
                    Quarter = g.Key.Quarter,
                    ProcessingFeesCollected = g.Sum(b => b.ProcessingFee),
                    BookingsWithFees = g.Count()
                })
                .OrderBy(q => q.Year).ThenBy(q => q.Quarter)
                .ToList();

            // Calculate growth percentages for quarterly data
            for (int i = 1; i < quarterlyTrends.Count; i++)
            {
                var current = quarterlyTrends[i];
                var previous = quarterlyTrends[i - 1];
                
                current.GrowthPercentage = previous.ProcessingFeesCollected > 0 
                    ? ((current.ProcessingFeesCollected - previous.ProcessingFeesCollected) / previous.ProcessingFeesCollected) * 100
                    : (current.ProcessingFeesCollected > 0 ? 100 : 0);
            }

            // Growth analysis
            var growthAnalysis = CalculateGrowthAnalysis(monthlyTrends, quarterlyTrends);

            // Seasonality insights
            var seasonalityInsights = CalculateSeasonalityInsights(monthlyTrends);

            return new ProcessingFeeHistoricalAnalysisDTO
            {
                MonthlyTrends = monthlyTrends,
                QuarterlyTrends = quarterlyTrends,
                GrowthAnalysis = growthAnalysis,
                SeasonalityInsights = seasonalityInsights
            };
        }

        private string CalculatePerformanceRating(FeeStructurePerformanceDTO performance)
        {
            // Simple rating based on revenue per booking and conversion rate
            var score = (performance.RevenuePerBooking * 0.6m) + (performance.ConversionRate * 0.4m);

            return score switch
            {
                >= 15 => "Excellent",
                >= 10 => "Good", 
                >= 5 => "Average",
                _ => "Poor"
            };
        }

        private List<string> GenerateRecommendations(List<FeeStructurePerformanceDTO> performanceList)
        {
            var recommendations = new List<string>();

            if (!performanceList.Any())
            {
                recommendations.Add("No processing fee data available to generate recommendations.");
                return recommendations;
            }

            var bestPerforming = performanceList.OrderByDescending(p => p.RevenuePerBooking).First();
            var avgConversion = performanceList.Average(p => p.ConversionRate);

            recommendations.Add($"Best performing fee structure: {bestPerforming.FeePercentage}% + ${bestPerforming.FixedAmount:F2} with ${bestPerforming.RevenuePerBooking:F2} revenue per booking");
            
            if (bestPerforming.ConversionRate > avgConversion)
            {
                recommendations.Add($"Consider adopting the {bestPerforming.FeePercentage}% + ${bestPerforming.FixedAmount:F2} structure for better conversion rates");
            }

            if (performanceList.Any(p => p.ConversionRate < 50))
            {
                recommendations.Add("Some fee structures show low conversion rates - consider reducing fees for better adoption");
            }

            if (performanceList.Any(p => p.RevenuePerBooking < 2))
            {
                recommendations.Add("Consider increasing processing fees for events with very low revenue per booking");
            }

            return recommendations;
        }

        private RecommendationInsightDTO DetermineOptimalFeeStructure(List<FeeStructurePerformanceDTO> performanceList)
        {
            if (!performanceList.Any())
            {
                return new RecommendationInsightDTO
                {
                    RecommendedPercentage = 2.5m,
                    RecommendedFixedAmount = 0.30m,
                    Reasoning = "Default recommendation based on industry standards",
                    PotentialAdditionalRevenue = 0,
                    EstimatedConversionRate = 80
                };
            }

            var optimal = performanceList.OrderByDescending(p => p.RevenuePerBooking).First();
            
            return new RecommendationInsightDTO
            {
                RecommendedPercentage = optimal.FeePercentage,
                RecommendedFixedAmount = optimal.FixedAmount,
                Reasoning = $"This structure generates the highest revenue per booking (${optimal.RevenuePerBooking:F2}) with {optimal.ConversionRate:F1}% conversion rate",
                PotentialAdditionalRevenue = CalculatePotentialRevenue(performanceList, optimal),
                EstimatedConversionRate = optimal.ConversionRate
            };
        }

        private decimal CalculatePotentialRevenue(List<FeeStructurePerformanceDTO> performanceList, FeeStructurePerformanceDTO optimal)
        {
            var currentAverage = performanceList.Average(p => p.RevenuePerBooking);
            var totalBookings = performanceList.Sum(p => p.TotalBookings);
            
            return (optimal.RevenuePerBooking - currentAverage) * totalBookings;
        }

        private GrowthAnalysisDTO CalculateGrowthAnalysis(List<MonthlyRevenueDTO> monthlyTrends, List<QuarterlyRevenueDTO> quarterlyTrends)
        {
            var analysis = new GrowthAnalysisDTO();

            if (monthlyTrends.Count >= 2)
            {
                var lastMonth = monthlyTrends.Last();
                var previousMonth = monthlyTrends[monthlyTrends.Count - 2];
                analysis.MonthOverMonthGrowth = previousMonth.ProcessingFeesCollected > 0 
                    ? ((lastMonth.ProcessingFeesCollected - previousMonth.ProcessingFeesCollected) / previousMonth.ProcessingFeesCollected) * 100
                    : 0;
            }

            if (quarterlyTrends.Count >= 2)
            {
                var lastQuarter = quarterlyTrends.Last();
                var previousQuarter = quarterlyTrends[quarterlyTrends.Count - 2];
                analysis.QuarterOverQuarterGrowth = previousQuarter.ProcessingFeesCollected > 0 
                    ? ((lastQuarter.ProcessingFeesCollected - previousQuarter.ProcessingFeesCollected) / previousQuarter.ProcessingFeesCollected) * 100
                    : 0;
            }

            // Determine trend
            var recentTrend = monthlyTrends.TakeLast(3).ToList();
            if (recentTrend.Count >= 3)
            {
                var isIncreasing = recentTrend[2].ProcessingFeesCollected > recentTrend[1].ProcessingFeesCollected &&
                                 recentTrend[1].ProcessingFeesCollected > recentTrend[0].ProcessingFeesCollected;
                var isDecreasing = recentTrend[2].ProcessingFeesCollected < recentTrend[1].ProcessingFeesCollected &&
                                 recentTrend[1].ProcessingFeesCollected < recentTrend[0].ProcessingFeesCollected;

                analysis.GrowthTrend = isIncreasing ? "Increasing" : isDecreasing ? "Decreasing" : "Stable";
            }

            // Generate growth factors
            analysis.GrowthFactors = new List<string>
            {
                "Processing fee adoption by event organizers",
                "Increase in event bookings",
                "Seasonal event patterns",
                "Fee structure optimization"
            };

            return analysis;
        }

        private SeasonalityInsightDTO CalculateSeasonalityInsights(List<MonthlyRevenueDTO> monthlyTrends)
        {
            var insights = new SeasonalityInsightDTO();

            if (monthlyTrends.Any())
            {
                // Find peak and lowest months by average revenue
                var monthlyAverages = monthlyTrends
                    .GroupBy(m => m.Month)
                    .Select(g => new { Month = g.Key, AvgRevenue = g.Average(m => m.ProcessingFeesCollected) })
                    .ToList();

                if (monthlyAverages.Any())
                {
                    var peakMonth = monthlyAverages.OrderByDescending(m => m.AvgRevenue).First();
                    var lowestMonth = monthlyAverages.OrderBy(m => m.AvgRevenue).First();

                    insights.PeakMonth = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(peakMonth.Month);
                    insights.LowestMonth = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(lowestMonth.Month);

                    // Calculate seasonality score (coefficient of variation)
                    var avgRevenue = monthlyAverages.Average(m => m.AvgRevenue);
                    var variance = monthlyAverages.Average(m => Math.Pow((double)(m.AvgRevenue - avgRevenue), 2));
                    var stdDev = (decimal)Math.Sqrt(variance);
                    insights.SeasonalityScore = avgRevenue > 0 ? (stdDev / avgRevenue) * 100 : 0;

                    // Generate seasonal patterns
                    insights.SeasonalPatterns = new List<string>
                    {
                        $"Peak revenue typically occurs in {insights.PeakMonth}",
                        $"Lowest revenue typically occurs in {insights.LowestMonth}",
                        insights.SeasonalityScore > 50 ? "High seasonal variation detected" : "Low seasonal variation detected"
                    };
                }
            }

            return insights;
        }

        // POST: admin/revenue/export
        [HttpPost("export")]
        public async Task<IActionResult> ExportRevenueData([FromBody] ExportRequestDTO exportRequest)
        {
            try
            {
                _logger.LogInformation("Exporting revenue data in {Format} format", exportRequest.Format);

                // Get the data based on the request
                var filter = new AdminRevenueFilterDTO
                {
                    StartDate = exportRequest.Options?.DateRange?.Start,
                    EndDate = exportRequest.Options?.DateRange?.End,
                    OrganizerId = exportRequest.Options?.OrganizerId
                };

                var data = await GetProcessingFeesSummary(filter);
                
                // For now, return a simple CSV format
                // You can enhance this with proper Excel/PDF generation
                if (exportRequest.Format.ToLower() == "csv")
                {
                    var csv = GenerateCSV(data.Value);
                    return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"revenue-export-{DateTime.Now:yyyy-MM-dd}.csv");
                }

                // For Excel and PDF, you would implement proper generation here
                // For now, return CSV for all formats
                var csvData = GenerateCSV(data.Value);
                var contentType = exportRequest.Format.ToLower() switch
                {
                    "excel" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "pdf" => "application/pdf",
                    _ => "text/csv"
                };
                
                var extension = exportRequest.Format.ToLower() switch
                {
                    "excel" => "xlsx",
                    "pdf" => "pdf",
                    _ => "csv"
                };

                return File(System.Text.Encoding.UTF8.GetBytes(csvData), contentType, $"revenue-export-{DateTime.Now:yyyy-MM-dd}.{extension}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting revenue data");
                return StatusCode(500, "Error exporting revenue data");
            }
        }

        private string GenerateCSV(ProcessingFeeRevenueDTO data)
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Revenue Export");
            csv.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            csv.AppendLine("");
            csv.AppendLine("Summary");
            csv.AppendLine($"Total Processing Fees Collected,{data.TotalProcessingFeesCollected:C}");
            csv.AppendLine($"Total Bookings with Fees,{data.TotalBookingsWithFees}");
            csv.AppendLine($"Total Events with Fees Enabled,{data.TotalEventsWithFeesEnabled}");
            csv.AppendLine($"Average Processing Fee,{data.AverageProcessingFee:C}");
            csv.AppendLine($"This Month Fees,{data.ThisMonthFees:C}");
            csv.AppendLine($"Last Month Fees,{data.LastMonthFees:C}");
            csv.AppendLine($"Month over Month Growth,{data.MonthOverMonthGrowth:F2}%");
            csv.AppendLine("");
            csv.AppendLine("Event Breakdown");
            csv.AppendLine("Event ID,Event Title,Organizer,Total Fees Collected,Booking Count,Fee Enabled,Fee Percentage,Fixed Amount");
            
            foreach (var eventData in data.EventBreakdown)
            {
                csv.AppendLine($"{eventData.EventId},\"{eventData.EventTitle}\",\"{eventData.OrganizerName}\",{eventData.TotalFeesCollected:C},{eventData.BookingCount},{eventData.ProcessingFeeEnabled},{eventData.ProcessingFeePercentage:F2}%,{eventData.ProcessingFeeFixedAmount:C}");
            }

            return csv.ToString();
        }

        #endregion
    }

    // DTO for export requests
    public class ExportRequestDTO
    {
        public string Format { get; set; } = "csv";
        public string DataType { get; set; } = "summary";
        public ExportOptionsDTO? Options { get; set; }
    }

    public class ExportOptionsDTO
    {
        public DateRangeDTO? DateRange { get; set; }
        public int? OrganizerId { get; set; }
        public bool IncludeCharts { get; set; } = false;
        public bool IncludeSummary { get; set; } = true;
        public bool IncludeDetails { get; set; } = true;
    }

    public class DateRangeDTO
    {
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
    }

}
