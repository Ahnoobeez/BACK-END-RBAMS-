using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the installation schedule record stored or used by the application.
    public class Installation_Schedule : ICreatedByRecord
    {
        // Stores created by on this installation schedule.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related installation record.
        public int Installation_ID { get; set; }
        // Records the time for this operation.
        public TimeOnly Time { get; set; }
        // Free-text remarks that records context or explanations for this installation schedule.
        public string Remarks { get; set; }
        // Stores mall hours on this installation schedule.
        public string Mall_Hours { get; set; }
        // Records the date for this operation.
        public DateOnly Date { get; set; }
        // Identifies the related job order record.
        public int JobOrder_ID { get; set; }

    }
}
