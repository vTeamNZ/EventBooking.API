using System;

namespace EventBooking.API.Services
{
    public interface IEventStatusService
    {
        DateTime GetCurrentNZTime();
        bool IsEventActive(DateTime? eventDate);
        bool IsEventExpired(DateTime? eventDate);
    }
}
