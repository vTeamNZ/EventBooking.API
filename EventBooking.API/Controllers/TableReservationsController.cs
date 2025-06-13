using EventBooking.API.Data;
using EventBooking.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EventBooking.API.Controllers
{
    [Authorize(Roles = "Admin,Attendee")]
    //[AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class TableReservationsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TableReservationsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/TableReservations
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TableReservation>>> GetTableReservations()
        {
            return await _context.TableReservations.ToListAsync();
        }

        // GET: api/TableReservations/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TableReservation>> GetTableReservation(int id)
        {
            var tableReservation = await _context.TableReservations.FindAsync(id);

            if (tableReservation == null)
            {
                return NotFound();
            }

            return tableReservation;
        }

        // PUT: api/TableReservations/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTableReservation(int id, TableReservation tableReservation)
        {
            if (id != tableReservation.Id)
            {
                return BadRequest();
            }

            _context.Entry(tableReservation).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TableReservationExists(id))
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

        // POST: api/TableReservations
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        //[Authorize]
        [HttpPost]
        public async Task<ActionResult<TableReservation>> PostTableReservation(TableReservation tableReservation)
        {
            _context.TableReservations.Add(tableReservation);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTableReservation", new { id = tableReservation.Id }, tableReservation);
        }

        // DELETE: api/TableReservations/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTableReservation(int id)
        {
            var tableReservation = await _context.TableReservations.FindAsync(id);
            if (tableReservation == null)
            {
                return NotFound();
            }

            _context.TableReservations.Remove(tableReservation);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool TableReservationExists(int id)
        {
            return _context.TableReservations.Any(e => e.Id == id);
        }
    }
}
