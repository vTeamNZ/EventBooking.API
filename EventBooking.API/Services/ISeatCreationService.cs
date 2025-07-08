using EventBooking.API.Models;

namespace EventBooking.API.Services
{
    public interface ISeatCreationService
    {
        /// <summary>
        /// Creates seats for an event based on its venue configuration
        /// </summary>
        /// <param name="eventId">The ID of the event</param>
        /// <param name="venueId">The ID of the venue</param>
        /// <returns>The number of seats created</returns>
        Task<int> CreateSeatsForEventAsync(int eventId, int venueId);
    }
}