using System;
using System.Collections.Generic;

namespace EventBooking.API.DTOs
{
    public class TicketReservationRequest
    {
        public int EventId { get; set; }
        public string UserId { get; set; }
        public List<TicketDetailDTO> TicketDetails { get; set; }
        public CustomerDetailsDTO CustomerDetails { get; set; }
        public List<FoodItemDTO> SelectedFoods { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class TicketDetailDTO
    {
        public string Type { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class CustomerDetailsDTO
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Mobile { get; set; }
    }

    public class FoodItemDTO
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }
}
