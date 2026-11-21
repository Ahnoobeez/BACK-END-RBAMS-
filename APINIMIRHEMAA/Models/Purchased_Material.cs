using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    // Represents the purchased material record stored or used by the application.
    public class Purchased_Material : ICreatedByRecord
    {
        // Stores created by on this purchased material.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related purchased record.
        public int Purchased_ID { get; set; }
        // Records the date received item for this operation.
        public DateOnly Date_Received_Item { get; set; }
        // Records the si date for this operation.
        public DateOnly SI_Date { get; set; }
        // Stores si no on this purchased material.
        public int SI_No { get; set; }
        // Supplies the supplier name used to identify the related record.
        public string Supplier_Name { get; set; }
        // Free-text remarks that records context or explanations for this purchased material.
        public string Remarks { get; set; }
        // Stores quantity on this purchased material.
        public int Quantity { get; set; }
        // Financial unit price recorded for this purchased material.
        public int Unit_price { get; set; }
        // Financial amount recorded for this purchased material.
        public int Amount { get; set; }
        // Identifies the related material record.
        public int Material_ID { get; set; }
        [NotMapped]
        // Indicates whether locked by material requisition applies to this purchased material.
        public bool IsLockedByMaterialRequisition { get; set; }
    }
}
