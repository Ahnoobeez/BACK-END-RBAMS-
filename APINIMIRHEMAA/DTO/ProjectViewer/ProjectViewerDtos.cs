using APINIMIRHEMAA.Models;
using ClientsProjectModel = APINIMIRHEMAA.Models.ClientsProject;

namespace APINIMIRHEMAA.DTO.ProjectViewer
{
    public sealed class ProjectViewerQuotationOptionDto
    {
        public int QuotationId { get; init; }
        public string? CompanyName { get; init; }
        public string? Title { get; init; }
    }

    public sealed class ProjectViewerQuotationDetailsDto
    {
        public Quotation? Quotation { get; init; }
        public ClientsProjectModel? Project { get; init; }
        public ClientEntity? Client { get; init; }
        public List<Conforme> Conformes { get; init; } = new();
        public List<PurchaseOrder> PurchaseOrders { get; init; } = new();
        public List<ProjectViewerJobOrderDto> JobOrders { get; init; } = new();
        public List<ProjectViewerDeliveryReceiptDto> DeliveryReceipts { get; init; } = new();
        public List<CollectionReceipt> CollectionReceipts { get; init; } = new();
        public List<Quotation_Store> QuotationStores { get; init; } = new();
        public List<Quotation_Items> QuotationItems { get; init; } = new();
        public List<ProjectViewerQuotationItemDto> Items { get; init; } = new();
    }

    public sealed class ProjectViewerJobOrderDto
    {
        public JobOrder Record { get; init; } = null!;
        public List<JobOrderItems> Items { get; init; } = new();
    }

    public sealed class ProjectViewerDeliveryReceiptDto
    {
        public DeliveryReceipt Record { get; init; } = null!;
        public List<DeliveryReceiptItems> Items { get; init; } = new();
        public List<DeliveryFiles> Files { get; init; } = new();
    }

    public sealed class ProjectViewerQuotationItemDto
    {
        public string? Store { get; init; }
        public string? Requirement { get; init; }
        public string? Description { get; init; }
        public decimal? Width { get; init; }
        public decimal? Height { get; init; }
        public decimal? Quantity { get; init; }
        public decimal? UnitPrice { get; init; }
        public decimal? TotalAmount { get; init; }
    }
}
