namespace EventBooking.API.DTOs.Revenue
{
    /// <summary>
    /// DTO for individual ticket type organizer revenue data
    /// </summary>
    public class OrganizerTicketTypeDTO
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
        public string Color { get; set; } = string.Empty; // For UI display
    }

    /// <summary>
    /// Main response for Tab 3: Paid to Organizer API endpoint
    /// </summary>
    public class OrganizerRevenueDTO
    {
        public List<OrganizerTicketTypeDTO> TicketTypes { get; set; } = new List<OrganizerTicketTypeDTO>();
        public int TotalIssued { get; set; }
        public int TotalPaid { get; set; }
        public int TotalUnpaid { get; set; }
        public decimal TotalOrganizerRevenue { get; set; }
        public decimal PaidRevenue { get; set; }
        public decimal UnpaidRevenue { get; set; }
        public decimal PaymentCompletionRate { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
