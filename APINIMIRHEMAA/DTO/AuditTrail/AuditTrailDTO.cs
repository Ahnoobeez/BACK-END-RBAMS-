using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.DTO.AuditTrail
{
    // Defines the audit trail response or transfer shape exchanged through the API.
    public class AuditTrailDTO

    {
        [Key]
        // Identifies the related audit trail record.
        public int AuditTrail_ID { get; set; }
        // Supplies the name used to identify the related record.
        public string Name { get; set; }
        // Records the date for this operation.
        public DateOnly Date { get; set; }
        // Records the time for this operation.
        public TimeOnly Time { get; set; }
        // Carries action for the related API operation.
        public string Action { get; set; }
        // Carries department for the related API operation.
        public string Department { get; set; }
    }
}
