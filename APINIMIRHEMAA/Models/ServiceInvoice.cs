using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    // Represents the service invoice record stored or used by the application.
    public class ServiceInvoice : ICreatedByRecord
    {
        // Stores created by on this service invoice.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related service invoice record.
        public int ServiceInvoice_ID { get; set; }
        // Identifies the related job order record.
        public int? JobOrder_ID { get; set; }
        // Stores branch on this service invoice.
        public string? Branch { get; set; }
        // Supplies the title used to identify the related record.
        public string? Title { get; set; }
        // Supplies the client name used to identify the related record.
        public string? ClientName { get; set; }
        // Stores address on this service invoice.
        public string? Address { get; set; }
        // Stores terms on this service invoice.
        public string? Terms { get; set; }
        // Records the invoice date for this operation.
        public DateOnly Invoice_Date { get; set; }
        // Stores quantity on this service invoice.
        public int? Quantity { get; set; }
        // Stores unit on this service invoice.
        public string? Unit { get; set; }
        // Free-text description that records context or explanations for this service invoice.
        public string? Description { get; set; }
        // Financial unit price recorded for this service invoice.
        public decimal? Unit_Price { get; set; }
        // Financial amount recorded for this service invoice.
        public decimal? Amount { get; set; }
        // Current workflow status for this service invoice.
        public string? Status { get; set; }

        [NotMapped]
        // Display or business invoice number used to identify this service invoice.
        public string Invoice_Number => $"SI-{ServiceInvoice_ID:D6}";
    }
}
