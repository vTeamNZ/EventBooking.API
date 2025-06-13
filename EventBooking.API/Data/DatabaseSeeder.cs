using EventBooking.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventBooking.API.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedTestData(AppDbContext context)
        {
            // Start a transaction
            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                // Check if The Lankan Space event exists
                var lankanEvent = await context.Events
                    .FirstOrDefaultAsync(e => e.Title == "The Lankan Space");

                if (lankanEvent == null)
                {
                    // Create The Lankan Space event
                    lankanEvent = new Event
                    {
                        Title = "The Lankan Space",
                        Description = "Experience authentic Sri Lankan culture and cuisine",
                        Date = DateTime.Now.AddDays(30),
                        Location = "Auckland, New Zealand",
                        Capacity = 200,
                        Price = 35.00M,
                        ImageUrl = "/events/1.jpg",
                        IsActive = true
                    };
                    
                    context.Events.Add(lankanEvent);
                    await context.SaveChangesAsync();
                }

                // Check and add ticket types if they don't exist
                if (!await context.TicketTypes.AnyAsync(tt => tt.EventId == lankanEvent.Id))
                {
                    var ticketTypes = new List<TicketType>
                    {
                        new TicketType 
                        { 
                            EventId = lankanEvent.Id,
                            Type = "Regular",
                            Price = 50.00M,
                            Description = "Standard entry ticket"
                        },
                        new TicketType 
                        { 
                            EventId = lankanEvent.Id,
                            Type = "VIP",
                            Price = 100.00M,
                            Description = "VIP access with traditional welcome and premium seating"
                        },
                        new TicketType 
                        { 
                            EventId = lankanEvent.Id,
                            Type = "Student",
                            Price = 35.00M,
                            Description = "Discounted student ticket (requires valid student ID)"
                        }
                    };

                    context.TicketTypes.AddRange(ticketTypes);
                    await context.SaveChangesAsync();
                }

                // Check and add food items if they don't exist
                if (!await context.FoodItems.AnyAsync(fi => fi.EventId == lankanEvent.Id))
                {
                    var foodItems = new List<FoodItem>
                    {
                        new FoodItem 
                        { 
                            EventId = lankanEvent.Id,
                            Name = "Rice & Curry",
                            Price = 18.00M,
                            Description = "Traditional Sri Lankan rice with 3 vegetables, dhal curry, and papadam"
                        },
                        new FoodItem 
                        { 
                            EventId = lankanEvent.Id,
                            Name = "Kottu Roti",
                            Price = 20.00M,
                            Description = "Famous Sri Lankan street food made with chopped roti, vegetables, and your choice of chicken or vegetarian"
                        },
                        new FoodItem 
                        { 
                            EventId = lankanEvent.Id,
                            Name = "Watalappam",
                            Price = 6.00M,
                            Description = "Traditional Sri Lankan dessert made with jaggery, coconut milk, and aromatic spices"
                        }
                    };

                    context.FoodItems.AddRange(foodItems);
                    await context.SaveChangesAsync();
                }

                // Commit the transaction
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                // If there's an error, roll back the transaction
                await transaction.RollbackAsync();
                throw; // Re-throw the exception to be handled by the caller
            }
        }
    }
}
