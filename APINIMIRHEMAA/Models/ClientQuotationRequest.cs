using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    public sealed class ClientQuotationRequest
    {
        [Key]
        public int Request_ID { get; set; }

        [Required]
        public string Client_Name { get; set; } = string.Empty;

        [Required]
        public string Client_Telephone { get; set; } = string.Empty;

        [Required]
        public string Contact_Person { get; set; } = string.Empty;

        [Required]
        public string Business_Style { get; set; } = string.Empty;

        [Required]
        public string Client_EmailAddress { get; set; } = string.Empty;

        [Required]
        public string TermsAndCondition { get; set; } = string.Empty;

        [Required]
        public string Company_Address { get; set; } = string.Empty;

        [Required]
        public string TIN { get; set; } = string.Empty;

        [Required]
        public string Payment_Terms { get; set; } = string.Empty;

        [Required]
        public string Project_Title { get; set; } = string.Empty;

        [Required]
        public string Items_Json { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Sub_Total { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Delivery_Charge { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Installation_Charge { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal VAT { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Overall_Total { get; set; }

        public DateTime SubmittedAtUtc { get; set; }

        [Required]
        [MaxLength(32)]
        public string Status { get; set; } = "Pending";

        public int? ClaimedByUserId { get; set; }

        public DateTime? ClaimedAtUtc { get; set; }

        public int? Client_ID { get; set; }

        public int? Quotation_ID { get; set; }
    }
}
