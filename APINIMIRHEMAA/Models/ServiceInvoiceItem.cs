using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class ServiceInvoiceItem : ICreatedByRecord
    {
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        public int ServiceInvoiceItem_ID { get; set; }
        public int ServiceInvoice_ID { get; set; }
        public int Quantity { get; set; }
        [Required]
        [MaxLength(50)]
        public string Unit { get; set; } = string.Empty;
        [Required]
        public string Description { get; set; } = string.Empty;
        public decimal Unit_Price { get; set; }
        public decimal Amount { get; set; }
    }
}
