using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EventBooking.API.Models.Payment
{
    public class CreatePaymentIntentRequest
    {
        [Required]
        public long Amount { get; set; }

        [Required]
        public string Currency { get; set; } = "nzd";

        [Required]
        public int EventId { get; set; }

        [Required]
        public string EventTitle { get; set; }

        // Customer details are optional during payment intent creation
        // They will be collected during payment confirmation
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [StringLength(100)]
        public string? FirstName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [StringLength(100)]
        public string? LastName { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [EmailAddress]
        [StringLength(255)]
        public string? Email { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [Phone]
        [StringLength(20)]
        public string? Mobile { get; set; }

        [Required]
        public string Description { get; set; }

        [Required]
        public string TicketDetails { get; set; }

        public string? FoodDetails { get; set; }
    }
}
