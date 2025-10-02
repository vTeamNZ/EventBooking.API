using EventBooking.API.DTOs;
using EventBooking.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventBooking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrganizerTicketPaymentController : ControllerBase
    {
        private readonly IOrganizerTicketPaymentService _ticketPaymentService;
        private readonly ILogger<OrganizerTicketPaymentController> _logger;

        public OrganizerTicketPaymentController(
            IOrganizerTicketPaymentService ticketPaymentService,
            ILogger<OrganizerTicketPaymentController> logger)
        {
            _ticketPaymentService = ticketPaymentService;
            _logger = logger;
        }

        /// <summary>
        /// Get all organizer ticket payments for an event
        /// </summary>
        [HttpGet("event/{eventId}")]
        public async Task<ActionResult<PaginatedOrganizerPaymentsDTO>> GetEventTicketPayments(int eventId, [FromQuery] OrganizerPaymentSearchRequest? searchRequest = null)
        {
            try
            {
                searchRequest ??= new OrganizerPaymentSearchRequest { EventId = eventId };
                searchRequest.EventId = eventId; // Ensure EventId is set from route
                
                var payments = await _ticketPaymentService.GetEventPaymentsAsync(searchRequest);
                return Ok(payments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ticket payments for event {EventId}", eventId);
                return StatusCode(500, "An error occurred while retrieving ticket payments");
            }
        }

        /// <summary>
        /// Get a specific organizer ticket payment by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<OrganizerTicketPaymentDTO>> GetTicketPayment(int id)
        {
            try
            {
                var payment = await _ticketPaymentService.GetPaymentByIdAsync(id);
                if (payment == null)
                {
                    return NotFound($"Ticket payment with ID {id} not found");
                }
                return Ok(payment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving ticket payment {PaymentId}", id);
                return StatusCode(500, "An error occurred while retrieving the ticket payment");
            }
        }

        /// <summary>
        /// Create a new organizer ticket payment record
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<OrganizerTicketPaymentDTO>> CreateTicketPayment(CreateOrganizerTicketPaymentRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var payment = await _ticketPaymentService.CreatePaymentAsync(request);
                return CreatedAtAction(nameof(GetTicketPayment), new { id = payment.Id }, payment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating ticket payment for BookingLineItem {BookingLineItemId}", request.BookingLineItemId);
                return StatusCode(500, "An error occurred while creating the ticket payment");
            }
        }

        /// <summary>
        /// Update an existing organizer ticket payment record
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<OrganizerTicketPaymentDTO>> UpdateTicketPayment(int id, UpdateOrganizerTicketPaymentRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var payment = await _ticketPaymentService.UpdatePaymentAsync(id, request);
                if (payment == null)
                {
                    return NotFound($"Ticket payment with ID {id} not found");
                }
                return Ok(payment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ticket payment {PaymentId}", id);
                return StatusCode(500, "An error occurred while updating the ticket payment");
            }
        }

        /// <summary>
        /// Delete an organizer ticket payment record
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteTicketPayment(int id)
        {
            try
            {
                var success = await _ticketPaymentService.DeletePaymentAsync(id);
                if (!success)
                {
                    return NotFound($"Ticket payment with ID {id} not found");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting ticket payment {PaymentId}", id);
                return StatusCode(500, "An error occurred while deleting the ticket payment");
            }
        }

        /// <summary>
        /// Update payment status for multiple tickets (bulk operation)
        /// </summary>
        [HttpPost("bulk-update-status")]
        public async Task<ActionResult> BulkUpdatePaymentStatus(BulkUpdatePaymentStatusRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var updatedCount = await _ticketPaymentService.BulkUpdatePaymentStatusAsync(request);
                return Ok(new { UpdatedCount = updatedCount, Message = $"Updated {updatedCount} ticket payment records" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bulk updating payment status for {Count} tickets", request.PaymentIds.Count);
                return StatusCode(500, "An error occurred while updating payment status");
            }
        }

        /// <summary>
        /// Get payment summary for an event
        /// </summary>
        [HttpGet("event/{eventId}/summary")]
        public async Task<ActionResult<OrganizerPaymentSummaryDTO>> GetEventPaymentSummary(int eventId)
        {
            try
            {
                var summary = await _ticketPaymentService.GetEventPaymentSummaryAsync(eventId);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payment summary for event {EventId}", eventId);
                return StatusCode(500, "An error occurred while retrieving payment summary");
            }
        }
    }
}
