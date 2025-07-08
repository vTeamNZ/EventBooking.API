using EventBooking.API.Models;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.API.Data
{
    public static class TestDataSeeder
    {
        public static async Task SeedTestData(AppDbContext context)
        {
            // Seeding disabled: No test or mock data will be inserted.
        }
    }
}
