using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.DTOs
{
    public class ProcessingFeeRevenueDTO
    {
        public decimal TotalProcessingFeesCollected { get; set; }
        public int TotalBookingsWithFees { get; set; }
        public int TotalEventsWithFeesEnabled { get; set; }
        public decimal AverageProcessingFee { get; set; }
        public decimal ThisMonthFees { get; set; }
        public decimal LastMonthFees { get; set; }
        public decimal MonthOverMonthGrowth { get; set; }
        public List<EventProcessingFeeDTO> EventBreakdown { get; set; } = new();
        public List<ProcessingFeeTrendDTO> TrendData { get; set; } = new();
        public FeeStructureRecommendationDTO Recommendations { get; set; } = new();
    }

    public class EventProcessingFeeDTO
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public string OrganizerName { get; set; } = string.Empty;
        public int OrganizerId { get; set; }
        public DateTime? EventDate { get; set; }
        public bool ProcessingFeeEnabled { get; set; }
        public decimal ProcessingFeePercentage { get; set; }
        public decimal ProcessingFeeFixedAmount { get; set; }
        public decimal TotalFeesCollected { get; set; }
        public int BookingCount { get; set; }
        public int BookingsWithFees { get; set; }
        public decimal NetEventRevenue { get; set; }
        public decimal TotalEventRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal FeeConversionRate { get; set; } // Percentage of bookings that had processing fees
        public DateTime? LastBookingDate { get; set; }
        public string EventStatus { get; set; } = string.Empty;
    }

    public class ProcessingFeeTrendDTO
    {
        public DateTime Date { get; set; }
        public decimal DailyFeesCollected { get; set; }
        public int BookingsWithFees { get; set; }
        public decimal AverageFeeAmount { get; set; }
        public int EventsActive { get; set; }
    }

    public class FeeStructureRecommendationDTO
    {
        public List<FeeStructurePerformanceDTO> FeeStructurePerformance { get; set; } = new();
        public RecommendationInsightDTO OptimalFeeStructure { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();
        public decimal PlatformAverageConversionRate { get; set; }
    }

    public class FeeStructurePerformanceDTO
    {
        public decimal FeePercentage { get; set; }
        public decimal FixedAmount { get; set; }
        public int EventsUsing { get; set; }
        public int TotalBookings { get; set; }
        public decimal TotalFeesCollected { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal ConversionRate { get; set; }
        public decimal RevenuePerBooking { get; set; }
        public string PerformanceRating { get; set; } = string.Empty; // Excellent, Good, Average, Poor
    }

    public class RecommendationInsightDTO
    {
        public decimal RecommendedPercentage { get; set; }
        public decimal RecommendedFixedAmount { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public decimal PotentialAdditionalRevenue { get; set; }
        public decimal EstimatedConversionRate { get; set; }
    }

    public class AdminRevenueFilterDTO
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? OrganizerId { get; set; }
        public int? EventId { get; set; }
        public bool? ProcessingFeeEnabled { get; set; }
        public string SortBy { get; set; } = "TotalFeesCollected"; // TotalFeesCollected, BookingCount, EventTitle, EventDate
        public string SortDirection { get; set; } = "desc"; // asc, desc
    }

    public class ProcessingFeeHistoricalAnalysisDTO
    {
        public List<MonthlyRevenueDTO> MonthlyTrends { get; set; } = new();
        public List<QuarterlyRevenueDTO> QuarterlyTrends { get; set; } = new();
        public GrowthAnalysisDTO GrowthAnalysis { get; set; } = new();
        public SeasonalityInsightDTO SeasonalityInsights { get; set; } = new();
    }

    public class MonthlyRevenueDTO
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal ProcessingFeesCollected { get; set; }
        public int BookingsWithFees { get; set; }
        public int ActiveEvents { get; set; }
        public decimal GrowthPercentage { get; set; }
    }

    public class QuarterlyRevenueDTO
    {
        public int Year { get; set; }
        public int Quarter { get; set; }
        public decimal ProcessingFeesCollected { get; set; }
        public int BookingsWithFees { get; set; }
        public decimal GrowthPercentage { get; set; }
    }

    public class GrowthAnalysisDTO
    {
        public decimal MonthOverMonthGrowth { get; set; }
        public decimal QuarterOverQuarterGrowth { get; set; }
        public decimal YearOverYearGrowth { get; set; }
        public string GrowthTrend { get; set; } = string.Empty; // Increasing, Decreasing, Stable
        public List<string> GrowthFactors { get; set; } = new();
    }

    public class SeasonalityInsightDTO
    {
        public string PeakMonth { get; set; } = string.Empty;
        public string LowestMonth { get; set; } = string.Empty;
        public decimal SeasonalityScore { get; set; }
        public List<string> SeasonalPatterns { get; set; } = new();
    }
}
