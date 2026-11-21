namespace APINIMIRHEMAA.DTO.Admin
{
    // Defines the company overview report response or transfer shape exchanged through the API.
    public sealed class CompanyOverviewReportDto
    {
        // Carries from for the related API operation.
        public DateOnly From { get; init; }
        // Carries to for the related API operation.
        public DateOnly To { get; init; }
        // Time value for generated at utc, using the UTC/time-zone convention indicated by its name.
        public DateTime GeneratedAtUtc { get; init; }
        // Carries quotations for the related API operation.
        public CompanyReportSectionDto Quotations { get; init; } = new();
        // Carries job orders for the related API operation.
        public CompanyReportSectionDto JobOrders { get; init; } = new();
        // Carries purchased materials for the related API operation.
        public CompanyReportSectionDto PurchasedMaterials { get; init; } = new();
        // Carries service invoices for the related API operation.
        public CompanyReportSectionDto ServiceInvoices { get; init; } = new();
        // Financial collection payments recorded for this company overview report dto.
        public CompanyReportSectionDto CollectionPayments { get; init; } = new();
        // Carries requisitions for the related API operation.
        public CompanyReportSectionDto Requisitions { get; init; } = new();
        // Carries inventory for the related API operation.
        public InventorySnapshotReportDto Inventory { get; init; } = new();
        // Carries installations for the related API operation.
        public InstallationsReportDto Installations { get; init; } = new();
    }

    // Defines the company report section response or transfer shape exchanged through the API.
    public sealed class CompanyReportSectionDto
    {
        // Records the date field for this operation.
        public string DateField { get; init; } = "";
        // Carries record count for the related API operation.
        public long RecordCount { get; set; }
        // Date associated with undated record count for this company report section dto.
        public long UndatedRecordCount { get; set; }
        // Financial amount total recorded for this company report section dto.
        public decimal? AmountTotal { get; set; }
        // Financial amount record count recorded for this company report section dto.
        public long AmountRecordCount { get; set; }
        // Indicates whether status field applies to this company report section dto.
        public bool HasStatusField { get; set; }
        // Current workflow status for this company report section dto.
        public Dictionary<string, long> StatusCounts { get; set; } = new();
    }

    // Defines the inventory snapshot report response or transfer shape exchanged through the API.
    public sealed class InventorySnapshotReportDto
    {
        // Time value for as of utc, using the UTC/time-zone convention indicated by its name.
        public DateTime AsOfUtc { get; init; }
        // Carries item count for the related API operation.
        public long ItemCount { get; init; }
        // Financial balance record count recorded for this inventory snapshot report dto.
        public long BalanceRecordCount { get; init; }
        // Financial balance total recorded for this inventory snapshot report dto.
        public long? BalanceTotal { get; init; }
        // Carries low or restock count for the related API operation.
        public long LowOrRestockCount { get; init; }
        // Current workflow status for this inventory snapshot report dto.
        public Dictionary<string, long> StatusCounts { get; init; } = new();
    }

    // Defines the installations report response or transfer shape exchanged through the API.
    public sealed class InstallationsReportDto
    {
        // Carries schedules for the related API operation.
        public CompanyReportSectionDto Schedules { get; init; } = new();
        // Carries reports for the related API operation.
        public CompanyReportSectionDto Reports { get; init; } = new();
    }
}
