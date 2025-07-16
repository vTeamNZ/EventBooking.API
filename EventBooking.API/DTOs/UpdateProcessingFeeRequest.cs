using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.DTOs
{
    public class UpdateProcessingFeeRequest
    {
        [Range(0, 100, ErrorMessage = "Processing fee percentage must be between 0 and 100")]
        public decimal ProcessingFeePercentage { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Processing fee fixed amount must be non-negative")]
        public decimal ProcessingFeeFixedAmount { get; set; }

        public bool ProcessingFeeEnabled { get; set; }
    }
}
