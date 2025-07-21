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
    [Route("[controller]")]
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
        public async Task<ActionResult<IEnumerable<object>>> GetOrganizers()
        {
            var list = await _context.Organizers.Include(o => o.User).ToListAsync();
            
            // Transform the list to avoid circular reference issues
            var result = list.Select(organizer => new
            {
                organizer.Id,
                organizer.Name,
                organizer.ContactEmail,
                organizer.PhoneNumber,
                organizer.OrganizationName,
                organizer.Website,
                organizer.FacebookUrl,
                organizer.YoutubeUrl,
                organizer.IsVerified,
                organizer.CreatedAt,
                UserName = organizer.User?.UserName,
                FullName = organizer.User?.FullName
            });
            
            return Ok(result);
        }

        // GET: api/Organizers/5
        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<ActionResult<object>> GetOrganizer(int id)
        {
            var organizer = await _context.Organizers
                                          .Include(o => o.User)
                                          .FirstOrDefaultAsync(o => o.Id == id);

            if (organizer == null)
            {
                return NotFound();
            }

            // Return an anonymous object with only the needed properties to avoid circular reference issues
            return new
            {
                organizer.Id,
                organizer.Name,
                organizer.ContactEmail,
                organizer.PhoneNumber,
                organizer.OrganizationName,
                organizer.Website,
                organizer.FacebookUrl,
                organizer.YoutubeUrl,
                organizer.IsVerified,
                organizer.CreatedAt,
                UserName = organizer.User?.UserName,
                FullName = organizer.User?.FullName
            };
        }

        // PUT: api/Organizers/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [Authorize(Roles = "Admin,Organizer")] // ? SECURITY FIX: Only admins and organizers can update organizer info
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
        [Authorize(Roles = "Admin")] // ? SECURITY FIX: Only admins can create new organizers
        [HttpPost]
        public async Task<ActionResult<Organizer>> PostOrganizer(Organizer organizer)
        {
            _context.Organizers.Add(organizer);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOrganizer), new { id = organizer.Id }, organizer);
        }

        // DELETE: api/Organizers/5
        [Authorize(Roles = "Admin")] // ? SECURITY FIX: Only admins can delete organizers
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
