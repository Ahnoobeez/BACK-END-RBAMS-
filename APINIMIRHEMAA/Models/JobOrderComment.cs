using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the job order comment record stored or used by the application.
    public class JobOrderComment : ICreatedByRecord
    {
        // Stores created by on this job order comment.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related job order comment record.
        public int JobOrderComment_ID { get; set; }
        // Identifies the related job order record.
        public int JobOrder_ID { get; set; }
        [Required, MaxLength(200)]
        // Supplies the employee name used to identify the related record.
        public string Employee_Name { get; set; } = "";
        [Required]
        // Free-text comment that records context or explanations for this job order comment.
        public string Comment { get; set; } = "";
        // Stores created at on this job order comment.
        public DateTime CreatedAt { get; set; }
    }
}
