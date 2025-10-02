namespace EventBooking.API.DTOs.Revenue
{
    /// <summary>
    /// DTO for Tab 4: Revenue Summary - Complete financial overview and reconciliation
    /// </summary>
    public class RevenueSummaryDTO
    {
        // Panel 1: Total Event Capacity Value
        public decimal MaxPossibleRevenue { get; set; }
        
        // Panel 2: KiwiLanka Revenue (from Tab 2)
        public decimal KiwiLankaRevenue { get; set; }
        public int KiwiLankaTickets { get; set; }
        
        // Panel 3: Organizer Revenue (from Tab 3)
        public decimal OrganizerRevenue { get; set; }
        public int OrganizerTickets { get; set; }
        
        // Panel 4: Combined Summary
        public decimal TotalRevenue { get; set; }
        public decimal RemainingCapacityValue { get; set; }
        public decimal UtilizationPercentage { get; set; }
        
        // Revenue split percentages
        public decimal KiwiLankaPercentage { get; set; }
        public decimal OrganizerPercentage { get; set; }
        
        // Estimated fees (optional)
        public decimal EstimatedPlatformCommission { get; set; }
        public decimal EstimatedStripeFees { get; set; }
        public decimal EstimatedNetToOrganizer { get; set; }
        
        // Analysis metadata
        public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
        public string EventTitle { get; set; } = string.Empty;
        public int EventId { get; set; }
        
        // Breakdown by ticket type for detailed view
        public List<RevenueSummaryTicketTypeDTO> TicketTypeBreakdown { get; set; } = new List<RevenueSummaryTicketTypeDTO>();
    }

    /// <summary>
    /// Per-ticket-type breakdown for revenue summary
    /// </summary>
    public class RevenueSummaryTicketTypeDTO
    {
        public int TicketTypeId { get; set; }
        public string TicketTypeName { get; set; } = string.Empty;
        public decimal TicketPrice { get; set; }
        
        // Capacity data
        public int TotalCapacity { get; set; }
        public decimal MaxPossibleRevenueForType { get; set; }
        
        // KiwiLanka sales
        public int KiwiLankaTicketsSold { get; set; }
        public decimal KiwiLankaRevenue { get; set; }
        
        // Organizer direct sales
        public int OrganizerTicketsSold { get; set; }
        public decimal OrganizerRevenue { get; set; }
        
        // Totals for this ticket type
        public int TotalSold { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal UtilizationPercentage { get; set; }
    }
}
