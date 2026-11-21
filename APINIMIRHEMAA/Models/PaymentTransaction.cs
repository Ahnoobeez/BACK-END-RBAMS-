using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APINIMIRHEMAA.Models
{
    // Represents the payment transaction record stored or used by the application.
    public class PaymentTransaction : ICreatedByRecord
    {
        [Key]
        // Identifies the related payment transaction record.
        public int PaymentTransaction_ID { get; set; }

        // Identifies the related collection receipt record.
        public int? CollectionReceipt_ID { get; set; }
        // Stores job order id on this payment transaction.
        public int JobOrderID { get; set; }
        // Identifies the related quotation record.
        public int Quotation_ID { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        // Financial amount recorded for this payment transaction.
        public decimal Amount { get; set; }

        [Column(TypeName = "datetime2(7)")]
        // Time value for paid at utc, using the UTC/time-zone convention indicated by its name.
        public DateTime PaidAtUtc { get; set; }

        // Stores created by on this payment transaction.
        public string? CreatedBy { get; set; }

        [JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }

        [JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }

        [JsonIgnore]
        // Stores collection receipt on this payment transaction.
        public CollectionReceipt? CollectionReceipt { get; set; }
    }
}
