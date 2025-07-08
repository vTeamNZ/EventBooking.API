using EventBooking.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventBooking.API.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedTestData(AppDbContext context)
        {
            // Seeding disabled: No test or mock data will be inserted.
        }
    }
}
