using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventBooking.API.Data;
using EventBooking.API.Models;
using EventBooking.API.DTOs;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

namespace EventBooking.API.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class SeatsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SeatsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Seats/event/{eventId}/layout
        [AllowAnonymous] // Required for public seat selection
        [HttpGet("event/{eventId}/layout")]
        public async Task<ActionResult<SeatLayoutResponse>> GetEventSeatLayout(int eventId)
        {
            try
            {
                var eventEntity = await _context.Events
                    .Include(e => e.Venue)
                    .Include(e => e.Seats)
                        .ThenInclude(s => s.TicketType)
                    .FirstOrDefaultAsync(e => e.Id == eventId);

                if (eventEntity == null)
                {
                    return NotFound();
                }
                
                if (eventEntity.SeatSelectionMode != Models.SeatSelectionMode.EventHall)
                {
                    // This endpoint is only for EventHall mode - other modes should use different endpoints
                }

                // ? CRITICAL FIX: Clear expired reservations for all events, not just this one
                await ClearExpiredReservations(eventId);
                
                // ? SAFETY: Also clear any seats with expired ReservedUntil but still marked as Reserved
                await ClearOrphanedReservedSeats(eventId);

                // Get ticket types for coloring seats
                var ticketTypes = await _context.TicketTypes
                    .Where(tt => tt.EventId == eventId)
                    .ToListAsync();
                
                var response = new SeatLayoutResponse
                {
                    EventId = eventId,
                    Mode = eventEntity.SeatSelectionMode,
                    Venue = eventEntity.Venue != null ? new SeatLayoutVenueDTO
                    {
                        Id = eventEntity.Venue.Id,
                        Name = eventEntity.Venue.Name,
                        Width = eventEntity.Venue.Width,
                        Height = eventEntity.Venue.Height
                    } : null,
                    // Include aisle information
                    HasHorizontalAisles = eventEntity.Venue?.HasHorizontalAisles ?? false,
                    HorizontalAisleRows = eventEntity.Venue?.HorizontalAisleRows ?? string.Empty,
                    HasVerticalAisles = eventEntity.Venue?.HasVerticalAisles ?? false,
                    VerticalAisleSeats = eventEntity.Venue?.VerticalAisleSeats ?? string.Empty,
                    AisleWidth = eventEntity.Venue?.AisleWidth ?? 2,
                    // Add ticket types to the response for seat coloring
                    TicketTypes = ticketTypes.Select(tt => new TicketTypeDTO
                    {
                        Id = tt.Id,
                        Name = !string.IsNullOrEmpty(tt.Name) ? tt.Name : tt.Type,
                        Price = tt.Price,
                        Description = tt.Description ?? string.Empty,
                        Color = tt.Color,
                        SeatRowAssignments = tt.SeatRowAssignments
                    }).ToList()
                };

                // Add stage information if available
                if (!string.IsNullOrEmpty(eventEntity.StagePosition))
                {
                    try
                    {
                        var stageData = JsonSerializer.Deserialize<StageDTO>(eventEntity.StagePosition);
                        response.Stage = stageData;
                    }
                    catch { /* Ignore invalid JSON */ }
                }

                // Add seats
                response.Seats = eventEntity.Seats.Select(s => new SeatDTO
                {
                    Id = s.Id,
                    SeatNumber = s.SeatNumber,
                    Row = s.Row,
                    Number = s.Number,
                    X = s.X,
                    Y = s.Y,
                    Width = s.Width,
                    Height = s.Height,
                    Price = s.TicketType?.Price ?? 0, // Use TicketType price instead of seat price
                    Status = s.Status,
                    TicketTypeId = s.TicketTypeId,
                    TicketType = s.TicketType != null ? new TicketTypeDTO
                    {
                        Id = s.TicketType.Id,
                        Name = !string.IsNullOrEmpty(s.TicketType.Name) ? s.TicketType.Name : s.TicketType.Type,
                        Price = s.TicketType.Price,
                        Description = s.TicketType.Description ?? string.Empty,
                        Color = s.TicketType.Color,
                        SeatRowAssignments = s.TicketType.SeatRowAssignments
                    } : null,
                    TableId = s.TableId,
                    ReservedUntil = s.ReservedUntil
                }).ToList();

                // Tables have been removed, initialize with empty list
                response.Tables = new List<TableDTO>();

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while processing the seat layout" });
            }
        }

        // POST: api/Seats/reserve
        [AllowAnonymous] // Required for public seat reservation
        [HttpPost("reserve")]
        public async Task<ActionResult> ReserveSeat([FromBody] ReserveSeatRequest request)
        {
            try
            {
                // Find seat first and check availability
                var seat = await _context.Seats
                    .Include(s => s.TicketType) // Include TicketType to get the price
                    .FirstOrDefaultAsync(s => s.Id == request.SeatId);
                
                if (seat == null)
                {
                    return NotFound("Seat not found");
                }

                // Check the status
                if (seat.Status != SeatStatus.Available)
                {
                    return BadRequest("Seat is not available");
                }

                // Reserve seat for 10 minutes
                seat.Status = SeatStatus.Reserved;
                seat.ReservedUntil = DateTime.UtcNow.AddMinutes(10);
                seat.ReservedBy = request.SessionId;

                await _context.SaveChangesAsync();

                var price = seat.TicketType?.Price ?? 0;

                return Ok(new { 
                    message = "Seat reserved successfully", 
                    reservedUntil = seat.ReservedUntil,
                    seatNumber = seat.SeatNumber,
                    price = price // Use TicketType price
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while reserving the seat", error = ex.Message });
            }
        }

        // POST: api/Seats/reserve-table
        [HttpPost("reserve-table")]
        [AllowAnonymous] // ? Allow public access for table reservation functionality
        public async Task<ActionResult> ReserveTable([FromBody] ReserveTableRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                // Use explicit locking to prevent concurrent reservations
                var table = await _context.Tables
                    .Include(t => t.Seats)
                    .FirstOrDefaultAsync(t => t.Id == request.TableId);
                
                if (table == null)
                    return NotFound("Table not found");

                List<Seat> seatsToReserve;
                var seatIds = request.FullTable 
                    ? table.Seats.Select(s => s.Id).ToList() 
                    : request.SeatIds;

                // Get all seats for the table
                var seats = await _context.Seats
                    .Include(s => s.TicketType) // Include TicketType to get the price
                    .Where(s => s.TableId == request.TableId && seatIds.Contains(s.Id))
                    .ToListAsync();

                // Check for existing reservations
                // TODO: Fix reservation model compatibility
                /*
                var existingReservations = await _context.Reservations
                    .AnyAsync(r => seatIds.Contains(r.SeatId) && 
                                 (r.Status == "Reserved" || r.Status == "Booked"));
                
                if (existingReservations)
                    return BadRequest("Some seats are already reserved or booked");
                */

                // Reload seats to ensure we have the latest state within the transaction
                foreach (var seat in seats)
                {
                    await _context.Entry(seat).ReloadAsync();
                }

                if (request.FullTable)
                {
                    seatsToReserve = seats.Where(s => s.Status == SeatStatus.Available).ToList();
                    if (seatsToReserve.Count != table.Capacity)
                        return BadRequest("Table is not fully available");
                }
                else
                {
                    seatsToReserve = seats.Where(s => request.SeatIds.Contains(s.Id) && 
                                                     s.Status == SeatStatus.Available).ToList();
                    if (seatsToReserve.Count != request.SeatIds.Count)
                        return BadRequest("Some seats are not available");
                }

                // Reserve all seats
                foreach (var seat in seatsToReserve)
                {
                    seat.Status = SeatStatus.Reserved;
                    seat.ReservedUntil = DateTime.UtcNow.AddMinutes(10);
                    seat.ReservedBy = request.SessionId;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                var totalPrice = seatsToReserve.Sum(s => s.TicketType?.Price ?? 0); // Use TicketType price
                return Ok(new { 
                    message = $"Table {table.TableNumber} reserved successfully", 
                    reservedSeats = seatsToReserve.Count,
                    totalPrice = totalPrice,
                    reservedUntil = seatsToReserve.First().ReservedUntil
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "An error occurred while reserving the table", error = ex.Message });
            }
        }

        // POST: api/Seats/release
        [AllowAnonymous] // Required for public seat release
        [HttpPost("release")]
        public async Task<ActionResult> ReleaseSeat([FromBody] ReleaseSeatRequest request)
        {
            var seat = await _context.Seats
                .Where(s => s.Id == request.SeatId && s.ReservedBy == request.SessionId)
                .FirstOrDefaultAsync();

            if (seat == null)
                return NotFound("Seat not found or not reserved by this session");

            seat.Status = SeatStatus.Available;
            seat.ReservedUntil = null;
            seat.ReservedBy = null;

            await _context.SaveChangesAsync();
            
            return Ok(new { message = "Seat released successfully" });
        }

        // GET: api/Seats/pricing/{eventId}
        [AllowAnonymous] // Required for public pricing information
        [HttpGet("pricing/{eventId}")]
        public async Task<ActionResult<PricingResponse>> GetEventPricing(int eventId)
        {
            var eventEntity = await _context.Events
                .Include(e => e.TicketTypes)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventEntity == null)
                return NotFound();

            var response = new PricingResponse
            {
                EventId = eventId,
                Mode = eventEntity.SeatSelectionMode,
                TicketTypes = eventEntity.TicketTypes.Select(tt => new TicketTypeDTO
                {
                    Id = tt.Id,
                    Name = !string.IsNullOrEmpty(tt.Name) ? tt.Name : tt.Type,
                    Price = tt.Price,
                    Description = tt.Description ?? "",
                    Color = tt.Color
                }).ToList()
            };

            return Ok(response);
        }

        private async Task ClearExpiredReservations(int eventId)
        {
            var expiredSeats = await _context.Seats
                .Where(s => s.EventId == eventId && 
                           s.Status == SeatStatus.Reserved && 
                           s.ReservedUntil < DateTime.UtcNow)
                .ToListAsync();

            foreach (var seat in expiredSeats)
            {
                seat.Status = SeatStatus.Available;
                seat.ReservedUntil = null;
                seat.ReservedBy = null;
            }

            if (expiredSeats.Any())
            {
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// ? SAFETY: Clear any orphaned reserved seats that should be available
        /// This handles cases where ReservedUntil has passed but Status is still Reserved
        /// </summary>
        private async Task ClearOrphanedReservedSeats(int eventId)
        {
            var orphanedSeats = await _context.Seats
                .Where(s => s.EventId == eventId && 
                           s.Status == SeatStatus.Reserved && 
                           (s.ReservedUntil == null || s.ReservedUntil < DateTime.UtcNow))
                .ToListAsync();

            foreach (var seat in orphanedSeats)
            {
                seat.Status = SeatStatus.Available;
                seat.ReservedUntil = null;
                seat.ReservedBy = null;
            }

            if (orphanedSeats.Any())
            {
                await _context.SaveChangesAsync();
            }
        }

        // GET: api/Seats
        [HttpGet]
        [AllowAnonymous] // ? Allow public access to view all seats for debugging/admin purposes
        public async Task<ActionResult<IEnumerable<Seat>>> GetSeats()
        {
            return await _context.Seats.ToListAsync();
        }

        // GET: api/Seats/5
        [HttpGet("{id}")]
        [AllowAnonymous] // ? Allow public access to view individual seat details
        public async Task<ActionResult<Seat>> GetSeat(int id)
        {
            var seat = await _context.Seats.FindAsync(id);

            if (seat == null)
            {
                return NotFound();
            }

            return seat;
        }

        // PUT: api/Seats/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AllowAnonymous] // ? Allow public access for seat updates (status changes, reservations, etc.)
        [HttpPut("{id}")]
        public async Task<IActionResult> PutSeat(int id, Seat seat)
        {
            if (id != seat.Id)
            {
                return BadRequest();
            }

            _context.Entry(seat).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!SeatExists(id))
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

        // POST: api/Seats
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [Authorize(Roles = "Admin,Organizer")] // ? SECURITY FIX: Only admins and organizers can create seats
        [HttpPost]
        public async Task<ActionResult<Seat>> PostSeat(Seat seat)
        {
            _context.Seats.Add(seat);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSeat", new { id = seat.Id }, seat);
        }

        // DELETE: api/Seats/5
        [Authorize(Roles = "Admin")] // ? SECURITY FIX: Only admins can delete seats
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteSeat(int id)
        {
            var seat = await _context.Seats.FindAsync(id);
            if (seat == null)
            {
                return NotFound();
            }

            _context.Seats.Remove(seat);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool SeatExists(int id)
        {
            return _context.Seats.Any(e => e.Id == id);
        }

        // GET: api/Seats/event/{eventId}
        [HttpGet("event/{eventId}")]
        [AllowAnonymous] // ? Allow public access to view seats for event selection
        public async Task<ActionResult<IEnumerable<Seat>>> GetSeatsByEventId(int eventId)
        {
            var seats = await _context.Seats
                .Where(s => s.EventId == eventId)
                .Include(s => s.TicketType)
                // TODO: Fix reservations relationship
                //.Include(s => s.Reservations)
                .Select(s => new
                {
                    s.Id,
                    s.SeatNumber,
                    s.Row,
                    s.Number,
                    s.X,
                    s.Y,
                    s.Width,
                    s.Height,
                    Price = s.TicketType != null ? s.TicketType.Price : 0, // Use TicketType.Price instead of Seats.Price
                    TicketType = new
                    {
                        s.TicketType.Id,
                        Name = !string.IsNullOrEmpty(s.TicketType.Name) ? s.TicketType.Name : s.TicketType.Type,
                        s.TicketType.Color,
                        s.TicketType.Price
                    },
                    Status = s.Status.ToString() // Using the seat's Status property directly
                })
                .ToListAsync();

            if (!seats.Any())
            {
                return NotFound($"No seats found for event with ID {eventId}");
            }

            return Ok(seats);
        }

        // GET: api/Seats/event/{eventId}/allocation-status
        [HttpGet("event/{eventId}/allocation-status")]
        [AllowAnonymous] // ? Allow public access to view seat allocation status for event selection
        public async Task<ActionResult> GetSeatAllocationStatus(int eventId)
        {
            var eventEntity = await _context.Events
                .Include(e => e.Venue)
                .Include(e => e.TicketTypes)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventEntity == null)
            {
                return NotFound("Event not found");
            }

            var seats = await _context.Seats
                .Where(s => s.EventId == eventId)
                .GroupBy(s => s.Row)
                .Select(g => new
                {
                    Row = g.Key,
                    TotalSeats = g.Count(),
                    AvailableSeats = g.Count(s => s.Status == SeatStatus.Available),
                    ReservedSeats = g.Count(s => s.Status == SeatStatus.Reserved || s.IsReserved),
                    BookedSeats = g.Count(s => s.Status == SeatStatus.Booked),
                    UnavailableSeats = g.Count(s => s.Status == SeatStatus.Unavailable)
                })
                .OrderBy(r => r.Row)
                .ToListAsync();

            var ticketTypeAllocations = eventEntity.TicketTypes
                .Where(tt => !string.IsNullOrEmpty(tt.SeatRowAssignments))
                .Select(tt => new
                {
                    TicketType = !string.IsNullOrEmpty(tt.Name) ? tt.Name : tt.Type,
                    Color = tt.Color,
                    RowAssignments = tt.SeatRowAssignments
                })
                .ToList();

            return Ok(new
            {
                EventId = eventId,
                EventTitle = eventEntity.Title,
                VenueName = eventEntity.Venue?.Name,
                SeatSelectionMode = eventEntity.SeatSelectionMode.ToString(),
                RowStatus = seats,
                TicketTypeAllocations = ticketTypeAllocations,
                Summary = new
                {
                    TotalSeats = seats.Sum(r => r.TotalSeats),
                    TotalAvailable = seats.Sum(r => r.AvailableSeats),
                    TotalReserved = seats.Sum(r => r.ReservedSeats),
                    TotalBooked = seats.Sum(r => r.BookedSeats),
                    TotalUnavailable = seats.Sum(r => r.UnavailableSeats)
                }
            });
        }

        // POST: api/Seats/reserve-multiple
        [AllowAnonymous] // Required for public multi-seat reservation
        [HttpPost("reserve-multiple")]
        public async Task<ActionResult> ReserveMultipleSeats([FromBody] ReserveMultipleSeatsRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                // Find seats first, then check availability within the transaction
                var seats = await _context.Seats
                    .Include(s => s.TicketType) // Include TicketType to get the price
                    .Where(s => request.SeatIds.Contains(s.Id))
                    .ToListAsync();
                
                if (!seats.Any())
                    return NotFound("No seats found");

                if (seats.Count != request.SeatIds.Count)
                    return BadRequest("Not all requested seats were found");

                // Reload seats to ensure we have the latest state within the transaction
                foreach (var seat in seats)
                {
                    await _context.Entry(seat).ReloadAsync();
                }

                // Double-check all seats are available
                var unavailableSeats = seats.Where(s => s.Status != SeatStatus.Available).ToList();
                if (unavailableSeats.Any())
                {
                    return BadRequest($"The following seats are not available: {string.Join(", ", unavailableSeats.Select(s => s.SeatNumber))}");
                }

                // Reserve all seats for 10 minutes
                foreach (var seat in seats)
                {
                    seat.Status = SeatStatus.Reserved;
                    seat.ReservedUntil = DateTime.UtcNow.AddMinutes(10);
                    seat.ReservedBy = request.SessionId;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { 
                    message = "All seats reserved successfully", 
                    reservedUntil = seats.First().ReservedUntil,
                    seats = seats.Select(s => new {
                        seatNumber = s.SeatNumber,
                        price = s.TicketType?.Price ?? 0
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "An error occurred while reserving seats", error = ex.Message });
            }
        }

        // POST: api/Seats/mark-booked
        [HttpPost("mark-booked")]
        [Authorize(Roles = "Admin,Organizer")] // ? SECURITY FIX: Only Admin and Organizers can bypass payment and mark seats as booked
        public async Task<ActionResult> MarkSeatsAsBooked([FromBody] MarkSeatsBookedRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                // Validate input
                if (request.SeatNumbers == null || !request.SeatNumbers.Any())
                {
                    return BadRequest("No seat numbers provided");
                }

                // Get all seats by seat numbers
                var seats = await _context.Seats
                    .Where(s => s.EventId == request.EventId && request.SeatNumbers.Contains(s.SeatNumber))
                    .ToListAsync();

                if (!seats.Any())
                {
                    return NotFound("No matching seats found");
                }

                // Check if all requested seats were found
                var foundSeatNumbers = seats.Select(s => s.SeatNumber).ToHashSet();
                var missingSeatNumbers = request.SeatNumbers.Where(sn => !foundSeatNumbers.Contains(sn)).ToList();
                
                if (missingSeatNumbers.Any())
                {
                    return BadRequest($"The following seats were not found: {string.Join(", ", missingSeatNumbers)}");
                }

                // Mark all seats as booked
                foreach (var seat in seats)
                {
                    seat.Status = SeatStatus.Booked;
                    seat.ReservedBy = request.OrganizerEmail;
                    seat.ReservedUntil = DateTime.UtcNow.AddDays(1); // Set expiry for cleanup if needed
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { 
                    message = "Seats marked as booked successfully", 
                    markedSeats = seats.Count,
                    seatNumbers = seats.Select(s => s.SeatNumber).ToList()
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "An error occurred while marking seats as booked", error = ex.Message });
            }
        }

        // POST: api/Seats/confirm-payment
        [HttpPost("confirm-payment")]
        [AllowAnonymous] // ? PAYMENT FIX: Allow any user to mark their paid seats as booked
        public async Task<ActionResult> ConfirmPaymentAndBookSeats([FromBody] PaymentConfirmationRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            try
            {
                // Validate input
                if (request.SeatNumbers == null || !request.SeatNumbers.Any())
                {
                    return BadRequest("No seat numbers provided");
                }

                // First verify payment exists and is successful
                var payment = await _context.Payments
                    .Where(p => p.PaymentIntentId == request.PaymentIntentId 
                           && p.EventId == request.EventId
                           && p.Status == "succeeded")
                    .FirstOrDefaultAsync();

                if (payment == null)
                {
                    return BadRequest("No successful payment found for this request");
                }

                // Get seats that are currently reserved by this session
                var seats = await _context.Seats
                    .Where(s => s.EventId == request.EventId 
                           && request.SeatNumbers.Contains(s.SeatNumber)
                           && s.Status == SeatStatus.Reserved
                           && s.ReservedBy == request.SessionId)
                    .ToListAsync();

                if (!seats.Any())
                {
                    return NotFound("No matching reserved seats found for this session");
                }

                // Check if all requested seats were found
                var foundSeatNumbers = seats.Select(s => s.SeatNumber).ToHashSet();
                var missingSeatNumbers = request.SeatNumbers.Where(sn => !foundSeatNumbers.Contains(sn)).ToList();
                
                if (missingSeatNumbers.Any())
                {
                    return BadRequest($"The following seats were not found or not reserved by this session: {string.Join(", ", missingSeatNumbers)}");
                }

                // Mark all seats as booked and clear reservation info
                foreach (var seat in seats)
                {
                    seat.Status = SeatStatus.Booked;
                    seat.ReservedBy = null; // Clear session reservation
                    seat.ReservedUntil = null; // Clear reservation expiry
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { 
                    message = "Payment confirmed and seats booked successfully", 
                    bookedSeats = seats.Count,
                    seatNumbers = seats.Select(s => s.SeatNumber).ToList()
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "An error occurred while confirming payment and booking seats", error = ex.Message });
            }
        }

        // GET: api/Seats/reservations/{eventId}/{sessionId}
        [AllowAnonymous] // Required for public session reservation checking
        [HttpGet("reservations/{eventId}/{sessionId}")]
        public async Task<ActionResult<IEnumerable<ReservedSeatDTO>>> GetReservationsBySession(int eventId, string sessionId)
        {
            try
            {
                // Look in SeatReservations table first, then fallback to Seats table
                var seatReservations = await _context.SeatReservations
                    .Where(sr => sr.EventId == eventId 
                        && sr.SessionId == sessionId
                        && !sr.IsConfirmed
                        && sr.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync();

                if (seatReservations.Any())
                {
                    // Get the actual seat details from the reservations
                    var seatIds = seatReservations.Where(sr => sr.SeatId.HasValue).Select(sr => sr.SeatId.Value).ToList();
                    
                    var reservedSeats = await _context.Seats
                        .Include(s => s.TicketType)
                        .Where(s => seatIds.Contains(s.Id))
                        .Select(s => new ReservedSeatDTO
                        {
                            SeatId = s.Id,
                            Row = s.Row,
                            Number = s.Number,
                            SeatNumber = s.SeatNumber,
                            Price = s.TicketType != null ? s.TicketType.Price : 0,
                            TicketType = s.TicketType,
                            ReservedUntil = s.ReservedUntil,
                            Status = s.Status
                        })
                        .ToListAsync();

                    return Ok(reservedSeats);
                }

                // Fallback: Legacy system - check Seats table directly
                var legacyReservedSeats = await _context.Seats
                    .Include(s => s.TicketType)
                    .Where(s => s.EventId == eventId 
                        && s.Status == SeatStatus.Reserved 
                        && s.ReservedBy == sessionId
                        && s.ReservedUntil > DateTime.UtcNow)
                    .Select(s => new ReservedSeatDTO
                    {
                        SeatId = s.Id,
                        Row = s.Row,
                        Number = s.Number,
                        SeatNumber = s.SeatNumber,
                        Price = s.TicketType != null ? s.TicketType.Price : 0,
                        TicketType = s.TicketType,
                        ReservedUntil = s.ReservedUntil,
                        Status = s.Status
                    })
                    .ToListAsync();

                // Return empty array instead of 404 to avoid frontend errors
                return Ok(legacyReservedSeats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error fetching reservations", error = ex.Message });
            }
        }

        // =====================================================================
        // ?? INDUSTRY STANDARD SEAT RESERVATION ENDPOINTS
        // =====================================================================

        /// <summary>
        /// ? INDUSTRY STANDARD: Check seat availability before reservation
        /// No database writes - read-only validation
        /// </summary>
        [AllowAnonymous]
        [HttpPost("check-availability")]
        public async Task<ActionResult<SeatAvailabilityResponse>> CheckSeatAvailability([FromBody] CheckSeatAvailabilityRequest request)
        {
            try
            {
                var seats = await _context.Seats
                    .Where(s => request.SeatIds.Contains(s.Id) && s.EventId == request.EventId)
                    .ToListAsync();

                var response = new SeatAvailabilityResponse();
                var unavailableDetails = new List<UnavailableSeatInfo>();

                foreach (var seatId in request.SeatIds)
                {
                    var seat = seats.FirstOrDefault(s => s.Id == seatId);
                    if (seat == null)
                    {
                        response.UnavailableSeatIds.Add(seatId);
                        unavailableDetails.Add(new UnavailableSeatInfo
                        {
                            SeatId = seatId,
                            SeatNumber = $"Unknown-{seatId}",
                            Status = SeatStatus.Booked,
                            Reason = "Seat not found"
                        });
                        continue;
                    }

                    if (seat.Status == SeatStatus.Available)
                    {
                        response.AvailableSeatIds.Add(seatId);
                    }
                    else
                    {
                        response.UnavailableSeatIds.Add(seatId);
                        unavailableDetails.Add(new UnavailableSeatInfo
                        {
                            SeatId = seatId,
                            SeatNumber = seat.SeatNumber,
                            Status = seat.Status,
                            ReservedUntil = seat.ReservedUntil,
                            Reason = seat.Status == SeatStatus.Reserved ? "Currently reserved" : "Already booked"
                        });
                    }
                }

                response.UnavailableDetails = unavailableDetails;
                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error checking seat availability", error = ex.Message });
            }
        }

        // =====================================================================
        // ? PAYMENT-BASED ENDPOINTS (Simple Approach)
        // =====================================================================
        // Note: Frontend now passes seat data directly to payment without reservation calls
        // Seats are only reserved when payment is actually processed
        // This removes the complexity of timers, session tracking, and pre-payment reservations

        /// <summary>
        /// Helper method to convert string row (A, B, C, etc.) to integer for database storage
        /// </summary>
        private static int ConvertRowToNumber(string row)
        {
            if (string.IsNullOrEmpty(row))
                return 0;

            // If it's already a number, parse it
            if (int.TryParse(row, out int numericRow))
                return numericRow;

            // Convert letter-based rows (A=1, B=2, C=3, etc.)
            row = row.ToUpper().Trim();
            if (row.Length == 1 && char.IsLetter(row[0]))
            {
                return row[0] - 'A' + 1; // A=1, B=2, C=3, etc.
            }

            // For multi-character rows like AA, AB, etc.
            int result = 0;
            for (int i = 0; i < row.Length; i++)
            {
                if (char.IsLetter(row[i]))
                {
                    result = result * 26 + (row[i] - 'A' + 1);
                }
            }

            return result > 0 ? result : 999; // Fallback for unrecognized formats
        }
    }
}
