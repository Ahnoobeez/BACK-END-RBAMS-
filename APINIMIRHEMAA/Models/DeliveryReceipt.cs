using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the delivery receipt record stored or used by the application.
    public class DeliveryReceipt : ICreatedByRecord
    {
        // Stores created by on this delivery receipt.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related delivery record.
        public int Delivery_ID { get; set; }
        // Stores job order id on this delivery receipt.
        public int? JobOrderID { get; set; }
        // Supplies the title used to identify the related record.
        public string? Title { get; set; }
        // Supplies the client name used to identify the related record.
        public string? ClientName { get; set; }
        // Stores address on this delivery receipt.
        public string? Address { get; set; }
        // Stores terms on this delivery receipt.
        public string? Terms { get; set; }
        // Date associated with date for this delivery receipt.
        public DateOnly? date { get; set; }
        // Time associated with time for this delivery receipt.
        public TimeOnly? time { get; set; }
        // Current workflow status for this delivery receipt.
        public string? Status { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        // Current workflow status for this delivery receipt.
        public string? ApprovalStatus { get; set; }

    }
}
