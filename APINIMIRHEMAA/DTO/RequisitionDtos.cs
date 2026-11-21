namespace APINIMIRHEMAA.DTO
{
    // Defines the requisition preview response or transfer shape exchanged through the API.
    public class RequisitionPreviewDto
    {
        // Identifies the related material record.
        public int Material_ID { get; set; }
        // Identifies the related purchased record.
        public int? Purchased_ID { get; set; }
        // Carries quantity for the related API operation.
        public int Quantity { get; set; }
        // Financial unit cost recorded for this requisition preview dto.
        public int Unit_Cost { get; set; }
        // Carries supplier for the related API operation. This value is optional.
        public string? Supplier { get; set; }
        // Carries from who for the related API operation. This value is optional.
        public string? FromWho { get; set; }
        // Carries to who for the related API operation. This value is optional.
        public string? ToWho { get; set; }
    }
}