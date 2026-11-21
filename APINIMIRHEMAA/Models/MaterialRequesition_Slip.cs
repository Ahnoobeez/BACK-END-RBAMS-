using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the material requesition slip record stored or used by the application.
    public class MaterialRequesition_Slip : ICreatedByRecord
    {
        // Stores created by on this material requesition slip.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related mr record.
        public int MR_ID { get; set; }
        // Identifies the related material record.
        public int Material_ID { get; set; }
        // Supplies the control number used to identify the related record.
        public string? Control_Number { get; set; }
        // Records the date for this operation.
        public DateOnly? Date { get; set; }
        // Stores from who on this material requesition slip.
        public string? FromWho { get; set; }
        // Stores to who on this material requesition slip.
        public string ToWho { get; set; }
        // Supplies the material name used to identify the related record.
        public string? Material_Name { get; set; }
        // Supplies the project name used to identify the related record.
        public string? Project_Name { get; set; }
        // Stores supplier on this material requesition slip.
        public string? Supplier { get; set; }
        // Identifies the related purchased record.
        public int? Purchased_ID { get; set; }
        // Stores quantity on this material requesition slip.
        public int? Quantity { get; set; }
        // Financial unit cost recorded for this material requesition slip.
        public int? Unit_Cost { get; set; }
        // Financial total cost recorded for this material requesition slip.
        public int? Total_Cost { get; set; }
        // Free-text noted by that records context or explanations for this material requesition slip.
        public string? Noted_By { get; set; }
        // Stores approved by on this material requesition slip.
        public string? Approved_By { get; set; }
        // Stores submmited by on this material requesition slip.
        public string? Submmited_By { get; set; }
        // Stores received by on this material requesition slip.
        public string? Received_By { get; set; }
        // Current workflow status for this material requesition slip.
        public string? Status { get; set; }


    }
}
