using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the audit trail record stored or used by the application.
    public class AuditTrail
    {
        [Key]
        // Identifies the related audit trail record.
        public int AuditTrail_ID { get; set; }
        // Supplies the name used to identify the related record.
        public string? Name { get; set; }
        // Records the date for this operation.
        public DateOnly? Date { get; set; }
        // Records the time for this operation.
        public TimeOnly? Time { get; set; }
        // Stores action on this audit trail.
        public string? Action { get; set; }
        // Stores department on this audit trail.
        public string? Department { get; set; }
    }
}
