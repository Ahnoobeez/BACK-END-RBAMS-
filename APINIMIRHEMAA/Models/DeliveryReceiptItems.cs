using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the delivery receipt items record stored or used by the application.
    public class DeliveryReceiptItems : ICreatedByRecord
    {
        // Stores created by on this delivery receipt items.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related delivery receipt items record.
        public int DeliveryReceiptItems_ID { get; set; }
        // Stores requirement on this delivery receipt items.
        public string? Requirement { get; set; }
        // Free-text description that records context or explanations for this delivery receipt items.
        public string? Description { get; set; }
        // Stores store on this delivery receipt items.
        public string? Store { get; set; }
        // Stores quantity on this delivery receipt items.
        public int? Quantity { get; set; }
        // Identifies the related delivery record.
        public int Delivery_ID { get; set; }

    }
}
