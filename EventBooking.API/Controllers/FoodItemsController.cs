using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventBooking.API.Data;
using EventBooking.API.Models;
using Microsoft.AspNetCore.Authorization;

namespace EventBooking.API.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class FoodItemsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FoodItemsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/FoodItems/event/5
        [HttpGet("event/{eventId}")]
        [AllowAnonymous] // ✅ Allow public viewing of food items for menu display
        public async Task<ActionResult<IEnumerable<FoodItem>>> GetFoodItemsForEvent(int eventId)
        {
            var foodItems = await _context.FoodItems
                .Where(f => f.EventId == eventId)
                .ToListAsync();

            return foodItems;
        }

        // POST: api/FoodItems
        [HttpPost]
        [Authorize(Roles = "Admin,Organizer")] // ✅ SECURITY FIX: Only admins and organizers can create food items
        public async Task<ActionResult<FoodItem>> CreateFoodItem(FoodItem foodItem)
        {
            _context.FoodItems.Add(foodItem);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetFoodItemsForEvent), new { eventId = foodItem.EventId }, foodItem);
        }

        // PUT: api/FoodItems/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Organizer")] // ✅ SECURITY FIX: Only admins and organizers can update food items
        public async Task<IActionResult> UpdateFoodItem(int id, FoodItem foodItem)
        {
            if (id != foodItem.Id)
            {
                return BadRequest();
            }

            _context.Entry(foodItem).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!FoodItemExists(id))
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

        // DELETE: api/FoodItems/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Organizer")] // ✅ SECURITY FIX: Only admins and organizers can delete food items
        public async Task<IActionResult> DeleteFoodItem(int id)
        {
            var foodItem = await _context.FoodItems.FindAsync(id);
            if (foodItem == null)
            {
                return NotFound();
            }

            _context.FoodItems.Remove(foodItem);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool FoodItemExists(int id)
        {
            return _context.FoodItems.Any(e => e.Id == id);
        }
    }
}
