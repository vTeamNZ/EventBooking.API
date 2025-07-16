using EventBooking.API.Models;

namespace EventBooking.API.Services
{
    public interface IProcessingFeeService
    {
        /// <summary>
        /// Calculate processing fee for an order amount
        /// </summary>
        /// <param name="orderAmount">The base order amount (before processing fee)</param>
        /// <param name="processingFeePercentage">The percentage fee (e.g., 0.027 for 2.7%)</param>
        /// <param name="processingFeeFixedAmount">The fixed fee amount (e.g., 0.40)</param>
        /// <returns>The calculated processing fee amount</returns>
        decimal CalculateProcessingFee(decimal orderAmount, decimal processingFeePercentage, decimal processingFeeFixedAmount);

        /// <summary>
        /// Calculate processing fee for an event
        /// </summary>
        /// <param name="orderAmount">The base order amount (before processing fee)</param>
        /// <param name="eventData">The event containing processing fee configuration</param>
        /// <returns>The calculated processing fee amount, or 0 if processing fee is disabled</returns>
        decimal CalculateProcessingFee(decimal orderAmount, Event eventData);

        /// <summary>
        /// Calculate total amount including processing fee
        /// </summary>
        /// <param name="orderAmount">The base order amount (before processing fee)</param>
        /// <param name="eventData">The event containing processing fee configuration</param>
        /// <returns>Object containing breakdown of amounts</returns>
        ProcessingFeeCalculation CalculateTotalWithProcessingFee(decimal orderAmount, Event eventData);
    }

    public class ProcessingFeeCalculation
    {
        public decimal OrderAmount { get; set; }
        public decimal ProcessingFeeAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public bool ProcessingFeeApplied { get; set; }
        public string ProcessingFeeDescription { get; set; } = string.Empty;
    }

    public class ProcessingFeeService : IProcessingFeeService
    {
        public decimal CalculateProcessingFee(decimal orderAmount, decimal processingFeePercentage, decimal processingFeeFixedAmount)
        {
            if (orderAmount <= 0) return 0;

            // Convert percentage to decimal (e.g., 2.7% becomes 0.027)
            var percentageAsDecimal = processingFeePercentage / 100;
            
            // Calculate: (orderAmount × percentage) + fixedAmount
            var percentageFee = orderAmount * percentageAsDecimal;
            var totalFee = percentageFee + processingFeeFixedAmount;

            return Math.Round(totalFee, 2, MidpointRounding.AwayFromZero);
        }

        public decimal CalculateProcessingFee(decimal orderAmount, Event eventData)
        {
            if (!eventData.ProcessingFeeEnabled || orderAmount <= 0)
                return 0;

            return CalculateProcessingFee(orderAmount, eventData.ProcessingFeePercentage, eventData.ProcessingFeeFixedAmount);
        }

        public ProcessingFeeCalculation CalculateTotalWithProcessingFee(decimal orderAmount, Event eventData)
        {
            var calculation = new ProcessingFeeCalculation
            {
                OrderAmount = orderAmount,
                ProcessingFeeApplied = eventData.ProcessingFeeEnabled
            };

            if (eventData.ProcessingFeeEnabled && orderAmount > 0)
            {
                calculation.ProcessingFeeAmount = CalculateProcessingFee(orderAmount, eventData);
                calculation.TotalAmount = orderAmount + calculation.ProcessingFeeAmount;

                // Simple description without calculation details
                calculation.ProcessingFeeDescription = "Processing Fee";
            }
            else
            {
                calculation.ProcessingFeeAmount = 0;
                calculation.TotalAmount = orderAmount;
                calculation.ProcessingFeeDescription = "";
            }

            return calculation;
        }
    }
}
