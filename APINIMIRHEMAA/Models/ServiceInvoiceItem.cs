using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the service invoice item record stored or used by the application.
    public class ServiceInvoiceItem : ICreatedByRecord
    {
        // Stores created by on this service invoice item.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related service invoice item record.
        public int ServiceInvoiceItem_ID { get; set; }
        // Identifies the related service invoice record.
        public int ServiceInvoice_ID { get; set; }
        // Stores quantity on this service invoice item.
        public int Quantity { get; set; }
        // Stores store on this service invoice item.
        public string? Store { get; set; }
        // Stores requirement on this service invoice item.
        public string? Requirement { get; set; }
        [Required]
        [MaxLength(50)]
        // Stores unit on this service invoice item.
        public string Unit { get; set; } = string.Empty;
        [Required]
        // Free-text description that records context or explanations for this service invoice item.
        public string Description { get; set; } = string.Empty;
        // Financial unit price recorded for this service invoice item.
        public decimal Unit_Price { get; set; }
        // Financial amount recorded for this service invoice item.
        public decimal Amount { get; set; }
    }
}
