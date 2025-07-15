using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventBooking.API.Data;
using EventBooking.API.Models;
using EventBooking.API.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EventBooking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SeatingController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SeatingController> _logger;

        public SeatingController(AppDbContext context, ILogger<SeatingController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("events/{eventId}/booked-seats")]
        public async Task<ActionResult<IEnumerable<SeatReservation>>> GetBookedSeats(int eventId)
        {
            var bookedSeats = await _context.SeatReservations
                .Where(r => r.EventId == eventId && (r.IsConfirmed || r.ExpiresAt > DateTime.UtcNow))
                .ToListAsync();

            return Ok(bookedSeats);
        }

        [HttpPost("events/{eventId}/reserve")]
        public async Task<ActionResult<SeatReservation>> ReserveSeat(int eventId, ReserveRowSeatRequest request)
        {
            // Check if the event exists
            var @event = await _context.Events
                .Include(e => e.Venue)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (@event == null)
            {
                return NotFound("Event not found");
            }

            // Validate seat number
            if (request.Row < 0 || request.Row >= @event.Venue.NumberOfRows ||
                request.Number < 1 || request.Number > @event.Venue.SeatsPerRow)
            {
                return BadRequest("Invalid seat position");
            }

            // Check if seat is already booked or reserved
            var existingReservation = await _context.SeatReservations
                .Where(r => r.EventId == eventId && r.Row == request.Row && r.Number == request.Number)
                .Where(r => r.IsConfirmed || r.ExpiresAt > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            if (existingReservation != null)
            {
                return BadRequest("Seat is already reserved or booked");
            }

            // Create new reservation
            var reservation = new SeatReservation
            {
                EventId = eventId,
                Row = request.Row,
                Number = request.Number,
                SessionId = request.SessionId,
                ReservedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10), // 10-minute reservation
                IsConfirmed = false,
                UserId = User.Identity?.IsAuthenticated == true ? User.Identity.Name : null
            };

            _context.SeatReservations.Add(reservation);
            await _context.SaveChangesAsync();

            return Ok(reservation);
        }

        [HttpPost("events/{eventId}/confirm")]
        [Authorize]
        public async Task<ActionResult> ConfirmReservation(int eventId, ConfirmReservationRequest request)
        {
            var reservations = await _context.SeatReservations
                .Where(r => r.EventId == eventId && r.SessionId == request.SessionId && !r.IsConfirmed)
                .Where(r => r.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            if (!reservations.Any())
            {
                return NotFound("No valid reservations found");
            }

            foreach (var reservation in reservations)
            {
                reservation.IsConfirmed = true;
                reservation.UserId = User.Identity?.Name;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("events/{eventId}/release")]
        public async Task<ActionResult> ReleaseReservation(int eventId, string sessionId)
        {
            var reservations = await _context.SeatReservations
                .Where(r => r.EventId == eventId && r.SessionId == sessionId && !r.IsConfirmed)
                .ToListAsync();

            if (!reservations.Any())
            {
                return NotFound("No reservations found");
            }

            _context.SeatReservations.RemoveRange(reservations);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }


    public class ConfirmReservationRequest
    {
        public string SessionId { get; set; } = string.Empty;
    }
}
