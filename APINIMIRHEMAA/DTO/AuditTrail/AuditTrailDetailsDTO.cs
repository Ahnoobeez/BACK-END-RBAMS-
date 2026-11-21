namespace APINIMIRHEMAA.DTO.AuditTrail
{
    // Defines the audit trail details response or transfer shape exchanged through the API.
    public class AuditTrailDetailsDTO
    {
        // Carries entity for the related API operation.
        public string Entity { get; set; } = "";
        // Identifies the related record record.
        public string? RecordId { get; set; }
        // Carries operation for the related API operation.
        public string Operation { get; set; } = "";
        // Carries document label for the related API operation.
        public string DocumentLabel { get; set; } = "";
        // Free-text action description that records context or explanations for this audit trail details dto.
        public string? ActionDescription { get; set; }
        // Identifies the related batch record.
        public string? BatchId { get; set; }
        // Carries previous values for the related API operation.
        public Dictionary<string, string?>? PreviousValues { get; set; }
        // Carries current values for the related API operation.
        public Dictionary<string, string?>? CurrentValues { get; set; }
        // Carries form records for the related API operation.
        public List<AuditTrailFormRecordDTO> FormRecords { get; set; } = new();
    }

    // Defines the audit trail form record response or transfer shape exchanged through the API.
    public class AuditTrailFormRecordDTO
    {
        // Carries entity for the related API operation.
        public string Entity { get; set; } = "";
        // Identifies the related record record.
        public string? RecordId { get; set; }
        // Carries operation for the related API operation.
        public string Operation { get; set; } = "";
        // Carries previous values for the related API operation.
        public Dictionary<string, string?>? PreviousValues { get; set; }
        // Carries current values for the related API operation.
        public Dictionary<string, string?>? CurrentValues { get; set; }
    }

    // Defines the audit trail entry response or transfer shape exchanged through the API.
    public class AuditTrailEntryDTO
    {
        // Identifies the related audit trail record.
        public int AuditTrail_ID { get; set; }
        // Supplies the name used to identify the related record.
        public string? Name { get; set; }
        // Records the date for this operation.
        public DateOnly? Date { get; set; }
        // Records the time for this operation.
        public TimeOnly? Time { get; set; }
        // Carries action for the related API operation. This value is optional.
        public string? Action { get; set; }
        // Carries department for the related API operation. This value is optional.
        public string? Department { get; set; }
        // Free-text details that records context or explanations for this audit trail entry dto.
        public AuditTrailDetailsDTO? Details { get; set; }
    }
}
