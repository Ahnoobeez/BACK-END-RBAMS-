using APINIMIRHEMAA.Models;
using InstallationReportModel = APINIMIRHEMAA.Models.InstallationReport;

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
        public ClientEntity? Client { get; init; }
        public List<Conforme> Conformes { get; init; } = new();
        public List<PurchaseOrder> PurchaseOrders { get; init; } = new();
        public List<ProjectViewerJobOrderDto> JobOrders { get; init; } = new();
        public List<ProjectViewerDeliveryReceiptDto> DeliveryReceipts { get; init; } = new();
        public List<CollectionReceipt> CollectionReceipts { get; init; } = new();
        public List<ServiceInvoice> ServiceInvoices { get; init; } = new();
        public List<InstallationReportModel> InstallationReports { get; init; } = new();
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
        public int QuotationItemId { get; init; }
        public int QuotationStoreId { get; init; }
        public string? Store { get; init; }
        public string? Requirement { get; init; }
        public string? Description { get; init; }
        public decimal? Width { get; init; }
        public decimal? Height { get; init; }
        public string? MeasurementUnit { get; init; }
        public decimal? Quantity { get; init; }
        public decimal? PricePerSquareInch { get; init; }
        public decimal? UnitPrice { get; init; }
        public decimal? TotalAmount { get; init; }
        public List<int> MaterialIds { get; init; } = new();
    }
}
