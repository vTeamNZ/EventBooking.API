using EventBooking.API.Models.Payment;
using Microsoft.Extensions.Options;

namespace EventBooking.API.Services
{
    public interface IAfterPayFeeService
    {
        /// <summary>
        /// Calculate AfterPay fee for an order amount
        /// </summary>
        /// <param name="orderAmount">The base order amount (before AfterPay fee)</param>
        /// <returns>The calculated AfterPay fee amount</returns>
        decimal CalculateAfterPayFee(decimal orderAmount);

        /// <summary>
        /// Calculate total amount including AfterPay fee
        /// </summary>
        /// <param name="orderAmount">The base order amount (before AfterPay fee)</param>
        /// <returns>Object containing breakdown of amounts</returns>
        AfterPayFeeCalculation CalculateTotalWithAfterPayFee(decimal orderAmount);

        /// <summary>
        /// Check if AfterPay is enabled in configuration
        /// </summary>
        /// <returns>True if AfterPay is enabled</returns>
        bool IsAfterPayEnabled();

        /// <summary>
        /// Get AfterPay fee settings
        /// </summary>
        /// <returns>AfterPay fee settings</returns>
        AfterPayFeeSettings GetAfterPaySettings();
    }

    public class AfterPayFeeCalculation
    {
        public decimal OrderAmount { get; set; }
        public decimal AfterPayFeeAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public bool AfterPayFeeApplied { get; set; }
        public string AfterPayFeeDescription { get; set; } = string.Empty;
    }

    public class AfterPayFeeService : IAfterPayFeeService
    {
        private readonly AfterPayFeeSettings _settings;

        public AfterPayFeeService(IOptions<AfterPayFeeSettings> settings)
        {
            _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
        }

        public decimal CalculateAfterPayFee(decimal orderAmount)
        {
            if (!_settings.Enabled || orderAmount <= 0) return 0;

            // AfterPay fee = (percentage * order amount) + fixed amount
            // Example: 6% + $0.30 = (0.06 * $100) + $0.30 = $6.30
            var percentageFee = orderAmount * (_settings.Percentage / 100);
            var totalFee = percentageFee + _settings.FixedAmount;
            
            return Math.Round(totalFee, 2);
        }

        public AfterPayFeeCalculation CalculateTotalWithAfterPayFee(decimal orderAmount)
        {
            var afterPayFeeAmount = CalculateAfterPayFee(orderAmount);
            
            return new AfterPayFeeCalculation
            {
                OrderAmount = orderAmount,
                AfterPayFeeAmount = afterPayFeeAmount,
                TotalAmount = orderAmount + afterPayFeeAmount,
                AfterPayFeeApplied = _settings.Enabled && afterPayFeeAmount > 0,
                AfterPayFeeDescription = _settings.Description
            };
        }

        public bool IsAfterPayEnabled()
        {
            return _settings.Enabled;
        }

        public AfterPayFeeSettings GetAfterPaySettings()
        {
            return _settings;
        }
    }
}
