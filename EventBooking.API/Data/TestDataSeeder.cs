using EventBooking.API.Models;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.API.Data
{
    public static class TestDataSeeder
    {
        public static async Task SeedTestData(AppDbContext context)
        {
            // Check if we already have ticket types
            if (await context.TicketTypes.AnyAsync())
            {
                return;
            }

            // Add some test ticket types for an event (assuming event ID 1 exists)
            var ticketTypes = new List<TicketType>
            {
                new TicketType
                {
                    EventId = 1,
                    Type = "Regular",
                    Price = 50.00m,
                    Description = "Regular entry ticket"
                },
                new TicketType
                {
                    EventId = 1,
                    Type = "VIP",
                    Price = 100.00m,
                    Description = "VIP ticket with special perks"
                },
                new TicketType
                {
                    EventId = 1,
                    Type = "Student",
                    Price = 35.00m,
                    Description = "Student discount ticket (ID required)"
                }
            };

            await context.TicketTypes.AddRangeAsync(ticketTypes);

            // Add some test food items
            if (!await context.FoodItems.AnyAsync())
            {
                var foodItems = new List<FoodItem>
                {
                    new FoodItem
                    {
                        EventId = 1,
                        Name = "Vegetarian Meal",
                        Description = "Fresh vegetarian meal with salad",
                        Price = 15.00m
                    },
                    new FoodItem
                    {
                        EventId = 1,
                        Name = "Chicken Meal",
                        Description = "Grilled chicken with rice and vegetables",
                        Price = 18.00m
                    },
                    new FoodItem
                    {
                        EventId = 1,
                        Name = "Fish and Chips",
                        Description = "Fresh fish with potato chips",
                        Price = 20.00m
                    }
                };

                await context.FoodItems.AddRangeAsync(foodItems);
            }

            await context.SaveChangesAsync();
        }
    }
}
