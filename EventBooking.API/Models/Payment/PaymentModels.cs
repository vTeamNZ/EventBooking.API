using System.ComponentModel.DataAnnotations;

namespace EventBooking.API.Models.Payment
{    public class CreatePaymentIntentResponse
    {
        public string ClientSecret { get; set; }
        public int BookingId { get; set; }
    }

    public class PaymentConfig
    {
        public string PublishableKey { get; set; }
    }
}
