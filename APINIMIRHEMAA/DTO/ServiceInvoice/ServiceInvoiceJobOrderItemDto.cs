namespace APINIMIRHEMAA.DTO.ServiceInvoice
{
    // Defines the service invoice job order item response or transfer shape exchanged through the API.
    public sealed class ServiceInvoiceJobOrderItemDto
    {
        // Carries quantity for the related API operation. This value is optional.
        public int? Quantity { get; init; }
        // Carries unit for the related API operation.
        public string Unit { get; init; } = string.Empty;
        // Free-text description that records context or explanations for this service invoice job order item dto.
        public string Description { get; init; } = string.Empty;
        // Financial unit price recorded for this service invoice job order item dto.
        public decimal Unit_Price { get; init; }
        // Financial amount recorded for this service invoice job order item dto.
        public decimal Amount { get; init; }
    }
}
