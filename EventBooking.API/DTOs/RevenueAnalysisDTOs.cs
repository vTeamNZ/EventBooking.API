using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.DTOs
{
    /// <summary>
    /// DTO for Tab 1: Tickets Summary - Complete operational ticket status overview
    /// </summary>
    public class TicketCapacityDTO
    {
        public int TicketTypeId { get; set; }
        public string TicketTypeName { get; set; } = string.Empty;
        public decimal TicketPrice { get; set; }
        public int SoldTickets { get; set; }
        public int AvailableTickets { get; set; }
        public int TotalCapacity { get; set; }
        public decimal UtilizationPercentage { get; set; }
        public string Color { get; set; } = "#007bff"; // For UI display
    }

    /// <summary>
    /// DTO for Tab 2: Stripe Revenue Analysis - Revenue processed through KiwiLanka platform  
    /// </summary>
    public class StripePricingTierDTO
    {
        public decimal TicketPrice { get; set; }
        public decimal Revenue { get; set; }
        public int Quantity { get; set; }
        public int SeatCombinations { get; set; }
        public int Transactions { get; set; }
        public decimal RevenuePercentage { get; set; }
        public decimal QuantityPercentage { get; set; }
    }

    public class StripeRevenueAnalysisDTO
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public List<StripePricingTierDTO> PricingTiers { get; set; } = new();
        public decimal TotalStripeRevenue { get; set; }
        public int TotalStripeTickets { get; set; }
        public int TotalStripeTransactions { get; set; }
        public decimal AverageTicketPrice { get; set; }
        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
        
        // New properties for date-based filtering transparency
        public int SessionsFetched { get; set; }
        public string AnalysisMethod { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO for Tab 3: Organizer Revenue - Track direct organizer sales and payment status
    /// </summary>
    public class OrganizerTicketTypeRevenueDTO
    {
        public int TicketTypeId { get; set; }
        public string TicketTypeName { get; set; } = string.Empty;
        public decimal TicketPrice { get; set; }
        public int IssuedTickets { get; set; }
        public int PaidTickets { get; set; }
        public int UnpaidTickets { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal PaidRevenue { get; set; }
        public decimal UnpaidRevenue { get; set; }
        public decimal PaymentPercentage { get; set; }
    }

    public class OrganizerRevenueDTO
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public List<OrganizerTicketTypeRevenueDTO> TicketTypes { get; set; } = new();
        public int TotalIssued { get; set; }
        public int TotalPaid { get; set; }
        public int TotalUnpaid { get; set; }
        public decimal TotalOrganizerRevenue { get; set; }
        public decimal PaidOrganizerRevenue { get; set; }
        public decimal UnpaidOrganizerRevenue { get; set; }
        public decimal OverallPaymentPercentage { get; set; }
    }

    /// <summary>
    /// DTO for Tab 4: Revenue Summary - Complete financial overview and reconciliation
    /// </summary>
    public class RevenueCapacityPanelDTO
    {
        public string PanelTitle { get; set; } = string.Empty;
        public List<RevenueBreakdownItemDTO> BreakdownItems { get; set; } = new();
        public decimal TotalRevenue { get; set; }
        public string DisplayCurrency { get; set; } = "NZD";
    }

    public class RevenueBreakdownItemDTO
    {
        public string TicketTypeName { get; set; } = string.Empty;
        public decimal TicketPrice { get; set; }
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
        public string FormattedLine { get; set; } = string.Empty; // e.g., "Front tickets: $140 × 16 = $2,240"
    }

    public class RevenueSummaryDTO
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        
        // Panel 1: Total Event Capacity Value
        public RevenueCapacityPanelDTO MaxPossibleRevenuePanel { get; set; } = new();
        
        // Panel 2: KiwiLanka Revenue (from Stripe)
        public RevenueCapacityPanelDTO KiwiLankaRevenuePanel { get; set; } = new();
        
        // Panel 3: Organizer Revenue
        public RevenueCapacityPanelDTO OrganizerRevenuePanel { get; set; } = new();
        
        // Panel 4: Combined Summary
        public RevenueSummaryTotalsDTO CombinedSummary { get; set; } = new();
        
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }

    public class RevenueSummaryTotalsDTO
    {
        public decimal KiwiLankaRevenue { get; set; }
        public decimal KiwiLankaPercentage { get; set; }
        public decimal OrganizerRevenue { get; set; }
        public decimal OrganizerPercentage { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal RemainingCapacityValue { get; set; }
        public decimal OverallEventUtilization { get; set; }
        
        // Estimated fees and commissions
        public decimal EstimatedPlatformCommission { get; set; }
        public decimal EstimatedStripeFees { get; set; }
        public decimal EstimatedNetToOrganizer { get; set; }
        public decimal EstimatedNetPercentage { get; set; }
    }

    /// <summary>
    /// Request DTO for generating revenue analysis
    /// </summary>
    public class RevenueAnalysisRequest
    {
        [Required(ErrorMessage = "Event ID is required")]
        public int EventId { get; set; }
        
        public bool IncludeStripeData { get; set; } = true;
        public bool IncludeOrganizerData { get; set; } = true;
        public bool IncludeCapacityAnalysis { get; set; } = true;
        
        [StringLength(3, ErrorMessage = "Currency code must be 3 characters")]
        public string Currency { get; set; } = "NZD";
    }

    /// <summary>
    /// Response DTO containing all four tab data for the enhanced sales dashboard
    /// </summary>
    public class EnhancedSalesDashboardDTO
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public DateTime? EventDate { get; set; }
        
        // Tab 1: Tickets Summary
        public List<TicketCapacityDTO> TicketsCapacitySummary { get; set; } = new();
        
        // Tab 2: Stripe Revenue Analysis
        public StripeRevenueAnalysisDTO? StripeRevenue { get; set; }
        
        // Tab 3: Organizer Revenue
        public OrganizerRevenueDTO OrganizerRevenue { get; set; } = new();
        
        // Tab 4: Revenue Summary
        public RevenueSummaryDTO RevenueSummary { get; set; } = new();
        
        // Metadata
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public string Currency { get; set; } = "NZD";
        public bool StripeDataAvailable { get; set; }
        public bool OrganizerDataAvailable { get; set; }
    }

    /// <summary>
    /// DTO for ticket capacity analysis
    /// </summary>
    public class TicketCapacityAnalysisDTO
    {
        public int EventId { get; set; }
        public int TotalCapacity { get; set; }
        public int TotalSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal UtilizationPercentage { get; set; }
        public List<TicketTypeUtilizationDTO> TicketTypeBreakdown { get; set; } = new();
    }

    /// <summary>
    /// DTO for ticket type utilization
    /// </summary>
    public class TicketTypeUtilizationDTO
    {
        public int TicketTypeId { get; set; }
        public string TicketTypeName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Capacity { get; set; }
        public int Sold { get; set; }
        public decimal Revenue { get; set; }
        public decimal UtilizationPercentage { get; set; }
    }

    /// <summary>
    /// DTO for event utilization analysis
    /// </summary>
    public class EventUtilizationDTO
    {
        public int EventId { get; set; }
        public int TotalCapacity { get; set; }
        public int TicketsSold { get; set; }
        public int RemainingCapacity { get; set; }
        public decimal UtilizationPercentage { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageTicketPrice { get; set; }
        public decimal ProjectedRevenue { get; set; }
    }

    /// <summary>
    /// DTO for ticket type revenue breakdown
    /// </summary>
    public class TicketTypeRevenueDTO
    {
        public int TicketTypeId { get; set; }
        public string TicketTypeName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int TicketsSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal ProcessingFees { get; set; }
        public decimal RevenuePercentage { get; set; }
    }

    /// <summary>
    /// Daily revenue breakdown DTO
    /// </summary>
    public class DailyRevenueDTO
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public int TransactionCount { get; set; }
        public decimal ProcessingFees { get; set; }
    }

    /// <summary>
    /// Payment method breakdown DTO
    /// </summary>
    public class PaymentMethodBreakdownDTO
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Percentage { get; set; }
    }

    /// <summary>
    /// Payment status breakdown DTO
    /// </summary>
    public class PaymentStatusBreakdownDTO
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
    }

    /// <summary>
    /// Customer analysis DTO
    /// </summary>
    public class CustomerAnalysisDTO
    {
        public int TotalUniqueCustomers { get; set; }
        public int RepeatCustomers { get; set; }
        public decimal RepeatCustomerPercentage { get; set; }
        public decimal AverageTicketsPerCustomer { get; set; }
    }

    /// <summary>
    /// Revenue source breakdown DTO
    /// </summary>
    public class RevenueSourceDTO
    {
        public string Source { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
    }

    /// <summary>
    /// Performance metrics DTO
    /// </summary>
    public class PerformanceMetricsDTO
    {
        public decimal GrossRevenue { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal ProcessingFeePercentage { get; set; }
        public decimal AverageRevenuePerTicket { get; set; }
        public decimal ConversionRate { get; set; }
        public decimal RefundRate { get; set; }
    }

    /// <summary>
    /// Comprehensive event revenue analysis DTO
    /// </summary>
    public class EventRevenueAnalysisDTO
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public int TotalTicketsSold { get; set; }
        public List<TicketTypeRevenueDTO> RevenueByTicketType { get; set; } = new();
        public List<PaymentMethodAnalysisDTO> PaymentMethodBreakdown { get; set; } = new();
        public List<DailyRevenueDTO> DailyRevenue { get; set; } = new();
        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Monthly revenue trend DTO for general revenue analysis
    /// </summary>
    public class MonthlyRevenueTrendDTO
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int TicketsSold { get; set; }
        public int EventCount { get; set; }
        public decimal AverageRevenuePerEvent { get; set; }
    }

    /// <summary>
    /// Event revenue comparison DTO
    /// </summary>
    public class EventRevenueComparisonDTO
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public decimal TotalRevenue { get; set; }
        public int TotalTickets { get; set; }
        public decimal AverageTicketPrice { get; set; }
        public decimal UtilizationPercentage { get; set; }
    }

    /// <summary>
    /// Payment method analysis DTO
    /// </summary>
    public class PaymentMethodAnalysisDTO
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public int TransactionCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Percentage { get; set; }
        public decimal AverageTransactionValue { get; set; }
    }

    /// <summary>
    /// Outstanding payment analysis DTO
    /// </summary>
    public class OutstandingPaymentAnalysisDTO
    {
        public int OrganizerId { get; set; }
        public decimal TotalOutstandingAmount { get; set; }
        public int TotalOutstandingTickets { get; set; }
        public List<EventOutstandingPaymentDTO> OutstandingByEvent { get; set; } = new();
        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Outstanding payment by event DTO
    /// </summary>
    public class EventOutstandingPaymentDTO
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public decimal OutstandingAmount { get; set; }
        public int OutstandingTickets { get; set; }
        public int DaysOutstanding { get; set; }
    }
}
