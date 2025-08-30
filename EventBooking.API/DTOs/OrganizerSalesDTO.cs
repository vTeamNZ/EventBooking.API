using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.DTOs
{
    public class OrganizerDashboardSummaryDTO
    {
        public int TotalEvents { get; set; }
        public int TotalTicketsSold { get; set; }
        public decimal TotalNetRevenue { get; set; }
        public List<EventSalesSummaryDTO> Events { get; set; } = new();
    }

    public class EventSalesSummaryDTO
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public DateTime? EventDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public int TotalTicketsSold { get; set; }
        public decimal TotalNetRevenue { get; set; }
        public List<TicketTypeSalesDTO> TicketSales { get; set; } = new();
    }

    public class TicketTypeSalesDTO
    {
        public int TicketTypeId { get; set; }
        public string TicketTypeName { get; set; } = string.Empty;
        public decimal TicketPrice { get; set; }
        public int TicketsSold { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal NetRevenue { get; set; }
    }

    public class EventSalesDetailDTO
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; } = string.Empty;
        public DateTime? EventDate { get; set; }
        public string EventLocation { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int TotalCapacity { get; set; }
        public int TotalTicketsSold { get; set; }
        public decimal TotalGrossRevenue { get; set; }
        public decimal TotalProcessingFees { get; set; }
        public decimal TotalNetRevenue { get; set; }
        public List<TicketTypeSalesDTO> TicketSales { get; set; } = new();
    }
}
