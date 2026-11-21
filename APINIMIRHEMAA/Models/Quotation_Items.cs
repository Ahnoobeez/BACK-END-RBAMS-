using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    // Represents the quotation items record stored or used by the application.
    public class Quotation_Items : ICreatedByRecord
    {
        // Stores created by on this quotation items.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related qi record.
        public int QI_ID { get; set; }
        // Stores requirements on this quotation items.
        public string? Requirements { get; set; }
        // Free-text description that records context or explanations for this quotation items.
        public string? Description { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        // Stores width on this quotation items.
        public decimal? Width { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        // Stores height on this quotation items.
        public decimal? Height { get; set; }
        // Stores measurement unit on this quotation items.
        public string? Measurement_Unit { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        // Stores quantity on this quotation items.
        public decimal? Quantity { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        // Financial price per square inch recorded for this quotation items.
        public decimal? Price_Per_Square_Inch { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        // Financial unit price recorded for this quotation items.
        public decimal? Unit_Price { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        // Financial total amount recorded for this quotation items.
        public decimal? Total_Amount { get; set; }
        // Identifies the related qs record.
        public int QS_ID { get; set; }
        [NotMapped]
        // Stores quotation store ids on this quotation items.
        public List<int> QuotationStoreIds { get; set; } = new();
        // Stores materials on this quotation items.
        public ICollection<QuotationItemMaterial> Materials { get; set; } = new List<QuotationItemMaterial>();
    }
}
