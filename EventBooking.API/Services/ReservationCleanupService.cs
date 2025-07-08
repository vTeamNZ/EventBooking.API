using EventBooking.API.Data;
using Microsoft.EntityFrameworkCore;

namespace EventBooking.API.Services
{
    public class ReservationCleanupService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<ReservationCleanupService> _logger;
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(1);

        public ReservationCleanupService(
            IServiceProvider services,
            ILogger<ReservationCleanupService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Reservation Cleanup Service is starting");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RemoveExpiredReservations();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while cleaning up expired reservations");
                }

                await Task.Delay(_cleanupInterval, stoppingToken);
            }
        }

        private async Task RemoveExpiredReservations()
        {
            using var scope = _services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.UtcNow;
            var expiredReservations = await dbContext.SeatReservations
                .Where(r => !r.IsConfirmed && r.ExpiresAt < now)
                .ToListAsync();

            if (expiredReservations.Any())
            {
                _logger.LogInformation("Found {Count} expired reservations to remove", expiredReservations.Count);
                dbContext.SeatReservations.RemoveRange(expiredReservations);
                await dbContext.SaveChangesAsync();
                _logger.LogInformation("Successfully removed {Count} expired reservations", expiredReservations.Count);
            }
        }
    }
}
