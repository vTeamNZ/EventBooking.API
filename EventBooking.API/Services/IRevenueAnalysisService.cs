using EventBooking.API.DTOs;

namespace EventBooking.API.Services
{
    /// <summary>
    /// Service for advanced revenue analysis and reporting
    /// </summary>
    public interface IRevenueAnalysisService
    {
        /// <summary>
        /// Get comprehensive revenue analysis for an event
        /// </summary>
        Task<EventRevenueAnalysisDTO> GetEventRevenueAnalysisAsync(int eventId);
        
        /// <summary>
        /// Get revenue trends over time for an organizer
        /// </summary>
        Task<List<MonthlyRevenueTrendDTO>> GetOrganizerRevenueTrendsAsync(int organizerId, int months = 12);
        
        /// <summary>
        /// Compare revenue performance across multiple events
        /// </summary>
        Task<List<EventRevenueComparisonDTO>> CompareEventRevenueAsync(List<int> eventIds);
        
        /// <summary>
        /// Get revenue breakdown by ticket types for an event
        /// </summary>
        Task<List<TicketTypeRevenueDTO>> GetTicketTypeRevenueBreakdownAsync(int eventId);
        
        /// <summary>
        /// Get payment method distribution analysis
        /// </summary>
        Task<List<PaymentMethodAnalysisDTO>> GetPaymentMethodAnalysisAsync(int eventId);
        
        /// <summary>
        /// Get outstanding payment analysis for an organizer
        /// </summary>
        Task<OutstandingPaymentAnalysisDTO> GetOutstandingPaymentAnalysisAsync(int organizerId);
    }
}