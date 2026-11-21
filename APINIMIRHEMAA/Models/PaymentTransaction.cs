using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace APINIMIRHEMAA.Models
{

    public class PaymentTransaction : ICreatedByRecord
    {
        [Key]

        public int PaymentTransaction_ID { get; set; }


        public int? CollectionReceipt_ID { get; set; }

        public int JobOrderID { get; set; }

        public int Quotation_ID { get; set; }

        [Column(TypeName = "decimal(18,2)")]

        public decimal Amount { get; set; }

        [Column(TypeName = "datetime2(7)")]

        public DateTime PaidAtUtc { get; set; }


        public string? CreatedBy { get; set; }

        [JsonIgnore]

        public int? CreatedByUserId { get; set; }

        [JsonIgnore]

        public int? CreatedByDepartmentId { get; set; }

        [JsonIgnore]

        public CollectionReceipt? CollectionReceipt { get; set; }
    }
}
