namespace EventBooking.API.DTOs.Revenue
{
    /// <summary>
    /// DTO for Tab 2: Paid to KiwiLanka - Stripe revenue analysis
    /// </summary>
    public class StripePricingTierDTO
    {
        public decimal TicketPrice { get; set; }
        public decimal Revenue { get; set; }
        public int Quantity { get; set; }
        public int SeatCombinations { get; set; }
        public int Transactions { get; set; }
        public string Currency { get; set; } = "NZD";
    }

    /// <summary>
    /// Main response for Tab 2 API endpoint
    /// </summary>
    public class StripeRevenueAnalysisDTO
    {
        public List<StripePricingTierDTO> PricingTiers { get; set; } = new List<StripePricingTierDTO>();
        public decimal TotalStripeRevenue { get; set; }
        public int TotalStripeTickets { get; set; }
        public int TotalStripeTransactions { get; set; }
        public decimal AverageTicketPrice { get; set; }
        public string Currency { get; set; } = "NZD";
        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
        public string EventTitle { get; set; } = string.Empty;
    }
}
