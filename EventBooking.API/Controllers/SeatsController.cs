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
    // Temporarily allowing anonymous access for testing
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class SeatsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SeatsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Seats/event/{eventId}/layout
        [HttpGet("event/{eventId}/layout")]
        public async Task<ActionResult<SeatLayoutResponse>> GetEventSeatLayout(int eventId)
        {
            Console.WriteLine($"========== GetEventSeatLayout Called for eventId: {eventId} ==========");
            Console.WriteLine($"Request: {Request.Method} {Request.Path} from {Request.Headers["Origin"]}");
            Console.WriteLine($"User-Agent: {Request.Headers["User-Agent"]}");
            
            try
            {
                var eventEntity = await _context.Events
                    .Include(e => e.Venue)
                        .ThenInclude(v => v!.Sections)
                    .Include(e => e.Seats)
                        .ThenInclude(s => s.Section)
                    .Include(e => e.Tables)
                        .ThenInclude(t => t.Section)
                    .Include(e => e.Tables)
                        .ThenInclude(t => t.Seats)
                    .FirstOrDefaultAsync(e => e.Id == eventId);

                if (eventEntity == null)
                {
                    Console.WriteLine($"Event with ID {eventId} not found");
                    return NotFound();
                }
                
                Console.WriteLine($"Event found: {eventEntity.Title}, SeatSelectionMode: {eventEntity.SeatSelectionMode}");
                Console.WriteLine($"Venue: {(eventEntity.Venue != null ? eventEntity.Venue.Name : "None")}");
                Console.WriteLine($"Seats count: {eventEntity.Seats.Count}");
                Console.WriteLine($"Tables count: {eventEntity.Tables.Count}");
                Console.WriteLine($"Stage position: {eventEntity.StagePosition ?? "None"}");
                
                if (eventEntity.SeatSelectionMode != Models.SeatSelectionMode.EventHall)
                {
                    Console.WriteLine($"WARNING: Event has SeatSelectionMode {eventEntity.SeatSelectionMode} but this API was called");
                }

                // Clear expired reservations
                await ClearExpiredReservations(eventId);

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
                    AisleWidth = eventEntity.Venue?.AisleWidth ?? 2
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

                // Add sections
                if (eventEntity.Venue?.Sections != null)
                {
                    response.Sections = eventEntity.Venue.Sections.Select(s => new SectionDTO
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Color = s.Color,
                        BasePrice = s.BasePrice
                    }).ToList();
                }

                // Add seats
                Console.WriteLine($"Processing {eventEntity.Seats.Count} seats for response");
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
                    Price = s.Price,
                    Status = s.Status,
                    SectionId = s.SectionId,
                    TableId = s.TableId,
                    ReservedUntil = s.ReservedUntil
                }).ToList();

                // Add tables
                response.Tables = eventEntity.Tables.Select(t => new TableDTO
                {
                    Id = t.Id,
                    TableNumber = t.TableNumber,
                    Capacity = t.Capacity,
                    X = t.X,
                    Y = t.Y,
                    Width = t.Width,
                    Height = t.Height,
                    Shape = t.Shape,
                    PricePerSeat = t.PricePerSeat,
                    TablePrice = t.TablePrice,
                    SectionId = t.SectionId,
                    AvailableSeats = t.Seats.Count(s => s.Status == SeatStatus.Available),
                    Seats = t.Seats.Select(s => new SeatDTO
                    {
                        Id = s.Id,
                        SeatNumber = s.SeatNumber,
                        Row = s.Row,
                        Number = s.Number,
                        X = s.X,
                        Y = s.Y,
                        Width = s.Width,
                        Height = s.Height,
                        Price = s.Price,
                        Status = s.Status,
                        SectionId = s.SectionId,
                        TableId = s.TableId,
                        ReservedUntil = s.ReservedUntil
                    }).ToList()
                }).ToList();

                Console.WriteLine($"Returning response with mode: {response.Mode}");
                Console.WriteLine($"Response has {response.Seats.Count} seats");
                Console.WriteLine($"Response has {response.Tables?.Count ?? 0} tables");
                Console.WriteLine($"Response has {response.Sections?.Count ?? 0} sections");
                Console.WriteLine($"========== End of GetEventSeatLayout for eventId: {eventId} ==========");

                return Ok(response);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in GetEventSeatLayout: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return StatusCode(500, new { message = "An error occurred while processing the seat layout" });
            }
        }

        // POST: api/Seats/reserve
        [HttpPost("reserve")]
        public async Task<ActionResult> ReserveSeat([FromBody] ReserveSeatRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Use explicit locking with UPDLOCK to prevent concurrent reservations
                var seat = await _context.Seats
                    .FromSqlRaw("SELECT * FROM Seats WITH (UPDLOCK) WHERE Id = {0}", request.SeatId)
                    .FirstOrDefaultAsync();
                
                if (seat == null)
                    return NotFound("Seat not found");

                // Double-check the status after getting the lock
                if (seat.Status != SeatStatus.Available)
                    return BadRequest("Seat is not available");

                // Check if there's any existing reservation
                // TODO: Fix reservation model compatibility
                /*
                var existingReservation = await _context.Reservations
                    .AnyAsync(r => r.SeatId == request.SeatId && 
                                 (r.Status == "Reserved" || r.Status == "Booked"));
                
                if (existingReservation)
                    return BadRequest("Seat is already reserved or booked");
                */

                // Reserve seat for 10 minutes
                seat.Status = SeatStatus.Reserved;
                seat.ReservedUntil = DateTime.UtcNow.AddMinutes(10);
                seat.ReservedBy = request.SessionId;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { 
                    message = "Seat reserved successfully", 
                    reservedUntil = seat.ReservedUntil,
                    seatNumber = seat.SeatNumber,
                    price = seat.Price
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "An error occurred while reserving the seat", error = ex.Message });
            }
        }

        // POST: api/Seats/reserve-table
        [HttpPost("reserve-table")]
        public async Task<ActionResult> ReserveTable([FromBody] ReserveTableRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Use explicit locking to prevent concurrent reservations
                var table = await _context.Tables
                    .FromSqlRaw(@"
                        SELECT t.* 
                        FROM Tables t WITH (UPDLOCK)
                        WHERE t.Id = {0}", request.TableId)
                    .Include(t => t.Seats)
                    .FirstOrDefaultAsync();
                
                if (table == null)
                    return NotFound("Table not found");

                List<Seat> seatsToReserve;
                var seatIds = request.FullTable 
                    ? table.Seats.Select(s => s.Id).ToList() 
                    : request.SeatIds;

                // Get all seats with a lock to prevent concurrent modifications
                var seats = await _context.Seats
                    .FromSqlRaw(@"
                        SELECT s.* 
                        FROM Seats s WITH (UPDLOCK) 
                        WHERE s.TableId = {0} 
                        AND s.Id IN ({1})",
                        request.TableId,
                        string.Join(",", seatIds))
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
                
                var totalPrice = seatsToReserve.Sum(s => s.Price);
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
        [HttpGet("pricing/{eventId}")]
        public async Task<ActionResult<PricingResponse>> GetEventPricing(int eventId)
        {
            var eventEntity = await _context.Events
                .Include(e => e.Venue)
                    .ThenInclude(v => v!.Sections)
                .Include(e => e.TicketTypes)
                .FirstOrDefaultAsync(e => e.Id == eventId);

            if (eventEntity == null)
                return NotFound();

            var response = new PricingResponse
            {
                EventId = eventId,
                Mode = eventEntity.SeatSelectionMode,
                SectionPricing = eventEntity.Venue?.Sections?.ToDictionary(s => s.Name, s => s.BasePrice) ?? new Dictionary<string, decimal>(),
                TicketTypes = eventEntity.TicketTypes.Select(tt => new TicketTypeDTO
                {
                    Id = tt.Id,
                    Name = tt.Type, // TicketType uses 'Type' property
                    Price = tt.Price,
                    Description = tt.Description ?? ""
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

        // GET: api/Seats
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Seat>>> GetSeats()
        {
            return await _context.Seats.ToListAsync();
        }

        // GET: api/Seats/5
        [HttpGet("{id}")]
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
        [HttpPost]
        public async Task<ActionResult<Seat>> PostSeat(Seat seat)
        {
            _context.Seats.Add(seat);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetSeat", new { id = seat.Id }, seat);
        }

        // DELETE: api/Seats/5
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
        public async Task<ActionResult<IEnumerable<Seat>>> GetSeatsByEventId(int eventId)
        {
            var seats = await _context.Seats
                .Where(s => s.EventId == eventId)
                .Include(s => s.Section)
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
                    s.Price,
                    Section = new
                    {
                        s.Section.Id,
                        s.Section.Name,
                        s.Section.Color
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
    }
}
