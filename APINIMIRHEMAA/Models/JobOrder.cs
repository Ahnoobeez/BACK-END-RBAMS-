using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    // Represents the job order record stored or used by the application.
    public class JobOrder : ICreatedByRecord
    {
        // Stores created by on this job order.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related job order record.
        public int JobOrder_ID { get; set; }
        // Stores conforme file id on this job order.
        public int Conforme_FileID { get; set; }
        // Supplies the title used to identify the related record.
        public string? Title { get; set; }
        // Records the installation date for this operation.
        public DateOnly? Installation_Date { get; set; }
        // Stores target delivery on this job order.
        public DateOnly? Target_Delivery { get; set; }
        // Records the date delivered for this operation.
        public DateOnly? Date_Delivered { get; set; }
        // Stores tiling on this job order.
        public bool? Tiling { get; set; }
        // Stores eyelet on this job order.
        public bool? Eyelet { get; set; }
        // Stores bleeding on this job order.
        public bool? Bleeding { get; set; }
        // Current workflow status for this job order.
        public string? Status { get; set; }
        // Stores production completed on this job order.
        public bool Production_Completed { get; set; }
        // Stores technical completed on this job order.
        public bool Technical_Completed { get; set; }
        // Free-text comments that records context or explanations for this job order.
        public List<JobOrderComment> Comments { get; set; } = new();
        [NotMapped]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        // Supplies the client name used to identify the related record.
        public string? Client_Name { get; set; }
        [NotMapped]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        // Stores contact person on this job order.
        public string? Contact_Person { get; set; }
        [NotMapped]
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        // Stores available delivery receipt branches on this job order.
        public List<string>? AvailableDeliveryReceiptBranches { get; set; }

    }
}
