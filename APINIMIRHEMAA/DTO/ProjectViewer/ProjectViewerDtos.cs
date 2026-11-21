using APINIMIRHEMAA.Models;
using InstallationReportModel = APINIMIRHEMAA.Models.InstallationReport;

namespace APINIMIRHEMAA.DTO.ProjectViewer
{
    // Defines the project viewer quotation option response or transfer shape exchanged through the API.
    public sealed class ProjectViewerQuotationOptionDto
    {
        // Identifies the related quotation record.
        public int QuotationId { get; init; }
        // Supplies the company name used to identify the related record.
        public string? CompanyName { get; init; }
        // Supplies the title used to identify the related record.
        public string? Title { get; init; }
    }

    // Defines the project viewer quotation details response or transfer shape exchanged through the API.
    public sealed class ProjectViewerQuotationDetailsDto
    {
        // Carries quotation for the related API operation. This value is optional.
        public Quotation? Quotation { get; init; }
        // Carries client for the related API operation. This value is optional.
        public ClientEntity? Client { get; init; }
        // Carries conformes for the related API operation.
        public List<Conforme> Conformes { get; init; } = new();
        // Carries purchase orders for the related API operation.
        public List<PurchaseOrder> PurchaseOrders { get; init; } = new();
        // Carries job orders for the related API operation.
        public List<ProjectViewerJobOrderDto> JobOrders { get; init; } = new();
        // Carries delivery receipts for the related API operation.
        public List<ProjectViewerDeliveryReceiptDto> DeliveryReceipts { get; init; } = new();
        // Carries collection receipts for the related API operation.
        public List<CollectionReceipt> CollectionReceipts { get; init; } = new();
        // Carries service invoices for the related API operation.
        public List<APINIMIRHEMAA.Models.ServiceInvoice> ServiceInvoices { get; init; } = new();
        // Carries service invoice items for the related API operation.
        public List<ServiceInvoiceItem> ServiceInvoiceItems { get; init; } = new();
        // Carries service invoice files for the related API operation.
        public List<ServiceInvoiceFile> ServiceInvoiceFiles { get; init; } = new();
        // Carries installation reports for the related API operation.
        public List<InstallationReportModel> InstallationReports { get; init; } = new();
        // Carries quotation stores for the related API operation.
        public List<Quotation_Store> QuotationStores { get; init; } = new();
        // Carries quotation items for the related API operation.
        public List<Quotation_Items> QuotationItems { get; init; } = new();
        // Carries items for the related API operation.
        public List<ProjectViewerQuotationItemDto> Items { get; init; } = new();
    }

    // Defines the project viewer job order response or transfer shape exchanged through the API.
    public sealed class ProjectViewerJobOrderDto
    {
        // Carries record for the related API operation.
        public JobOrder Record { get; init; } = null!;
        // Carries items for the related API operation.
        public List<JobOrderItems> Items { get; init; } = new();
    }

    // Defines the project viewer delivery receipt response or transfer shape exchanged through the API.
    public sealed class ProjectViewerDeliveryReceiptDto
    {
        // Carries record for the related API operation.
        public DeliveryReceipt Record { get; init; } = null!;
        // Carries items for the related API operation.
        public List<DeliveryReceiptItems> Items { get; init; } = new();
        // Carries files for the related API operation.
        public List<DeliveryFiles> Files { get; init; } = new();
    }

    // Defines the project viewer quotation item response or transfer shape exchanged through the API.
    public sealed class ProjectViewerQuotationItemDto
    {
        // Identifies the related quotation item record.
        public int QuotationItemId { get; init; }
        // Identifies the related quotation store record.
        public int QuotationStoreId { get; init; }
        // Carries quotation store ids for the related API operation.
        public List<int> QuotationStoreIds { get; init; } = new();
        // Carries store for the related API operation. This value is optional.
        public string? Store { get; init; }
        // Carries requirement for the related API operation. This value is optional.
        public string? Requirement { get; init; }
        // Free-text description that records context or explanations for this project viewer quotation item dto.
        public string? Description { get; init; }
        // Carries width for the related API operation. This value is optional.
        public decimal? Width { get; init; }
        // Carries height for the related API operation. This value is optional.
        public decimal? Height { get; init; }
        // Carries measurement unit for the related API operation. This value is optional.
        public string? MeasurementUnit { get; init; }
        // Carries quantity for the related API operation. This value is optional.
        public decimal? Quantity { get; init; }
        // Financial price per square inch recorded for this project viewer quotation item dto.
        public decimal? PricePerSquareInch { get; init; }
        // Financial unit price recorded for this project viewer quotation item dto.
        public decimal? UnitPrice { get; init; }
        // Financial total amount recorded for this project viewer quotation item dto.
        public decimal? TotalAmount { get; init; }
        // Carries material ids for the related API operation.
        public List<int> MaterialIds { get; init; } = new();
    }
}
