using System;

namespace EventBooking.API.Services
{
    public class EventStatusService : IEventStatusService
    {
        private readonly TimeZoneInfo _nzTimeZone;

        public EventStatusService()
        {
            try
            {
                // Try Windows timezone ID first
                _nzTimeZone = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
            }
            catch
            {
                try
                {
                    // Fallback for Linux/Mac systems
                    _nzTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");
                }
                catch
                {
                    // Ultimate fallback to UTC if NZ timezone not found
                    _nzTimeZone = TimeZoneInfo.Utc;
                }
            }
        }

        public DateTime GetCurrentNZTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _nzTimeZone);
        }

        public bool IsEventActive(DateTime? eventDate)
        {
            if (!eventDate.HasValue) return false;
            
            var currentNZTime = GetCurrentNZTime();
            // Same logic as EventsController: Event is active if it's today or in the future
            return eventDate.Value.Date >= currentNZTime.Date;
        }

        public bool IsEventExpired(DateTime? eventDate)
        {
            if (!eventDate.HasValue) return true;
            
            var currentNZTime = GetCurrentNZTime();
            // Event is expired if it's in the past
            return eventDate.Value.Date < currentNZTime.Date;
        }
    }
}
