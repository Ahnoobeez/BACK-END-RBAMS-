namespace APINIMIRHEMAA.DTO.ServiceInvoice
{
    public sealed class CreateServiceInvoiceDto
    {
        public int JobOrder_ID { get; init; }
        public DateOnly Invoice_Date { get; init; }
        public List<CreateServiceInvoiceItemDto> Items { get; init; } = new();
    }

    public sealed class CreateServiceInvoiceItemDto
    {
        public int Quantity { get; init; }
        public string Unit { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public decimal Unit_Price { get; init; }
        public decimal Amount { get; init; }
    }
}
