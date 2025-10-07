using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.DTOs
{
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

    public class DailyAnalyticsDTO
    {
        public DateTime Date { get; set; }
        public int PaidTickets { get; set; }
        public int OrganizerTickets { get; set; }
        public int TotalAttendance { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class BookingDetailViewDTO
    {
        public int BookingId { get; set; }
        public string PaymentId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public DateTime BookedTime { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int TotalTickets { get; set; }
        public List<TicketTypeDetailDTO> TicketDetails { get; set; } = new();
        public bool IsPaid { get; set; }
        public bool IsOrganizerBooking { get; set; }
    }

    public class TicketTypeDetailDTO
    {
        public string TicketTypeName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string SeatInfo { get; set; } = string.Empty;
    }

    public class TicketTypeBreakdownDTO
    {
        public int TicketTypeId { get; set; }
        public string TicketTypeName { get; set; } = string.Empty;
        public decimal TicketPrice { get; set; }
        public int PaidTickets { get; set; }
        public int OrganizerTickets { get; set; }
        public int TotalTickets { get; set; }
        public decimal PaidRevenue { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    /// <summary>
    /// DTO for reserved seats (Status = Booked but ReservedBy and ReservedUntil are NULL)
    /// These are "stuck" seats that are marked as booked but have no reservation information
    /// </summary>
    public class ReservedSeatViewDTO
    {
        public int SeatId { get; set; }
        public string SeatNumber { get; set; } = string.Empty;
        public string Row { get; set; } = string.Empty;
        public int Number { get; set; }
        public string TicketTypeName { get; set; } = string.Empty;
        public decimal SeatPrice { get; set; }
        public DateTime? ReservedUntil { get; set; }
        public string? ReservedBy { get; set; }
        public DateTime MarkedAsBookedTime { get; set; }
        public int DaysSinceBooked { get; set; }
    }
}
