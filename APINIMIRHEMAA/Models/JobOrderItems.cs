using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the job order items record stored or used by the application.
    public class JobOrderItems : ICreatedByRecord
    {
        // Stores created by on this job order items.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related job order items record.
        public int JobOrderItems_ID { get; set; }
        // Identifies the related job order record.
        public int JobOrder_ID { get; set; }
        // Identifies the related qi record.
        public int? QI_ID { get; set; }
        // Stores store on this job order items.
        public string? Store { get; set; }
        // Stores requirements on this job order items.
        public string? Requirements { get; set; }
        // Free-text description that records context or explanations for this job order items.
        public string? Description { get; set; }
        // Stores quantity on this job order items.
        public int? Quantity { get; set; }
        // Stores width on this job order items.
        public string? Width { get; set; }
        // Stores length on this job order items.
        public string? Length { get; set; }
        // Stores artist initial on this job order items.
        public string? Artist_Initial { get; set; }
        // Stores production initial on this job order items.
        public string? Production_Initial { get; set; }
        // Free-text remarks that records context or explanations for this job order items.
        public string? Remarks { get; set; }

    }
}
