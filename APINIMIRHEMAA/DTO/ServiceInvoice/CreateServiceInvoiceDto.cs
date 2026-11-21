namespace APINIMIRHEMAA.DTO.ServiceInvoice
{
    // Defines the create service invoice request body exchanged through the API.
    public sealed class CreateServiceInvoiceDto
    {
        // Identifies the related job order record.
        public int JobOrder_ID { get; init; }
        // Carries branch for the related API operation.
        public string Branch { get; init; } = string.Empty;
        // Supplies the title used to identify the related record.
        public string Title { get; init; } = string.Empty;
        // Supplies the client name used to identify the related record.
        public string ClientName { get; init; } = string.Empty;
        // Carries address for the related API operation.
        public string Address { get; init; } = string.Empty;
        // Carries terms for the related API operation.
        public string Terms { get; init; } = string.Empty;
        // Records the invoice date for this operation.
        public DateOnly Invoice_Date { get; init; }
        // Carries items for the related API operation.
        public List<CreateServiceInvoiceItemDto> Items { get; init; } = new();
    }

    // Defines the create service invoice item request body exchanged through the API.
    public sealed class CreateServiceInvoiceItemDto
    {
        // Carries quantity for the related API operation.
        public int Quantity { get; init; }
        // Carries unit for the related API operation.
        public string Unit { get; init; } = string.Empty;
        // Carries store for the related API operation.
        public string Store { get; init; } = string.Empty;
        // Carries requirement for the related API operation.
        public string Requirement { get; init; } = string.Empty;
        // Free-text description that records context or explanations for this create service invoice item dto.
        public string Description { get; init; } = string.Empty;
        // Financial unit price recorded for this create service invoice item dto.
        public decimal Unit_Price { get; init; }
        // Financial amount recorded for this create service invoice item dto.
        public decimal Amount { get; init; }
    }
}
