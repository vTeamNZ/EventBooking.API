using EventBooking.API.Models;

namespace EventBooking.API.Services
{
    public interface IVenueSeatMappingService
    {
        /// <summary>
        /// Determines the appropriate SeatSelectionMode based on venue layout type
        /// </summary>
        /// <param name="layoutType">The venue's layout type</param>
        /// <returns>The appropriate SeatSelectionMode</returns>
        SeatSelectionMode GetSeatSelectionModeForLayout(string layoutType);
        
        /// <summary>
        /// Checks if a venue layout type supports allocated seating
        /// </summary>
        /// <param name="layoutType">The venue's layout type</param>
        /// <returns>True if the layout supports allocated seating</returns>
        bool SupportsAllocatedSeating(string layoutType);
    }
    
    public class VenueSeatMappingService : IVenueSeatMappingService
    {
        private readonly Dictionary<string, SeatSelectionMode> _layoutMapping = new()
        {
            { "Theater", SeatSelectionMode.EventHall },
            { "Classroom", SeatSelectionMode.EventHall },
            { "Banquet", SeatSelectionMode.EventHall },
            { "Allocated Seating", SeatSelectionMode.EventHall },
            { "General Admission", SeatSelectionMode.GeneralAdmission },
            { "Standing Room", SeatSelectionMode.GeneralAdmission },
            { "Festival", SeatSelectionMode.GeneralAdmission }
        };
        
        public SeatSelectionMode GetSeatSelectionModeForLayout(string layoutType)
        {
            return _layoutMapping.GetValueOrDefault(layoutType, SeatSelectionMode.GeneralAdmission);
        }
        
        public bool SupportsAllocatedSeating(string layoutType)
        {
            return GetSeatSelectionModeForLayout(layoutType) == SeatSelectionMode.EventHall;
        }
    }
}
