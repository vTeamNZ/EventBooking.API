namespace EventBooking.API.Models.Payment
{
    public class PaymentStatusResponse
    {
        public string Status { get; set; }
        public bool IsSuccessful { get; set; }
        public string ReceiptEmail { get; set; }
        public long Amount { get; set; }
    }
}
