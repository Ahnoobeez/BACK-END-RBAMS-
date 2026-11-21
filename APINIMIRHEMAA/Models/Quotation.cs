using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    // Represents the quotation record stored or used by the application.
    public class Quotation : ICreatedByRecord
    {
        // Stores created by on this quotation.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related quotation record.
        public int Quotation_ID { get; set; }
        // Identifies the related client record.
        public int Client_ID { get; set; }
        // Stores client subject on this quotation.
        public string? Client_Subject { get; set; }
        [NotMapped]
        // Identifies the related client subject reservation record.
        public Guid? Client_SubjectReservation_ID { get; set; }
        [NotMapped]
        // Identifies the related revision of quotation record.
        public int? RevisionOfQuotation_ID { get; set; }
        // Supplies the title used to identify the related record.
        public string? Title { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        // Financial sub total recorded for this quotation.
        public decimal Sub_Total { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        // Stores delivery charge on this quotation.
        public decimal? Delivery_Charge { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        // Stores installation charge on this quotation.
        public decimal? Installation_Charge { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        // Stores vat on this quotation.
        public decimal? VAT { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        // Financial overall total recorded for this quotation.
        public decimal Overall_Total { get; set; }
        // Current workflow status for this quotation.
        public string? Status { get; set; }
        // Free-text comment that records context or explanations for this quotation.
        public string? Comment { get; set; }
        // Stores terms condition on this quotation.
        public string? Terms_Condition { get; set; }
        // Records the start date for this operation.
        public DateOnly Start_Date { get; set; }
        // Records the time for this operation.
        public TimeOnly Time { get; set; }

    }
}
