namespace APINIMIRHEMAA.DTO.AuditTrail
{
    public class AuditTrailDetailsDTO
    {
        public string Entity { get; set; } = "";
        public string? RecordId { get; set; }
        public string Operation { get; set; } = "";
        public string DocumentLabel { get; set; } = "";
        public string? ActionDescription { get; set; }
        public string? BatchId { get; set; }
        public Dictionary<string, string?>? PreviousValues { get; set; }
        public Dictionary<string, string?>? CurrentValues { get; set; }
        public List<AuditTrailFormRecordDTO> FormRecords { get; set; } = new();
    }

    public class AuditTrailFormRecordDTO
    {
        public string Entity { get; set; } = "";
        public string? RecordId { get; set; }
        public string Operation { get; set; } = "";
        public Dictionary<string, string?>? PreviousValues { get; set; }
        public Dictionary<string, string?>? CurrentValues { get; set; }
    }

    public class AuditTrailEntryDTO
    {
        public int AuditTrail_ID { get; set; }
        public string? Name { get; set; }
        public DateOnly? Date { get; set; }
        public TimeOnly? Time { get; set; }
        public string? Action { get; set; }
        public string? Department { get; set; }
        public AuditTrailDetailsDTO? Details { get; set; }
    }
}
