using EventBooking.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventBooking.API.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class TicketAvailabilityController : ControllerBase
    {
        private readonly ITicketAvailabilityService _ticketAvailabilityService;

        public TicketAvailabilityController(ITicketAvailabilityService ticketAvailabilityService)
        {
            _ticketAvailabilityService = ticketAvailabilityService;
        }

        /// <summary>
        /// Get availability for all ticket types in an event
        /// </summary>
        [HttpGet("event/{eventId}")]
        [AllowAnonymous]
        public async Task<ActionResult<Dictionary<int, TicketAvailabilityInfo>>> GetEventTicketAvailability(int eventId)
        {
            try
            {
                var availability = await _ticketAvailabilityService.GetTicketAvailabilityForEventAsync(eventId);
                
                var result = new Dictionary<int, TicketAvailabilityInfo>();
                
                foreach (var item in availability)
                {
                    var ticketTypeId = item.Key;
                    var available = item.Value;
                    var sold = await _ticketAvailabilityService.GetTicketsSoldAsync(ticketTypeId);
                    
                    result[ticketTypeId] = new TicketAvailabilityInfo
                    {
                        TicketTypeId = ticketTypeId,
                        Available = available,
                        Sold = sold,
                        HasLimit = available != -1
                    };
                }
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to get ticket availability", details = ex.Message });
            }
        }

        /// <summary>
        /// Get availability for a specific ticket type
        /// </summary>
        [HttpGet("ticket-type/{ticketTypeId}")]
        [AllowAnonymous]
        public async Task<ActionResult<TicketAvailabilityInfo>> GetTicketTypeAvailability(int ticketTypeId)
        {
            try
            {
                var available = await _ticketAvailabilityService.GetTicketsAvailableAsync(ticketTypeId);
                var sold = await _ticketAvailabilityService.GetTicketsSoldAsync(ticketTypeId);
                
                var result = new TicketAvailabilityInfo
                {
                    TicketTypeId = ticketTypeId,
                    Available = available,
                    Sold = sold,
                    HasLimit = available != -1
                };
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to get ticket availability", details = ex.Message });
            }
        }

        /// <summary>
        /// Check if a specific quantity is available for a ticket type
        /// </summary>
        [HttpGet("ticket-type/{ticketTypeId}/check/{quantity}")]
        [AllowAnonymous]
        public async Task<ActionResult<TicketAvailabilityCheckResult>> CheckTicketAvailability(int ticketTypeId, int quantity)
        {
            try
            {
                var isAvailable = await _ticketAvailabilityService.IsTicketTypeAvailableAsync(ticketTypeId, quantity);
                var available = await _ticketAvailabilityService.GetTicketsAvailableAsync(ticketTypeId);
                
                var result = new TicketAvailabilityCheckResult
                {
                    TicketTypeId = ticketTypeId,
                    RequestedQuantity = quantity,
                    IsAvailable = isAvailable,
                    AvailableQuantity = available,
                    HasLimit = available != -1
                };
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to check ticket availability", details = ex.Message });
            }
        }

        /// <summary>
        /// Check if specific quantity of tickets is available for a ticket type
        /// </summary>
        [HttpPost("check")]
        [AllowAnonymous]
        public async Task<ActionResult<TicketAvailabilityCheckResult>> CheckTicketAvailability(
            [FromBody] TicketAvailabilityCheckRequest request)
        {
            try
            {
                var isAvailable = await _ticketAvailabilityService.IsTicketTypeAvailableAsync(
                    request.TicketTypeId, request.RequestedQuantity);
                
                var availableQuantity = await _ticketAvailabilityService.GetTicketsAvailableAsync(
                    request.TicketTypeId);
                
                return Ok(new TicketAvailabilityCheckResult
                {
                    TicketTypeId = request.TicketTypeId,
                    RequestedQuantity = request.RequestedQuantity,
                    IsAvailable = isAvailable,
                    AvailableQuantity = availableQuantity,
                    HasLimit = availableQuantity != -1
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to check ticket availability", details = ex.Message });
            }
        }
    }

    // DTOs for API responses
    public class TicketAvailabilityInfo
    {
        public int TicketTypeId { get; set; }
        public int Available { get; set; }  // -1 means unlimited
        public int Sold { get; set; }
        public bool HasLimit { get; set; }
    }

    public class TicketAvailabilityCheckResult
    {
        public int TicketTypeId { get; set; }
        public int RequestedQuantity { get; set; }
        public bool IsAvailable { get; set; }
        public int AvailableQuantity { get; set; }  // -1 means unlimited
        public bool HasLimit { get; set; }
    }

    public class TicketAvailabilityCheckRequest
    {
        public int TicketTypeId { get; set; }
        public int RequestedQuantity { get; set; }
    }
}
