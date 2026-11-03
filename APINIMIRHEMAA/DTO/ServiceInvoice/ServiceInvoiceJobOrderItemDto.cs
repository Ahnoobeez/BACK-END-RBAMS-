namespace APINIMIRHEMAA.DTO.ServiceInvoice
{
    public sealed class ServiceInvoiceJobOrderItemDto
    {
        public int? Quantity { get; init; }
        public string Unit { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public decimal Unit_Price { get; init; }
        public decimal Amount { get; init; }
    }
}
