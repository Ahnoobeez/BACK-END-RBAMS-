namespace APINIMIRHEMAA.DTO.Admin
{

    public sealed class CompanyOverviewReportDto
    {

        public DateOnly From { get; init; }

        public DateOnly To { get; init; }

        public DateTime GeneratedAtUtc { get; init; }

        public CompanyReportSectionDto Quotations { get; init; } = new();

        public CompanyReportSectionDto JobOrders { get; init; } = new();

        public CompanyReportSectionDto PurchasedMaterials { get; init; } = new();

        public CompanyReportSectionDto ServiceInvoices { get; init; } = new();

        public CompanyReportSectionDto CollectionPayments { get; init; } = new();

        public CompanyReportSectionDto Requisitions { get; init; } = new();

        public InventorySnapshotReportDto Inventory { get; init; } = new();

        public InstallationsReportDto Installations { get; init; } = new();
    }


    public sealed class CompanyReportSectionDto
    {

        public string DateField { get; init; } = "";

        public long RecordCount { get; set; }

        public long UndatedRecordCount { get; set; }

        public decimal? AmountTotal { get; set; }

        public long AmountRecordCount { get; set; }

        public bool HasStatusField { get; set; }

        public Dictionary<string, long> StatusCounts { get; set; } = new();
    }


    public sealed class InventorySnapshotReportDto
    {

        public DateTime AsOfUtc { get; init; }

        public long ItemCount { get; init; }

        public long BalanceRecordCount { get; init; }

        public long? BalanceTotal { get; init; }

        public long LowOrRestockCount { get; init; }

        public Dictionary<string, long> StatusCounts { get; init; } = new();
    }


    public sealed class InstallationsReportDto
    {

        public CompanyReportSectionDto Schedules { get; init; } = new();

        public CompanyReportSectionDto Reports { get; init; } = new();
    }
}
