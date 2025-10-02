namespace EventBooking.API.DTOs.Revenue
{
    /// <summary>
    /// DTO for Tab 1: Tickets Summary - Complete operational ticket status overview
    /// </summary>
    public class RevenueTicketCapacityDTO
    {
        public int TicketTypeId { get; set; }
        public string TicketTypeName { get; set; } = string.Empty;
        public decimal TicketPrice { get; set; }
        public int SoldTickets { get; set; }
        public int AvailableTickets { get; set; }
        public int TotalCapacity { get; set; }
        public decimal UtilizationPercentage { get; set; }
        public string Color { get; set; } = string.Empty; // For UI display
    }

    /// <summary>
    /// Response wrapper for Tab 1 API endpoint
    /// </summary>
    public class RevenueTicketCapacitySummaryDTO
    {
        public List<RevenueTicketCapacityDTO> TicketTypes { get; set; } = new List<RevenueTicketCapacityDTO>();
        public int TotalSold { get; set; }
        public int TotalAvailable { get; set; }
        public int TotalCapacity { get; set; }
        public decimal OverallUtilization { get; set; }
        public decimal TotalPotentialRevenue { get; set; }
        public decimal CurrentRevenue { get; set; }
    }
}
