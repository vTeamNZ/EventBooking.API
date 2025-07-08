using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventBooking.API.Data;
using EventBooking.API.Models;
using EventBooking.API.DTOs;
using EventBooking.API.Services;
using System.Text.Json;

namespace EventBooking.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VenuesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEventStatusService _eventStatusService;

        public VenuesController(AppDbContext context, IEventStatusService eventStatusService)
        {
            _context = context;
            _eventStatusService = eventStatusService;
        }

        // GET: api/venues
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Venue>>> GetVenues()
        {
            return await _context.Venues.ToListAsync();
        }

        // GET: api/venues/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Venue>> GetVenue(int id)
        {
            var venue = await _context.Venues.FindAsync(id);

            if (venue == null)
            {
                return NotFound();
            }

            return venue;
        }

        // POST: api/venues
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<Venue>> CreateVenue(VenueDTO venueDto)
        {
            var venue = new Venue
            {
                Name = venueDto.Name,
                Description = venueDto.Description,
                Address = venueDto.Address,
                City = venueDto.City,
                LayoutType = venueDto.LayoutType,
                LayoutData = venueDto.LayoutData,
                Width = venueDto.Width,
                Height = venueDto.Height,
                NumberOfRows = venueDto.NumberOfRows,
                SeatsPerRow = venueDto.SeatsPerRow,
                RowSpacing = venueDto.RowSpacing,
                SeatSpacing = venueDto.SeatSpacing,
                HasStaggeredSeating = venueDto.HasStaggeredSeating,
                HasWheelchairSpaces = venueDto.HasWheelchairSpaces,
                WheelchairSpaces = venueDto.WheelchairSpaces,
                HasHorizontalAisles = venueDto.HasHorizontalAisles,
                HorizontalAisleRows = venueDto.HorizontalAisleRows,
                HasVerticalAisles = venueDto.HasVerticalAisles,
                VerticalAisleSeats = venueDto.VerticalAisleSeats,
                AisleWidth = venueDto.AisleWidth,
                SeatSelectionMode = venueDto.SeatSelectionMode
            };

            _context.Venues.Add(venue);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetVenue), new { id = venue.Id }, venue);
        }

        // PUT: api/venues/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateVenue(int id, VenueUpdateDTO venueDto)
        {
            if (id != venueDto.Id)
            {
                return BadRequest();
            }

            var venue = await _context.Venues.FindAsync(id);
            if (venue == null)
            {
                return NotFound();
            }

            // Check if seat selection mode is being changed
            if (venue.SeatSelectionMode != venueDto.SeatSelectionMode)
            {
                if (await HasActiveEvents(id))
                {
                    return BadRequest("Cannot change seat selection mode while venue has active events");
                }
            }

            venue.Name = venueDto.Name;
            venue.Description = venueDto.Description;
            venue.Address = venueDto.Address;
            venue.City = venueDto.City;
            venue.LayoutType = venueDto.LayoutType;
            venue.LayoutData = venueDto.LayoutData;
            venue.Width = venueDto.Width;
            venue.Height = venueDto.Height;
            venue.NumberOfRows = venueDto.NumberOfRows;
            venue.SeatsPerRow = venueDto.SeatsPerRow;
            venue.RowSpacing = venueDto.RowSpacing;
            venue.SeatSpacing = venueDto.SeatSpacing;
            venue.HasStaggeredSeating = venueDto.HasStaggeredSeating;
            venue.HasWheelchairSpaces = venueDto.HasWheelchairSpaces;
            venue.WheelchairSpaces = venueDto.WheelchairSpaces;
            venue.HasHorizontalAisles = venueDto.HasHorizontalAisles;
            venue.HorizontalAisleRows = venueDto.HorizontalAisleRows;
            venue.HasVerticalAisles = venueDto.HasVerticalAisles;
            venue.VerticalAisleSeats = venueDto.VerticalAisleSeats;
            venue.AisleWidth = venueDto.AisleWidth;
            venue.SeatSelectionMode = venueDto.SeatSelectionMode;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!VenueExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }

        // PUT: api/venues/5/seat-selection-mode
        [HttpPut("{id}/seat-selection-mode")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateSeatSelectionMode(int id, SeatSelectionMode mode)
        {
            var venue = await _context.Venues.FindAsync(id);
            if (venue == null)
            {
                return NotFound();
            }

            // Check if seat selection mode is being changed
            if (venue.SeatSelectionMode != mode)
            {
                if (await HasActiveEvents(id))
                {
                    return BadRequest("Cannot change seat selection mode while venue has active events");
                }
            }

            venue.SeatSelectionMode = mode;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: api/venues/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteVenue(int id)
        {
            var venue = await _context.Venues.FindAsync(id);
            if (venue == null)
            {
                return NotFound();
            }

            // Check if venue has any associated events
            bool hasEvents = await _context.Events.AnyAsync(e => e.VenueId == id);
            if (hasEvents)
            {
                return BadRequest("Cannot delete venue with associated events");
            }

            _context.Venues.Remove(venue);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool VenueExists(int id)
        {
            return _context.Venues.Any(e => e.Id == id);
        }

        private async Task<bool> HasActiveEvents(int venueId)
        {
            var venue = await _context.Venues
                .Include(v => v.Events)
                .FirstOrDefaultAsync(v => v.Id == venueId);

            if (venue == null || !venue.Events.Any())
                return false;

            return venue.Events.Any(e => e.IsActive && _eventStatusService.IsEventActive(e.Date));
        }
    }
}
