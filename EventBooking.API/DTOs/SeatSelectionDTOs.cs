using EventBooking.API.Models;

namespace EventBooking.API.DTOs
{
    public class SeatLayoutResponse
    {
        public int EventId { get; set; }
        public SeatSelectionMode Mode { get; set; }
        public SeatLayoutVenueDTO? Venue { get; set; }
        public StageDTO? Stage { get; set; }
        public List<SeatDTO> Seats { get; set; } = new List<SeatDTO>();
        public List<TableDTO> Tables { get; set; } = new List<TableDTO>();
        public List<TicketTypeDTO> TicketTypes { get; set; } = new List<TicketTypeDTO>();
        
        // Aisle configuration
        public bool HasHorizontalAisles { get; set; } = false;
        public string HorizontalAisleRows { get; set; } = string.Empty; 
        public bool HasVerticalAisles { get; set; } = false;
        public string VerticalAisleSeats { get; set; } = string.Empty;
        public int AisleWidth { get; set; } = 2;
    }

    public class SeatLayoutVenueDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class StageDTO
    {
        public decimal X { get; set; }
        public decimal Y { get; set; }
        public decimal Width { get; set; }
        public decimal Height { get; set; }
    }

    public class SeatDTO
    {
        public int Id { get; set; }
        public string SeatNumber { get; set; } = string.Empty;
        public string Row { get; set; } = string.Empty;
        public int Number { get; set; }
        public decimal X { get; set; }
        public decimal Y { get; set; }
        public decimal Width { get; set; }
        public decimal Height { get; set; }
        public decimal Price { get; set; }
        public SeatStatus Status { get; set; }
        public int? TicketTypeId { get; set; }
        public TicketTypeDTO? TicketType { get; set; }
        public int? TableId { get; set; }
        public DateTime? ReservedUntil { get; set; }
    }

    public class TableDTO
    {
        public int Id { get; set; }
        public string TableNumber { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public decimal X { get; set; }
        public decimal Y { get; set; }
        public decimal Width { get; set; }
        public decimal Height { get; set; }
        public string Shape { get; set; } = string.Empty;
        public decimal PricePerSeat { get; set; }
        public decimal? TablePrice { get; set; }
        public int? TicketTypeId { get; set; }
        public int AvailableSeats { get; set; }
        public List<SeatDTO> Seats { get; set; } = new List<SeatDTO>();
    }

    public class ReserveSeatRequest
    {
        public int SeatId { get; set; }
        public string SessionId { get; set; } = string.Empty;
    }

    public class ReserveRowSeatRequest
    {
        public int Row { get; set; }
        public int Number { get; set; }
        public string SessionId { get; set; } = string.Empty;
    }

    public class ReserveTableRequest
    {
        public int TableId { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public bool FullTable { get; set; } = false;
        public List<int> SeatIds { get; set; } = new List<int>();
    }

    public class ReleaseSeatRequest
    {
        public int SeatId { get; set; }
        public string SessionId { get; set; } = string.Empty;
    }

    public class PricingResponse
    {
        public int EventId { get; set; }
        public SeatSelectionMode Mode { get; set; }
        public List<TicketTypeDTO> TicketTypes { get; set; } = new List<TicketTypeDTO>();
    }

    public class TicketTypeDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty; // Added color for UI display
        public string? SeatRowAssignments { get; set; } // Added for frontend to know which rows are for this ticket type
    }

    public class ReserveMultipleSeatsRequest
    {
        public List<int> SeatIds { get; set; } = new List<int>();
        public string SessionId { get; set; } = string.Empty;
        public int EventId { get; set; }
    }
}
