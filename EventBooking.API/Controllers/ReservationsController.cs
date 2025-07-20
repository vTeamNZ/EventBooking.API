using EventBooking.API.Data;
using EventBooking.API.DTOs;
using EventBooking.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EventBooking.API.Controllers
{
    [Authorize] // ✅ SECURITY FIX: Require authentication for reservation management
    [Route("[controller]")]
    [ApiController]
    public class ReservationsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReservationsController(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: api/Reservations
        [Authorize(Roles = "Admin")] // ✅ Only admins can view all reservations
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Reservation>>> GetReservations()
        {
            return await _context.Reservations.ToListAsync();
        }

        // GET: api/Reservations/5
        [Authorize(Roles = "Admin")] // ✅ Only admins can view specific reservations
        [HttpGet("{id}")]
        public async Task<ActionResult<Reservation>> GetReservation(int id)
        {
            var reservation = await _context.Reservations.FindAsync(id);

            if (reservation == null)
            {
                return NotFound();
            }

            return reservation;
        }

        [HttpPost]
        public async Task<IActionResult> CreateReservation(ReservationCreateDTO dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // from token

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return Unauthorized();

            var reservation = new Reservation
            {
                EventId = dto.EventId,
                Row = dto.Row,
                Number = dto.Number,
                IsReserved = true,
                UserId = userId
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            return Ok(reservation);
        }


        // PUT: api/Reservations/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutReservation(int id, Reservation reservation)
        {
            if (id != reservation.Id)
            {
                return BadRequest();
            }

            _context.Entry(reservation).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ReservationExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Reservations
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        /*[HttpPost]
        public async Task<ActionResult<Reservation>> PostReservation(Reservation reservation)
        {
            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetReservation", new { id = reservation.Id }, reservation);
        }*/

        // DELETE: api/Reservations/5
        [Authorize(Roles = "Admin")] // ✅ SECURITY FIX: Only admins can delete reservations
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReservation(int id)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                return NotFound();
            }

            _context.Reservations.Remove(reservation);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ReservationExists(int id)
        {
            return _context.Reservations.Any(e => e.Id == id);
        }

        // POST: api/Reservations/hold
        [HttpPost("hold")]
        public async Task<IActionResult> HoldSeats([FromBody] List<SeatHoldRequest> seatHolds)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            // Check if any seats are already reserved
            foreach (var hold in seatHolds)
            {
                var existingReservation = await _context.Reservations
                    .FirstOrDefaultAsync(r => r.EventId == hold.EventId && 
                                            r.Row == hold.Row && 
                                            r.Number == hold.Number && 
                                            r.IsReserved);
                
                if (existingReservation != null)
                {
                    return BadRequest($"Seat {hold.Row}-{hold.Number} is already reserved");
                }
            }

            // Create temporary holds (expires in 10 minutes)
            var expiry = DateTime.UtcNow.AddMinutes(10);
            var holds = seatHolds.Select(hold => new Reservation
            {
                EventId = hold.EventId,
                Row = hold.Row,
                Number = hold.Number,
                IsReserved = false, // Temporary hold
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiry
            }).ToList();

            _context.Reservations.AddRange(holds);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Seats held temporarily", expiresAt = expiry });
        }

        // POST: api/Reservations/release
        [HttpPost("release")]
        public async Task<IActionResult> ReleaseSeats([FromBody] List<SeatHoldRequest> seatHolds)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            foreach (var hold in seatHolds)
            {
                var reservation = await _context.Reservations
                    .FirstOrDefaultAsync(r => r.EventId == hold.EventId && 
                                            r.Row == hold.Row && 
                                            r.Number == hold.Number && 
                                            r.UserId == userId &&
                                            !r.IsReserved); // Only temporary holds

                if (reservation != null)
                {
                    _context.Reservations.Remove(reservation);
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Seats released" });
        }

        // GET: api/Reservations/event/5/status
        [HttpGet("event/{eventId}/status")]
        public async Task<ActionResult> GetReservationStatus(int eventId)
        {
            var reservations = await _context.Reservations
                .Where(r => r.EventId == eventId)
                .Select(r => new {
                    r.Row,
                    r.Number,
                    r.IsReserved,
                    r.UserId,
                    r.ExpiresAt
                })
                .ToListAsync();

            // Clean up expired holds
            var expiredHolds = reservations
                .Where(r => !r.IsReserved && r.ExpiresAt.HasValue && r.ExpiresAt < DateTime.UtcNow)
                .ToList();

            if (expiredHolds.Any())
            {
                var toRemove = await _context.Reservations
                    .Where(r => r.EventId == eventId && 
                              !r.IsReserved && 
                              r.ExpiresAt.HasValue && 
                              r.ExpiresAt < DateTime.UtcNow)
                    .ToListAsync();
                
                _context.Reservations.RemoveRange(toRemove);
                await _context.SaveChangesAsync();
            }

            var currentReservations = reservations
                .Where(r => r.IsReserved || (r.ExpiresAt.HasValue && r.ExpiresAt >= DateTime.UtcNow))
                .Select(r => new {
                    seat = $"{r.Row}-{r.Number}",
                    isReserved = r.IsReserved,
                    isHeld = !r.IsReserved && r.ExpiresAt.HasValue && r.ExpiresAt >= DateTime.UtcNow,
                    expiresAt = r.ExpiresAt
                })
                .ToList();

            return Ok(currentReservations);
        }

        // POST: api/reservations/reserve-tickets
        [HttpPost("reserve-tickets")]
        [Authorize(Roles = "Admin,Organizer")]
        public async Task<ActionResult> ReserveTicketsWithoutPayment([FromBody] TicketReservationRequest request)
        {
            if (request == null)
            {
                return BadRequest("Invalid reservation data");
            }

            try
            {
                // Create a new reservation
                // TODO: Fix Reservation model - current model doesn't match expected properties
                throw new NotImplementedException("Reservation creation needs to be updated to match the current Reservation model");
                
                /*
                var reservation = new Reservation
                {
                    EventId = request.EventId,
                    UserId = request.UserId,
                    CustomerFirstName = request.CustomerDetails.FirstName,
                    CustomerLastName = request.CustomerDetails.LastName,
                    CustomerEmail = request.CustomerDetails.Email,
                    CustomerPhone = request.CustomerDetails.Mobile,
                    TotalAmount = request.TotalAmount,
                    Status = "Reserved",
                    CreatedAt = DateTime.UtcNow,
                    ReservationCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper()
                };
                */
                
                /*
                _context.Reservations.Add(reservation);
                await _context.SaveChangesAsync();

                // Add ticket details
                foreach (var ticket in request.TicketDetails)
                {
                    for (int i = 0; i < ticket.Quantity; i++)
                    {
                        var reservedTicket = new ReservedTicket
                        {
                            ReservationId = reservation.Id,
                            TicketTypeId = ticket.TicketTypeId,
                            Price = ticket.Price
                        };

                        _context.ReservedTickets.Add(reservedTicket);
                    }
                }

                // Add food details
                foreach (var food in request.SelectedFoods)
                {
                    for (int i = 0; i < food.Quantity; i++)
                    {
                        var reservedFood = new ReservedFood
                        {
                            ReservationId = reservation.Id,
                            FoodItemId = food.FoodItemId,
                            Price = food.Price
                        };

                        _context.ReservedFoods.Add(reservedFood);
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new { 
                    reservationCode = reservation.ReservationCode,
                    message = "Reservation created successfully!" 
                });
                */
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    Success = false, 
                    Message = "An error occurred while reserving tickets",
                    Error = ex.Message
                });
            }
        }
    }
}
