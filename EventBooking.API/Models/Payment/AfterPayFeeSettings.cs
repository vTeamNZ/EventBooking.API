namespace EventBooking.API.Models.Payment
{
    public class AfterPayFeeSettings
    {
        public bool Enabled { get; set; } = true;
        public decimal Percentage { get; set; } = 6.0m;
        public decimal FixedAmount { get; set; } = 0.30m;
        public string Currency { get; set; } = "NZD";
        public string Description { get; set; } = "AfterPay processing fee";
    }
}
