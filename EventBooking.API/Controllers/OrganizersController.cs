using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventBooking.API.Data;
using EventBooking.API.Models;
using Microsoft.AspNetCore.Authorization;
using System.Collections;

namespace EventBooking.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class OrganizersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OrganizersController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Organizers
        [AllowAnonymous]
        //[Authorize(Roles = "Admin,Organizer")]
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Organizer>>> GetOrganizers()
        {
            Console.WriteLine("Organizer API hit.");
            var list = await _context.Organizers.Include(o => o.User).ToListAsync();
            Console.WriteLine($"Found {list.Count} organizers.");
            return list;
        }

        // GET: api/Organizers/5
        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult<Organizer>> GetOrganizer(int id)
        {
            var organizer = await _context.Organizers
                                          .Include(o => o.User)
                                          .FirstOrDefaultAsync(o => o.Id == id);

            if (organizer == null)
            {
                return NotFound();
            }

            return organizer;
        }

        // PUT: api/Organizers/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AllowAnonymous]
        [HttpPut("{id}")]
        public async Task<IActionResult> PutOrganizer(int id, Organizer organizer)
        {
            if (id != organizer.Id)
            {
                return BadRequest();
            }

            _context.Entry(organizer).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!OrganizerExists(id))
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

        // POST: api/Organizers
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [AllowAnonymous]
        [HttpPost]
        public async Task<ActionResult<Organizer>> PostOrganizer(Organizer organizer)
        {
            _context.Organizers.Add(organizer);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOrganizer), new { id = organizer.Id }, organizer);
        }

        // DELETE: api/Organizers/5
        [AllowAnonymous]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrganizer(int id)
        {
            var organizer = await _context.Organizers.FindAsync(id);
            if (organizer == null)
            {
                return NotFound();
            }

            _context.Organizers.Remove(organizer);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool OrganizerExists(int id)
        {
            return _context.Organizers.Any(e => e.Id == id);
        }
    }
}
