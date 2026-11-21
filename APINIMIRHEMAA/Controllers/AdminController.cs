using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO.Admin;
using APINIMIRHEMAA.DTO.AuditTrail;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize(Policy = "admin")]
    [Route("api/[controller]")]
    [ApiController]
    // Coordinates the admin controller API endpoints, including request handling and response generation.
    public class AdminController : ControllerBase
    {
        // Injected or cached localdb dependency/state used by this class.
        private LocalDbContext _Localdb;
        // Injected or cached clouddb dependency/state used by this class.
        private CloudDbContext _Clouddb;
        // Injected or cached access permissions dependency/state used by this class.
        private readonly AccessPermissionService _accessPermissions;
        // Encryption service used to protect sensitive persisted values.
        private readonly EncryptionService _encryptionService;
        // Injected or cached user sessions dependency/state used by this class.
        private readonly UserSessionService _userSessions;
        // Logger used to record this class’s operational events and failures.
        private readonly ILogger<AdminController> _logger;

        // Initializes a new admin controller instance.
        public AdminController(
            LocalDbContext Localcontext,
            CloudDbContext Cloudcontext,
            AccessPermissionService accessPermissions,
            EncryptionService encryptionService,
            UserSessionService userSessions,
            ILogger<AdminController> logger)
        {
            _Localdb = Localcontext;
            _Clouddb = Cloudcontext;
            _accessPermissions = accessPermissions;
            _encryptionService = encryptionService;
            _userSessions = userSessions;
            _logger = logger;
        }

        // Sets the active state of employee-linked accounts and removes sessions when deactivating them.
        private async Task SetEmployeeAccountsActiveAsync(
            int employeeId,
            bool isActive,
            CancellationToken cancellationToken)
        {
            var linkedAccounts = await _Localdb.Users
                .IgnoreQueryFilters()
                // Apply this predicate in the database query before materializing results.
                .Where(account => account.Employee_ID == employeeId)
                .ToListAsync(cancellationToken);
            // Process each `account` returned by the collection expression in the loop header.
            foreach (var account in linkedAccounts)
            {
                account.IsActive = isActive;
            }

            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync(cancellationToken);
            // Enter this branch when `!isActive && linkedAccounts.Count > 0` is true.
            if (!isActive && linkedAccounts.Count > 0)
            {
                var userIds = linkedAccounts.Select(account => account.User_ID).ToArray();
                await _Localdb.UserSessions
                    // Apply this predicate in the database query before materializing results.
                    .Where(session => userIds.Contains(session.User_ID))
                    // Delete matching rows in the database without loading them into application memory.
                    .ExecuteDeleteAsync(cancellationToken);
            }
        }

        /// <summary>Encrypts the existing employee client data.</summary>
        [Authorize(Roles = "Admin,Super Admin")]
        [HttpPost("EncryptExistingEmployeeClientData")]
        public async Task<IActionResult> EncryptExistingEmployeeClientData(
            CancellationToken cancellationToken)
        {
            var employees = await EncryptEntitiesAsync(
                _Localdb,
                // Apply the ordering expression here before returning or materializing the results.
                _Localdb.Employee.AsNoTracking().OrderBy(employee => employee.Employee_Id),
                new[]
                {
                    nameof(Employee.LastName),
                    nameof(Employee.FirstName),
                    nameof(Employee.MiddleName),
                    nameof(Employee.status),
                    nameof(Employee.Birthday),
                    nameof(Employee.ContactNumber),
                    nameof(Employee.SSS_Number),
                    nameof(Employee.Philhealth_Number),
                    nameof(Employee.Pagibig_Number),
                    nameof(Employee.Date_Started),
                    nameof(Employee.Date_Resigned)
                },
                cancellationToken);
            var localClients = await EncryptEntitiesAsync(
                _Localdb,
                // Apply the ordering expression here before returning or materializing the results.
                _Localdb.Clients.IgnoreQueryFilters().AsNoTracking().OrderBy(client => client.Client_ID),
                ClientPrivateProperties,
                cancellationToken);
            var signatures = await EncryptEntitiesAsync(
                _Localdb,
                // Apply the ordering expression here before returning or materializing the results.
                _Localdb.UserSignatures.AsNoTracking().OrderBy(signature => signature.User_ID),
                new[] { nameof(UserSignature.SignatureData) },
                cancellationToken);
            var employeePhotos = await EncryptEntitiesAsync(
                _Localdb,
                // Apply the ordering expression here before returning or materializing the results.
                _Localdb.EmployeeProfilePhotos.AsNoTracking().OrderBy(photo => photo.Employee_ID),
                new[] { nameof(EmployeeProfilePhoto.PhotoData) },
                cancellationToken);
            var userPhotos = await EncryptEntitiesAsync(
                _Localdb,
                // Apply the ordering expression here before returning or materializing the results.
                _Localdb.UserProfilePhotos.AsNoTracking().OrderBy(photo => photo.User_ID),
                new[] { nameof(UserProfilePhoto.PhotoData) },
                cancellationToken);
            var cloudClients = await EncryptEntitiesAsync(
                _Clouddb,
                // Apply the ordering expression here before returning or materializing the results.
                _Clouddb.Clients.IgnoreQueryFilters().AsNoTracking().OrderBy(client => client.Client_ID),
                ClientPrivateProperties,
                cancellationToken);

            return Ok(new
            {
                local = new { employees, clients = localClients, signatures, employeePhotos, userPhotos },
                cloud = new { clients = cloudClients }
            });
        }

        private static readonly string[] ClientPrivateProperties =
        {
            nameof(ClientEntity.CreatedBy),
            nameof(ClientEntity.Client_Name),
            nameof(ClientEntity.Client_Telephone),
            nameof(ClientEntity.Contact_Person),
            nameof(ClientEntity.Business_Style),
            nameof(ClientEntity.Client_EmailAddress),
            nameof(ClientEntity.TermsAndCondition),
            nameof(ClientEntity.Company_Address),
            nameof(ClientEntity.TIN),
            nameof(ClientEntity.Payment_Terms),
            nameof(ClientEntity.Price_Per_Square_Inch)
        };

        // Performs the encrypt entities operation. Inputs: context (context); source (source); privateProperties (private properties); cancellationToken (cancellation token for the asynchronous operation). Returns int asynchronously.
        private static async Task<int> EncryptEntitiesAsync<TEntity>(
            DbContext context,
            IQueryable<TEntity> source,
            IReadOnlyCollection<string> privateProperties,
            CancellationToken cancellationToken)
            where TEntity : class
        {
            const int batchSize = 100;
            var processedCount = 0;
            var offset = 0;

            // Repeat the body while `true` remains true.
            while (true)
            {
                var batch = await source
                    .Skip(offset)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);
                // Exit the current operation when `batch.Count == 0` is true.
                if (batch.Count == 0)
                {
                    return processedCount;
                }

                // Process each `entity` returned by the collection expression in the loop header.
                foreach (var entity in batch)
                {
                    var entry = context.Attach(entity);
                    // Process each `property` returned by the collection expression in the loop header.
                    foreach (var property in privateProperties)
                    {
                        entry.Property(property).IsModified = true;
                    }
                }

                // Persist the pending entity changes to the database at this point.
                await context.SaveChangesAsync(cancellationToken);
                context.ChangeTracker.Clear();
                processedCount += batch.Count;
                offset += batch.Count;
            }
        }

        /// <summary>Retrieves the company overview report.</summary>
        [Authorize(Roles = "Admin,Super Admin")]
        [HttpGet("CompanyOverviewReport")]
        public async Task<ActionResult<CompanyOverviewReportDto>> GetCompanyOverviewReport(
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken cancellationToken)
        {
            // Return HTTP 400 Bad Request when `!from.HasValue || !to.HasValue` is true.
            if (!from.HasValue || !to.HasValue)
            {
                return BadRequest(new { message = "Both from and to date bounds are required (yyyy-MM-dd)." });
            }

            // Return HTTP 400 Bad Request when `from.Value > to.Value || to.Value == DateOnly.MaxValue` is true.
            if (from.Value > to.Value || to.Value == DateOnly.MaxValue)
            {
                return BadRequest(new { message = "The date range is invalid." });
            }

            var startDate = from.Value;
            var endDate = to.Value;
            var startUtc = DateTime.SpecifyKind(startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var endExclusiveUtc = DateTime.SpecifyKind(
                endDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Utc);
            var generatedAtUtc = DateTime.UtcNow;

            var quotations = _Localdb.Quotation.IgnoreQueryFilters().AsNoTracking();
            // Apply this predicate in the database query before materializing results.
            var quotationDates = quotations.Where(quotation =>
                quotation.Start_Date >= startDate && quotation.Start_Date <= endDate);
            var quotationStatuses = await BuildCompanyReportSectionAsync(
                quotationDates,
                // Filter the query to rows satisfying `uotations.Where(_ => false), , quotation => (decimal?)quotation.Overall_Total, quotation => quotation.Status, cancellationToken` before materializing the results.
                quotations.Where(_ => false),
                "Quotation.Start_Date",
                quotation => (decimal?)quotation.Overall_Total,
                quotation => quotation.Status,
                cancellationToken);

            var jobs = _Localdb.JobOrder.IgnoreQueryFilters().AsNoTracking();
            // Apply this predicate in the database query before materializing results.
            var jobsWithReportDate = jobs.Where(job =>
                (job.Date_Delivered ?? job.Target_Delivery ?? job.Installation_Date).HasValue);
            // Apply this predicate in the database query before materializing results.
            var jobDates = jobsWithReportDate.Where(job =>
                (job.Date_Delivered ?? job.Target_Delivery ?? job.Installation_Date) >= startDate &&
                (job.Date_Delivered ?? job.Target_Delivery ?? job.Installation_Date) <= endDate);
            var jobStatuses = await BuildCompanyReportSectionAsync(
                jobDates,
                // Filter the query to rows satisfying `jobs.Where(job => !(job.Date_Delivered ?? job.Target_Delivery ?? job.Installation_Date).HasValue), , null, job => job.Status, cancellationToken` before materializing the results.
                jobs.Where(job => !(job.Date_Delivered ?? job.Target_Delivery ?? job.Installation_Date).HasValue),
                "JobOrder.Date_Delivered ?? Target_Delivery ?? Installation_Date",
                null,
                job => job.Status,
                cancellationToken);

            var purchasedMaterials = _Localdb.Purchased_Material.IgnoreQueryFilters().AsNoTracking();
            // Apply this predicate in the database query before materializing results.
            var purchaseDates = purchasedMaterials.Where(purchase =>
                purchase.Date_Received_Item >= startDate && purchase.Date_Received_Item <= endDate);
            var purchasedMaterialSummary = await BuildCompanyReportSectionAsync(
                purchaseDates,
                // Filter the query to rows satisfying `Materials.Where(_ => false), , purchase => (decimal?)purchase.Amount, null, cancellationToken` before materializing the results.
                purchasedMaterials.Where(_ => false),
                "Purchased_Material.Date_Received_Item",
                purchase => (decimal?)purchase.Amount,
                null,
                cancellationToken);

            var invoices = _Localdb.ServiceInvoice.IgnoreQueryFilters().AsNoTracking();
            // Apply this predicate in the database query before materializing results.
            var invoiceDates = invoices.Where(invoice =>
                invoice.Invoice_Date >= startDate && invoice.Invoice_Date <= endDate);
            var invoiceSummary = await BuildCompanyReportSectionAsync(
                invoiceDates,
                // Filter the query to rows satisfying `invoices.Where(_ => false), , null, invoice => invoice.Status, cancellationToken` before materializing the results.
                invoices.Where(_ => false),
                "ServiceInvoice.Invoice_Date",
                null,
                invoice => invoice.Status,
                cancellationToken);
            var serviceInvoiceItems = _Localdb.ServiceInvoiceItems
                .IgnoreQueryFilters()
                .AsNoTracking();
            // Apply this predicate in the database query before materializing results.
            var invoicesWithAmounts = invoiceDates.Where(invoice =>
                invoice.Amount.HasValue ||
                serviceInvoiceItems.Any(item => item.ServiceInvoice_ID == invoice.ServiceInvoice_ID));
            invoiceSummary.AmountRecordCount = await invoicesWithAmounts.LongCountAsync(cancellationToken);
            // Enter this branch when `invoiceSummary.AmountRecordCount > 0` is true.
            if (invoiceSummary.AmountRecordCount > 0)
            {
                var headerAmountTotal = await invoiceDates
                    // Apply this predicate in the database query before materializing results.
                    .Where(invoice => invoice.Amount.HasValue)
                    .SumAsync(invoice => invoice.Amount, cancellationToken);
                var itemAmountTotal = await serviceInvoiceItems
                    // Apply this predicate in the database query before materializing results.
                    .Where(item => invoiceDates.Any(invoice =>
                        invoice.ServiceInvoice_ID == item.ServiceInvoice_ID &&
                        !invoice.Amount.HasValue))
                    .SumAsync(item => (decimal?)item.Amount, cancellationToken);
                invoiceSummary.AmountTotal = (headerAmountTotal ?? 0m) + (itemAmountTotal ?? 0m);
            }

            var payments = _Localdb.PaymentTransactions.IgnoreQueryFilters().AsNoTracking();
            // Apply this predicate in the database query before materializing results.
            var paymentDates = payments.Where(payment =>
                payment.PaidAtUtc >= startUtc && payment.PaidAtUtc < endExclusiveUtc);
            var paymentSummary = await BuildCompanyReportSectionAsync(
                paymentDates,
                // Filter the query to rows satisfying `payments.Where(_ => false), , payment => (decimal?)payment.Amount, null, cancellationToken` before materializing the results.
                payments.Where(_ => false),
                "PaymentTransaction.PaidAtUtc",
                payment => (decimal?)payment.Amount,
                null,
                cancellationToken);

            var requisitions = _Localdb.MaterialRequisition_Slip.IgnoreQueryFilters().AsNoTracking();
            // Apply this predicate in the database query before materializing results.
            var requisitionDates = requisitions.Where(requisition =>
                requisition.Date.HasValue &&
                requisition.Date.Value >= startDate &&
                requisition.Date.Value <= endDate);
            var requisitionSummary = await BuildCompanyReportSectionAsync(
                requisitionDates,
                // Filter the query to rows satisfying `uisitions.Where(requisition => !requisition.Date.HasValue), , requisition => requisition.Total_Cost.HasValue ? (decimal?)requisition.Total_Cost.Value : null, requisition => requisition.Status, cancellationToken` before materializing the results.
                requisitions.Where(requisition => !requisition.Date.HasValue),
                "MaterialRequisition_Slip.Date",
                requisition => requisition.Total_Cost.HasValue
                    ? (decimal?)requisition.Total_Cost.Value
                    : null,
                requisition => requisition.Status,
                cancellationToken);

            var inventory = _Localdb.Inventory.IgnoreQueryFilters().AsNoTracking();
            var inventorySnapshot = await inventory
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    ItemCount = group.LongCount(),
                    BalanceRecordCount = group.LongCount(item => item.Balance.HasValue),
                    BalanceTotal = group.Sum(item => (long?)item.Balance),
                    LowOrRestockCount = group.LongCount(item =>
                        (item.Balance ?? 0) <= 5 ||
                        (item.Status ?? "").ToLower().Contains("low") ||
                        (item.Status ?? "").ToLower().Contains("restock"))
                })
                .FirstOrDefaultAsync(cancellationToken);
            var inventoryStatuses = await inventory
                .GroupBy(item => item.Status)
                .Select(group => new { Status = group.Key, Count = group.LongCount() })
                .ToListAsync(cancellationToken);
            var inventoryStatusCounts = new Dictionary<string, long>();
            // Process each `statusGroup` returned by the collection expression in the loop header.
            foreach (var statusGroup in inventoryStatuses)
            {
                AddCount(
                    inventoryStatusCounts,
                    NormalizeInventoryStatus(statusGroup.Status),
                    statusGroup.Count);
            }

            var schedules = _Localdb.Installation_Schedule.IgnoreQueryFilters().AsNoTracking();
            // Apply this predicate in the database query before materializing results.
            var scheduleDates = schedules.Where(schedule =>
                schedule.Date >= startDate && schedule.Date <= endDate);
            var scheduleSummary = await BuildCompanyReportSectionAsync(
                scheduleDates,
                // Filter the query to rows satisfying `schedules.Where(_ => false), , null, null, cancellationToken` before materializing the results.
                schedules.Where(_ => false),
                "Installation_Schedule.Date",
                null,
                null,
                cancellationToken);

            var installationReports = _Localdb.InstallationReport.IgnoreQueryFilters().AsNoTracking();
            // Apply this predicate in the database query before materializing results.
            var installationReportDates = installationReports.Where(report =>
                report.ReportDate.HasValue &&
                report.ReportDate.Value >= startDate &&
                report.ReportDate.Value <= endDate);
            var installationReportSummary = await BuildCompanyReportSectionAsync(
                installationReportDates,
                // Filter the query to rows satisfying `onReports.Where(report => !report.ReportDate.HasValue), , null, null, cancellationToken` before materializing the results.
                installationReports.Where(report => !report.ReportDate.HasValue),
                "Installation_Report.ReportDate",
                null,
                null,
                cancellationToken);

            return Ok(new CompanyOverviewReportDto
            {
                From = startDate,
                To = endDate,
                GeneratedAtUtc = generatedAtUtc,
                Quotations = quotationStatuses,
                JobOrders = jobStatuses,
                PurchasedMaterials = purchasedMaterialSummary,
                ServiceInvoices = invoiceSummary,
                CollectionPayments = paymentSummary,
                Requisitions = requisitionSummary,
                Inventory = new InventorySnapshotReportDto
                {
                    AsOfUtc = generatedAtUtc,
                    ItemCount = inventorySnapshot?.ItemCount ?? 0,
                    BalanceRecordCount = inventorySnapshot?.BalanceRecordCount ?? 0,
                    BalanceTotal = inventorySnapshot?.BalanceTotal,
                    LowOrRestockCount = inventorySnapshot?.LowOrRestockCount ?? 0,
                    StatusCounts = inventoryStatusCounts
                },
                Installations = new InstallationsReportDto
                {
                    Schedules = scheduleSummary,
                    Reports = installationReportSummary
                }
            });
        }

        // Builds company report section. Inputs: dateBoundRecords (date bound records); undatedRecords (undated records); dateField (date field); amountSelector (amount selector); statusSelector (status selector); cancellationToken (cancellation token for the asynchronous operation). Returns CompanyReportSectionDto asynchronously.
        private static async Task<CompanyReportSectionDto> BuildCompanyReportSectionAsync<TEntity>(
            IQueryable<TEntity> dateBoundRecords,
            IQueryable<TEntity> undatedRecords,
            string dateField,
            Expression<Func<TEntity, decimal?>>? amountSelector,
            Expression<Func<TEntity, string?>>? statusSelector,
            CancellationToken cancellationToken)
        {
            var summary = new CompanyReportSectionDto
            {
                DateField = dateField,
                RecordCount = await dateBoundRecords.LongCountAsync(cancellationToken),
                UndatedRecordCount = await undatedRecords.LongCountAsync(cancellationToken),
                HasStatusField = statusSelector != null
            };

            // Enter this branch when `amountSelector != null && summary.RecordCount > 0` is true.
            if (amountSelector != null && summary.RecordCount > 0)
            {
                summary.AmountTotal = await dateBoundRecords
                    .Select(amountSelector)
                    .SumAsync(cancellationToken);
                var hasAmount = Expression.Lambda<Func<TEntity, bool>>(
                    Expression.NotEqual(
                        amountSelector.Body,
                        Expression.Constant(null, typeof(decimal?))),
                    amountSelector.Parameters);
                summary.AmountRecordCount = await dateBoundRecords.LongCountAsync(
                    hasAmount,
                    cancellationToken);
            }

            // Enter this branch when `statusSelector != null` is true.
            if (statusSelector != null)
            {
                var statusGroups = await dateBoundRecords
                    .GroupBy(statusSelector)
                    .Select(group => new { Status = group.Key, Count = group.LongCount() })
                    .ToListAsync(cancellationToken);
                // Process each `statusGroup` returned by the collection expression in the loop header.
                foreach (var statusGroup in statusGroups)
                {
                    AddCount(
                        summary.StatusCounts,
                        NormalizeReportStatus(statusGroup.Status),
                        statusGroup.Count);
                }
            }

            return summary;
        }

        // Normalizes the report status for consistent use.
        private static string NormalizeReportStatus(string? status)
        {
            var value = (status ?? "").Trim().ToLowerInvariant();
            // Exit the current operation when `value.Length == 0` is true.
            if (value.Length == 0)
                return "unknown";
            // Exit the current operation when `value.Contains() || value.Contains() || value.Contains()` is true.
            if (value.Contains("reject") || value.Contains("cancel") || value.Contains("declin"))
                return "rejected";
            // Exit the current operation when `value.Contains()` is true.
            if (value.Contains("block"))
                return "blocked";
            // Exit the current operation when `value.Contains()` is true.
            if (value.Contains("approve"))
                return "approved";
            // Exit the current operation when `value.Contains() || value.Contains() || value ==` is true.
            if (value.Contains("complete") || value.Contains("deliver") || value == "done")
                return "completed";
            // Exit the current operation when `value.Contains() || value.Contains() || value.Contains() || value.Contains() || value.Contains()` is true.
            if (value.Contains("progress") || value.Contains("ongoing") || value.Contains("active") ||
                value.Contains("started") || value.Contains("onsite"))
                return "inProgress";
            // Exit the current operation when `value.Contains() || value.Contains() || value == || value.Contains()` is true.
            if (value.Contains("pending") || value.Contains("await") || value == "open" ||
                value.Contains("submitted"))
                return "pending";
            return "other";
        }

        // Normalizes the inventory status for consistent use.
        private static string NormalizeInventoryStatus(string? status)
        {
            var value = (status ?? "").Trim().ToLowerInvariant();
            // Exit the current operation when `value.Length == 0` is true.
            if (value.Length == 0)
                return "unknown";
            // Exit the current operation when `value.Contains() || value.Contains()` is true.
            if (value.Contains("restock") || value.Contains("low"))
                return "lowOrRestock";
            // Exit the current operation when `value.Contains() || value.Contains()` is true.
            if (value.Contains("out") || value.Contains("zero"))
                return "outOfStock";
            // Exit the current operation when `value.Contains() || value.Contains()` is true.
            if (value.Contains("stock") || value.Contains("available"))
                return "inStock";
            return "other";
        }

        // Adds the supplied amount to the count stored for the specified key.
        private static void AddCount(
            IDictionary<string, long> counts,
            string key,
            long count)
        {
            counts.TryGetValue(key, out var currentCount);
            counts[key] = currentCount + count;
        }

        /// <summary>Retrieves all local audit trail entries.</summary>
        [HttpGet("GetLocalAuditTrail")]
        public List<AuditTrailEntryDTO> GetAllLocalAuditTrail()
        {
            var records = _Localdb.AuditTrail
                .AsNoTracking()
                // Apply the ordering expression here before returning or materializing the results.
                .OrderByDescending(record => record.Date)
                // Apply the ordering expression here before returning or materializing the results.
                .ThenByDescending(record => record.Time)
                .ToList();
            var legacyLabels = ResolveLegacyAuditLabels(records);
            var entries = records
                .Select(record => MapAuditTrailEntry(
                    record,
                    legacyLabels.GetValueOrDefault(record.AuditTrail_ID)))
                .ToList();
            return ConsolidateAuditBatchEntries(entries);
        }

        /// <summary>Retrieves the material requisition options.</summary>
        [HttpGet("GetMaterialRequisitionOptions")]
        public async Task<ActionResult<List<AdminMaterialRequisitionOption>>> GetMaterialRequisitionOptions(
            CancellationToken cancellationToken)
        {
            var materials = await (
                from inventory in _Localdb.Inventory.AsNoTracking()
                join material in _Localdb.Materials.AsNoTracking()
                    on inventory.Material_ID equals material.Material_ID
                orderby material.Item_Name
                select new
                {
                    inventory.Purchased,
                    inventory.Request_Purchased,
                    inventory.Request_Balance,
                    inventory.Returned,
                    material.Material_ID,
                    material.Item_Name,
                    material.Code_Name,
                    material.Unit
                }).ToListAsync(cancellationToken);

            var options = materials.Select(material =>
            {
                // Calculates available and purchased quantities for each material option.
                var purchasedAvailable = Math.Max(
                    (material.Purchased ?? 0) - (material.Request_Purchased ?? 0),
                    0);
                var available = Math.Max(
                    (material.Purchased ?? 0) + (material.Returned ?? 0)
                        - (material.Request_Purchased ?? 0)
                        - (material.Request_Balance ?? 0),
                    0);
                return new AdminMaterialRequisitionOption
                {
                    Material_ID = material.Material_ID,
                    Item_Name = material.Item_Name,
                    Code_Name = material.Code_Name,
                    Unit = material.Unit,
                    Available_Purchased = Math.Min(purchasedAvailable, available)
                };
            }).ToList();

            return Ok(options);
        }

        /// <summary>Retrieves the returned materials.</summary>
        [HttpGet("GetReturnedMaterials")]
        public async Task<ActionResult<List<MaterialRequesition_Slip>>> GetReturnedMaterials(
            CancellationToken cancellationToken)
        {
            var records = await _Localdb.MaterialRequisition_Slip
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(slip =>
                    slip.FromWho != null &&
                    (slip.FromWho.Trim().ToUpper() == "ADMIN" ||
                     slip.FromWho.Trim().ToUpper() == "WAREHOUSE" ||
                     slip.FromWho.Trim().ToUpper() == "INVENTORY") &&
                    slip.ToWho.Trim().ToUpper() == "SUPPLIER" &&
                    slip.Status != null && slip.Status.Trim().ToUpper() == "APPROVED")
                // Apply the ordering expression here before returning or materializing the results.
                .OrderByDescending(slip => slip.Date)
                // Apply the ordering expression here before returning or materializing the results.
                .ThenByDescending(slip => slip.MR_ID)
                .ToListAsync(cancellationToken);
            return Ok(records);
        }

        /// <summary>Adds the material requisition batch.</summary>
        [HttpPost("AddMaterialRequisitionBatch")]
        public async Task<IActionResult> AddMaterialRequisitionBatch(
            [FromBody] List<AdminMaterialRequisitionRequest> requests,
            CancellationToken cancellationToken)
        {
            // Return HTTP 400 Bad Request when `requests == null || requests.Count == 0` is true.
            if (requests == null || requests.Count == 0)
            {
                return BadRequest("Add at least one material before submitting.");
            }

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminUserId))
            {
                return Unauthorized("Your session is invalid. Please sign in again.");
            }

            var adminSignature = await _Localdb.UserSignatures
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    signature => signature.User_ID == adminUserId,
                    cancellationToken);
            if (adminSignature == null || adminSignature.SignatureData.Length == 0)
            {
                return BadRequest("Add a signature in Profile Settings before submitting a Material Requisition.");
            }

            var approvedBySignature =
                $"data:image/png;base64,{Convert.ToBase64String(adminSignature.SignatureData)}";
            using var transaction = await _Localdb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var materialIds = requests.Select(request => request.Material_ID).Distinct().ToList();
            var materials = await _Localdb.Materials
                // Apply this predicate in the database query before materializing results.
                .Where(material => materialIds.Contains(material.Material_ID))
                .ToDictionaryAsync(material => material.Material_ID, cancellationToken);
            var inventories = await _Localdb.Inventory
                // Apply this predicate in the database query before materializing results.
                .Where(inventory => materialIds.Contains(inventory.Material_ID))
                .ToDictionaryAsync(inventory => inventory.Material_ID, cancellationToken);
            var pendingDemandByMaterial = new Dictionary<int, int>();
            var slips = new List<MaterialRequesition_Slip>(requests.Count);
            var submitter = User.FindFirstValue(ClaimTypes.Name);
            var today = DateOnly.FromDateTime(DateTime.Now);

            // Repeat the body while `the loop condition` remains true.
            for (var index = 0; index < requests.Count; index++)
            {
                var request = requests[index];
                var label = $"Row {index + 1}";
                // Return HTTP 400 Bad Request when `string.IsNullOrWhiteSpace(request.Control_Number)` is true.
                if (string.IsNullOrWhiteSpace(request.Control_Number))
                    return BadRequest($"{label}: control number is required.");
                // Return HTTP 400 Bad Request when `!materials.TryGetValue(request.Material_ID, out var material) || !inventories.TryGetValue(request.Material_ID, out var inventory)` is true.
                if (!materials.TryGetValue(request.Material_ID, out var material) ||
                    !inventories.TryGetValue(request.Material_ID, out var inventory))
                    return BadRequest($"{label}: choose a material with a Warehouse inventory record.");
                // Return HTTP 400 Bad Request when `request.Quantity <= 0` is true.
                if (request.Quantity <= 0)
                    return BadRequest($"{label}: quantity must be greater than zero.");
                // Return HTTP 400 Bad Request when `request.Unit_Cost < 0 || (long)request.Quantity * request.Unit_Cost > int.MaxValue` is true.
                if (request.Unit_Cost < 0 ||
                    (long)request.Quantity * request.Unit_Cost > int.MaxValue)
                    return BadRequest($"{label}: unit cost or total cost exceeds the supported limit.");

                // Enter this branch when `!pendingDemandByMaterial.ContainsKey(request.Material_ID)` is true.
                if (!pendingDemandByMaterial.ContainsKey(request.Material_ID))
                {
                    pendingDemandByMaterial[request.Material_ID] = await _Localdb.MaterialRequisition_Slip
                        // Apply this predicate in the database query before materializing results.
                        .Where(slip => slip.Material_ID == request.Material_ID &&
                            (slip.Status == null || slip.Status.Trim() == "" ||
                             slip.Status.Trim().ToUpper() == "PENDING"))
                        .SumAsync(slip => slip.Quantity ?? 0, cancellationToken);
                }
                inventory.Request = pendingDemandByMaterial[request.Material_ID];

                var issuePlan = InventoryRules.PlanPurchasedIssue(inventory, request.Quantity);
                if (issuePlan.Error != null)
                {
                    return BadRequest($"{label}: {issuePlan.Error}");
                }
                InventoryRules.Apply(inventory, issuePlan);

                slips.Add(new MaterialRequesition_Slip
                {
                    Control_Number = request.Control_Number.Trim(),
                    FromWho = "Admin",
                    ToWho = "Supplier",
                    Material_ID = material.Material_ID,
                    Material_Name = material.Item_Name,
                    Supplier = string.IsNullOrWhiteSpace(request.Supplier) ? null : request.Supplier.Trim(),
                    Quantity = request.Quantity,
                    Unit_Cost = request.Unit_Cost,
                    Total_Cost = (int)((long)request.Quantity * request.Unit_Cost),
                    Submmited_By = submitter,
                    Approved_By = approvedBySignature,
                    Date = today,
                    Status = "Approved"
                });
            }

            _Localdb.MaterialRequisition_Slip.AddRange(slips);
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(new { saved = slips.Count });
        }

        /// <summary>Retrieves the local audit trail by id.</summary>
        [HttpGet("GetLocalAuditTrailByID")]
        public ActionResult<AuditTrailEntryDTO> GetLocalAuditTrailByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid Audit Trail ID.");
            }
            var auditTrail = _Localdb.AuditTrail.FirstOrDefault(x => x.AuditTrail_ID == id);
            // Return HTTP 404 Not Found when `auditTrail == null` is true.
            if (auditTrail == null)
            {
                return NotFound();
            }
            var legacyLabel = ResolveLegacyAuditLabels(new[] { auditTrail })
                .GetValueOrDefault(auditTrail.AuditTrail_ID);
            return Ok(MapAuditTrailEntry(auditTrail, legacyLabel));
        }

        // Resolves the legacy audit labels.
        private Dictionary<int, string> ResolveLegacyAuditLabels(
            IReadOnlyCollection<AuditTrail> records)
        {
            var references = records
                .Select(record => new
                {
                    record.AuditTrail_ID,
                    Reference = ParseLegacyAuditReference(record.Action)
                })
                // Apply this predicate in the database query before materializing results.
                .Where(item => item.Reference != null)
                .Select(item => new
                {
                    item.AuditTrail_ID,
                    Entity = item.Reference!.Value.Entity,
                    Id = item.Reference.Value.Id
                })
                .ToList();

            var quotationIds = references
                // Apply this predicate in the database query before materializing results.
                .Where(item => item.Entity == nameof(Quotation))
                .Select(item => item.Id)
                .Distinct()
                .ToList();
            var storeIds = references
                // Apply this predicate in the database query before materializing results.
                .Where(item => item.Entity == nameof(Quotation_Store))
                .Select(item => item.Id)
                .Distinct()
                .ToList();
            var itemIds = references
                // Apply this predicate in the database query before materializing results.
                .Where(item => item.Entity == nameof(Quotation_Items))
                .Select(item => item.Id)
                .Distinct()
                .ToList();
            var materialIds = references
                // Apply this predicate in the database query before materializing results.
                .Where(item => item.Entity == nameof(QuotationItemMaterial))
                .Select(item => item.Id)
                .Distinct()
                .ToList();

            var materialsById = materialIds.Count == 0
                ? new Dictionary<int, QuotationItemMaterial>()
                : _Localdb.QuotationItemMaterials.AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(item => materialIds.Contains(item.QuotationItemMaterial_ID))
                    .ToDictionary(item => item.QuotationItemMaterial_ID);
            var relatedItemIds = itemIds
                .Concat(materialsById.Values.Select(material => material.QI_ID))
                .Distinct()
                .ToList();
            var itemsById = relatedItemIds.Count == 0
                ? new Dictionary<int, Quotation_Items>()
                : _Localdb.Quotation_Items.AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(item => relatedItemIds.Contains(item.QI_ID))
                    .ToDictionary(item => item.QI_ID);
            var relatedStoreIds = storeIds
                .Concat(itemsById.Values.Select(item => item.QS_ID))
                .Concat(materialsById.Values
                    // Filter the query to rows satisfying `.Where(material => itemsById.ContainsKey(material.QI_ID)) .Select(material => itemsById[material.QI_ID].QS_ID)` before materializing the results.
                    .Where(material => itemsById.ContainsKey(material.QI_ID))
                    .Select(material => itemsById[material.QI_ID].QS_ID))
                .Distinct()
                .ToList();
            var storesById = relatedStoreIds.Count == 0
                ? new Dictionary<int, Quotation_Store>()
                : _Localdb.Quotation_Store.AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(store => relatedStoreIds.Contains(store.QS_ID))
                    .ToDictionary(store => store.QS_ID);
            quotationIds.AddRange(storesById.Values.Select(store => store.Quotation_ID));
            quotationIds = quotationIds.Distinct().ToList();

            var quotationsById = quotationIds.Count == 0
                ? new Dictionary<int, Quotation>()
                : _Localdb.Quotation.AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(quotation => quotationIds.Contains(quotation.Quotation_ID))
                    .ToDictionary(quotation => quotation.Quotation_ID);
            var clientIds = quotationsById.Values
                .Select(quotation => quotation.Client_ID)
                .Distinct()
                .ToList();
            var clientsById = clientIds.Count == 0
                ? new Dictionary<int, ClientEntity>()
                : _Localdb.Clients.AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(client => clientIds.Contains(client.Client_ID))
                    .ToDictionary(client => client.Client_ID);

            var labels = new Dictionary<int, string>();
            // Process each `reference` returned by the collection expression in the loop header.
            foreach (var reference in references)
            {
                var quotationId = reference.Entity switch
                {
                    nameof(Quotation) => reference.Id,
                    nameof(Quotation_Store) when storesById.TryGetValue(reference.Id, out var store)
                        => store.Quotation_ID,
                    nameof(Quotation_Items) when itemsById.TryGetValue(reference.Id, out var item) &&
                        storesById.TryGetValue(item.QS_ID, out var itemStore) => itemStore.Quotation_ID,
                    nameof(QuotationItemMaterial) when materialsById.TryGetValue(reference.Id, out var material) &&
                        itemsById.TryGetValue(material.QI_ID, out var materialItem) &&
                        storesById.TryGetValue(materialItem.QS_ID, out var materialStore)
                        => materialStore.Quotation_ID,
                    _ => (int?)null
                };
                // Skip this item and continue when `quotationId is not int id || !quotationsById.TryGetValue(id, out var quotation)` is true.
                if (quotationId is not int id ||
                    !quotationsById.TryGetValue(id, out var quotation))
                {
                    continue;
                }

                var clientName = clientsById.TryGetValue(quotation.Client_ID, out var client)
                    ? client.Client_Name
                    : null;
                var label = string.Join(" - ", new[]
                    {
                        clientName,
                        DecryptProtectedSubject(quotation.Client_Subject),
                        quotation.Title
                    }
                    // Filter the query to rows satisfying `.Where(value => !string.IsNullOrWhiteSpace(value)) .Select(value => value!.Trim())` before materializing the results.
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim()));
                // Enter this branch when `!string.IsNullOrWhiteSpace(label)` is true.
                if (!string.IsNullOrWhiteSpace(label))
                {
                    labels[reference.AuditTrail_ID] = label;
                }
            }

            return labels;
        }

        // Performs the parse legacy audit reference operation. Inputs: action (action). Returns (string Entity, int Id)?.
        private static (string Entity, int Id)? ParseLegacyAuditReference(string? action)
        {
            // Exit the current operation when `string.IsNullOrWhiteSpace(action)` is true.
            if (string.IsNullOrWhiteSpace(action))
            {
                return null;
            }

            var match = Regex.Match(
                action,
                @"^(?<entity>[A-Za-z0-9_]+)#\s*(?<id>\d+)\s*");
            return match.Success && int.TryParse(match.Groups["id"].Value, out var id)
                ? (match.Groups["entity"].Value, id)
                : null;
        }

        // Maps the audit trail entry to its corresponding representation.
        private AuditTrailEntryDTO MapAuditTrailEntry(
            AuditTrail record,
            string? legacyDocumentLabel)
        {
            const string detailsPrefix = "AUDIT_DETAILS_V1:";
            AuditTrailDetailsDTO? details = null;
            var storedAction = record.Action ?? "";
            // Enter this branch when `storedAction.StartsWith(detailsPrefix, StringComparison.Ordinal)` is true.
            if (storedAction.StartsWith(detailsPrefix, StringComparison.Ordinal))
            {
                try
                {
                    details = JsonSerializer.Deserialize<AuditTrailDetailsDTO>(
                        storedAction[detailsPrefix.Length..]);
                }
                // Handle `JsonException exception` raised by the preceding operation.
                catch (JsonException exception)
                {
                    Console.Error.WriteLine(
                        $"Audit trail details could not be read for record {record.AuditTrail_ID}: {exception.Message}");
                }
            }

            // Enter this branch when `details != null` is true.
            if (details != null)
            {
                details.DocumentLabel = DecryptProtectedSegments(details.DocumentLabel);
                details.ActionDescription = DecryptProtectedSegments(details.ActionDescription ?? "");
                DecryptProtectedValues(details.PreviousValues);
                DecryptProtectedValues(details.CurrentValues);
                details.FormRecords =
                [
                    new AuditTrailFormRecordDTO
                    {
                        Entity = details.Entity,
                        RecordId = details.RecordId,
                        Operation = details.Operation,
                        PreviousValues = details.PreviousValues,
                        CurrentValues = details.CurrentValues
                    }
                ];
            }

            var action = details == null
                ? FormatLegacyAuditAction(
                    DecryptProtectedSegments(storedAction),
                    DecryptProtectedSegments(legacyDocumentLabel ?? ""))
                : FormatAuditAction(details);

            return new AuditTrailEntryDTO
            {
                AuditTrail_ID = record.AuditTrail_ID,
                Name = record.Name,
                Date = record.Date,
                Time = record.Time,
                Action = action,
                Department = record.Department,
                Details = details
            };
        }

        // Combines audit entries from the same batch into a single display entry.
        private static List<AuditTrailEntryDTO> ConsolidateAuditBatchEntries(
            List<AuditTrailEntryDTO> entries)
        {
            var consolidated = new List<AuditTrailEntryDTO>();
            // Process each `group` returned by the collection expression in the loop header.
            foreach (var group in entries
                         // Filter the query to rows satisfying `.Where(entry => !string.IsNullOrWhiteSpace(entry.Details?.BatchId)) .GroupBy(entry => new { entry.Details!.BatchId, entry.Name, entry.Department })` before materializing the results.
                         .Where(entry => !string.IsNullOrWhiteSpace(entry.Details?.BatchId))
                         .GroupBy(entry => new
                         {
                             entry.Details!.BatchId,
                             entry.Name,
                             entry.Department
                         }))
            {
                var batch = group.ToList();
                // Enter this branch when `batch.Count == 1` is true.
                if (batch.Count == 1)
                {
                    consolidated.Add(batch[0]);
                    continue;
                }

                var auditRecords = batch
                    .SelectMany(entry => entry.Details!.FormRecords)
                    .ToList();
                var formName = ResolveAuditFormName(auditRecords.Select(record => record.Entity));
                var rootEntity = ResolveAuditRootEntity(formName);
                var rootRecords = auditRecords
                    // Apply this predicate in the database query before materializing results.
                    .Where(record =>
                        string.Equals(record.Entity, rootEntity, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var rootRecord = rootRecords.FirstOrDefault(record =>
                    string.Equals(record.Operation, "Edited", StringComparison.OrdinalIgnoreCase))
                    ?? rootRecords.FirstOrDefault();
                var replacementQuotation = string.Equals(
                        formName,
                        "Quotation",
                        StringComparison.OrdinalIgnoreCase)
                    && rootRecords.Any(record =>
                        string.Equals(record.Operation, "Edited", StringComparison.OrdinalIgnoreCase))
                    && rootRecords.Any(record =>
                        string.Equals(record.Operation, "Created", StringComparison.OrdinalIgnoreCase));
                var operation = rootRecord?.Operation switch
                {
                    "Created" => "Created",
                    "Deleted" => "Deleted",
                    _ => "Edited"
                };
                var actionDescription = operation switch
                {
                    "Created" => "has been created",
                    "Deleted" => "has been deleted",
                    _ => "has been updated"
                };
                // Enter this branch when `replacementQuotation` is true.
                if (replacementQuotation)
                {
                    actionDescription = "has been rejected and a new quotation has been created";
                }
                var rootEntry = batch.FirstOrDefault(entry =>
                    string.Equals(
                        entry.Details!.Entity,
                        rootEntity,
                        StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        entry.Details.RecordId,
                        rootRecord?.RecordId,
                        StringComparison.Ordinal));
                var details = new AuditTrailDetailsDTO
                {
                    Entity = formName,
                    RecordId = rootRecord?.RecordId,
                    Operation = operation,
                    BatchId = batch[0].Details!.BatchId,
                    DocumentLabel = rootEntry?.Details?.DocumentLabel ?? batch[0].Details!.DocumentLabel,
                    ActionDescription = actionDescription,
                    PreviousValues = rootRecord?.PreviousValues,
                    CurrentValues = rootRecord?.CurrentValues,
                    FormRecords = auditRecords
                };
                var firstEntry = batch
                    // Apply the ordering expression here before returning or materializing the results.
                    .OrderBy(entry => entry.Date)
                    // Apply the ordering expression here before returning or materializing the results.
                    .ThenBy(entry => entry.Time)
                    .First();
                consolidated.Add(new AuditTrailEntryDTO
                {
                    AuditTrail_ID = firstEntry.AuditTrail_ID,
                    Name = firstEntry.Name,
                    Date = firstEntry.Date,
                    Time = firstEntry.Time,
                    Department = firstEntry.Department,
                    Action = FormatAuditAction(details),
                    Details = details
                });
            }

            var batchedIds = entries
                // Apply this predicate in the database query before materializing results.
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Details?.BatchId))
                .Select(entry => entry.AuditTrail_ID)
                .ToHashSet();
            // Filter the query to rows satisfying `tries.Where(entry => !batchedIds.Contains(entry.AuditTrail_ID))` before materializing the results.
            consolidated.AddRange(entries.Where(entry => !batchedIds.Contains(entry.AuditTrail_ID)));
            return consolidated
                // Apply the ordering expression here before returning or materializing the results.
                .OrderByDescending(entry => entry.Date)
                // Apply the ordering expression here before returning or materializing the results.
                .ThenByDescending(entry => entry.Time)
                .ToList();
        }

        // Resolves the audit root entity.
        private static string ResolveAuditRootEntity(string formName) =>
            formName switch
            {
                "Quotation" => nameof(Quotation),
                "Job Order" => nameof(JobOrder),
                "Delivery Receipt" => nameof(DeliveryReceipt),
                "Service Invoice" => nameof(ServiceInvoice),
                "Installation Schedule" => nameof(Installation_Schedule),
                "Collection Receipt" => nameof(CollectionReceipt),
                "Installation Report" => nameof(InstallationReport),
                "Conforme" => nameof(Conforme),
                "Purchase Order" => nameof(PurchaseOrder),
                "Material Requisition" => nameof(MaterialRequesition_Slip),
                "Stock Transfer" => nameof(StockTransfer_Slip),
                _ => formName
            };

        // Resolves the audit form name.
        private static string ResolveAuditFormName(IEnumerable<string> entities)
        {
            var names = entities.ToHashSet(StringComparer.OrdinalIgnoreCase);
            // Exit the current operation when `names.Contains(nameof(PurchaseOrder))` is true.
            if (names.Contains(nameof(PurchaseOrder)))
            {
                return "Purchase Order";
            }
            // Exit the current operation when `names.Contains(nameof(Conforme))` is true.
            if (names.Contains(nameof(Conforme)))
            {
                return "Conforme";
            }
            // Exit the current operation when `names.Contains(nameof(Quotation)) || names.Contains(nameof(Quotation_Store)) || names.Contains(nameof(Quotation_Items)) || names.Contains(nameof(QuotationItemMaterial))` is true.
            if (names.Contains(nameof(Quotation)) ||
                names.Contains(nameof(Quotation_Store)) ||
                names.Contains(nameof(Quotation_Items)) ||
                names.Contains(nameof(QuotationItemMaterial)))
            {
                return "Quotation";
            }
            // Exit the current operation when `names.Contains(nameof(JobOrder)) || names.Contains(nameof(JobOrderItems))` is true.
            if (names.Contains(nameof(JobOrder)) || names.Contains(nameof(JobOrderItems)))
            {
                return "Job Order";
            }
            // Exit the current operation when `names.Contains(nameof(DeliveryReceipt)) || names.Contains(nameof(DeliveryReceiptItems)) || names.Contains(nameof(DeliveryFiles))` is true.
            if (names.Contains(nameof(DeliveryReceipt)) ||
                names.Contains(nameof(DeliveryReceiptItems)) ||
                names.Contains(nameof(DeliveryFiles)))
            {
                return "Delivery Receipt";
            }
            // Exit the current operation when `names.Contains(nameof(ServiceInvoice)) || names.Contains(nameof(ServiceInvoiceItem)) || names.Contains(nameof(ServiceInvoiceFile))` is true.
            if (names.Contains(nameof(ServiceInvoice)) ||
                names.Contains(nameof(ServiceInvoiceItem)) ||
                names.Contains(nameof(ServiceInvoiceFile)))
            {
                return "Service Invoice";
            }
            // Exit the current operation when `names.Contains(nameof(Installation_Schedule)) || names.Contains(nameof(InstallationScheduleEmployee))` is true.
            if (names.Contains(nameof(Installation_Schedule)) ||
                names.Contains(nameof(InstallationScheduleEmployee)))
            {
                return "Installation Schedule";
            }
            // Exit the current operation when `names.Contains(nameof(CollectionReceipt))` is true.
            if (names.Contains(nameof(CollectionReceipt)))
            {
                return "Collection Receipt";
            }
            // Exit the current operation when `names.Contains(nameof(InstallationReport))` is true.
            if (names.Contains(nameof(InstallationReport)))
            {
                return "Installation Report";
            }
            // Exit the current operation when `names.Contains(nameof(MaterialRequesition_Slip))` is true.
            if (names.Contains(nameof(MaterialRequesition_Slip)))
            {
                return "Material Requisition";
            }
            // Exit the current operation when `names.Contains(nameof(StockTransfer_Slip))` is true.
            if (names.Contains(nameof(StockTransfer_Slip)))
            {
                return "Stock Transfer";
            }

            return FormatAuditEntityName(names.FirstOrDefault() ?? "Form");
        }

        // Decrypts the protected values.
        private void DecryptProtectedValues(Dictionary<string, string?>? values)
        {
            // Exit the current operation when `values == null` is true.
            if (values == null)
            {
                return;
            }

            // Process each `key` returned by the collection expression in the loop header.
            foreach (var key in values.Keys.ToList())
            {
                // Enter this branch when `values[key] is { } value` is true.
                if (values[key] is { } value)
                {
                    values[key] = DecryptProtectedSegments(value);
                }
            }
        }

        // Decrypts the protected segments.
        private string DecryptProtectedSegments(string value) =>
            Regex.Replace(value, @"CfDJ8[A-Za-z0-9_-]+={0,2}", match =>
                DecryptProtectedSubject(match.Value));

        // Decrypts the protected subject.
        private string DecryptProtectedSubject(string? value)
        {
            // Exit the current operation when `string.IsNullOrEmpty(value) || !value.StartsWith(, StringComparison.Ordinal)` is true.
            if (string.IsNullOrEmpty(value) ||
                !value.StartsWith("CfDJ8", StringComparison.Ordinal))
            {
                return value ?? "";
            }

            return _encryptionService.TryDecrypt(value);
        }

        // Converts a legacy audit action into its current display label.
        private static string FormatLegacyAuditAction(string action, string? documentLabel)
        {
            var match = Regex.Match(
                action,
                @"^(?<entity>[A-Za-z0-9_]+)#\s*\d+\s*(?<description>.*)$");
            // Exit the current operation when `!match.Success` is true.
            if (!match.Success)
            {
                return action;
            }

            return ComposeAuditAction(
                FormatAuditEntityName(match.Groups["entity"].Value),
                documentLabel,
                match.Groups["description"].Value);
        }

        // Converts an audit action code into a readable display label.
        private static string FormatAuditAction(AuditTrailDetailsDTO details)
        {
            var description = details.ActionDescription;
            // Enter this branch when `string.IsNullOrWhiteSpace(description)` is true.
            if (string.IsNullOrWhiteSpace(description))
            {
                description = details.Operation switch
                {
                    "Created" => "has been created",
                    "Deleted" => "has been deleted",
                    _ => "was edited"
                };
            }

            return ComposeAuditAction(
                FormatAuditEntityName(details.Entity),
                details.DocumentLabel,
                description);
        }

        // Coordinates the and API endpoints, including request handling and response generation.
        // Builds the display action from the audit record and its related changes.
        private static string ComposeAuditAction(
            string formName,
            string? documentLabel,
            string? description)
        {
            var parts = new List<string> { formName };
            // Enter this branch when `!string.IsNullOrWhiteSpace(documentLabel) && !documentLabel.Equals(formName, StringComparison.OrdinalIgnoreCase)` is true.
            if (!string.IsNullOrWhiteSpace(documentLabel) &&
                !documentLabel.Equals(formName, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(documentLabel);
            }
            // Enter this branch when `!string.IsNullOrWhiteSpace(description)` is true.
            if (!string.IsNullOrWhiteSpace(description))
            {
                parts.Add(description);
            }

            return string.Join(" - ", parts);
        }

        // Converts an entity name into a readable audit label.
        private static string FormatAuditEntityName(string entityName) =>
            string.Concat(entityName.Select((character, index) =>
                index > 0 && char.IsUpper(character) &&
                !char.IsUpper(entityName[index - 1])
                    ? $" {character}"
                    : character.ToString()))
                .Replace('_', ' ')
                .Trim();

        /// <summary>Adds the local audit trail.</summary>
        [HttpPost("AddLocalAuditTrail")]
        public ActionResult<AuditTrail> AddLocalAuditTrail(AuditTrail auditTrail)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(auditTrail);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(auditTrail);
        }


        /// <summary>Retrieves all employees from the local database.</summary>
        [HttpGet("GetLocalEmployees")]
        public List<Employee> GetAllLocalEmployee()
        {
            return _Localdb.Employee.ToList();
        }

        /// <summary>Retrieves the archived local employees.</summary>
        [HttpGet("GetArchivedLocalEmployees")]
        public async Task<ActionResult<List<Employee>>> GetArchivedLocalEmployees(
            CancellationToken cancellationToken)
        {
            var employees = await _Localdb.Employee
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(employee => employee.IsArchived)
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(employee => employee.Employee_Id)
                .ToListAsync(cancellationToken);
            return Ok(employees);
        }

        /// <summary>Archives the employee and deactivates linked user accounts.</summary>
        [HttpPut("ArchiveLocalEmployee")]
        public async Task<ActionResult<Employee>> ArchiveLocalEmployee(
            [FromQuery] int employeeId,
            CancellationToken cancellationToken)
        {
            // Return HTTP 400 Bad Request when `employeeId <= 0` is true.
            if (employeeId <= 0)
            {
                return BadRequest(new { message = "A valid employee ID is required." });
            }

            await using var transaction = await _Localdb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var employee = await _Localdb.Employee.FirstOrDefaultAsync(
                record => record.Employee_Id == employeeId,
                cancellationToken);
            // Return HTTP 404 Not Found when `employee == null` is true.
            if (employee == null)
            {
                return NotFound(new { message = "The employee could not be found." });
            }
            // Return HTTP 400 Bad Request when `!employee.Date_Resigned.HasValue` is true.
            if (!employee.Date_Resigned.HasValue)
            {
                return BadRequest(new { message = "Only employees with a resignation date can be archived." });
            }
            // Return HTTP 409 Conflict when `employee.IsArchived` is true.
            if (employee.IsArchived)
            {
                return Conflict(new { message = "This employee is already archived." });
            }

            employee.IsArchived = true;
            await SetEmployeeAccountsActiveAsync(employeeId, false, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(employee);
        }

        /// <summary>Restores an archived employee and reactivates linked user accounts.</summary>
        [HttpPut("RestoreLocalEmployee")]
        public async Task<ActionResult<Employee>> RestoreLocalEmployee(
            [FromQuery] int employeeId,
            CancellationToken cancellationToken)
        {
            // Return HTTP 400 Bad Request when `employeeId <= 0` is true.
            if (employeeId <= 0)
            {
                return BadRequest(new { message = "A valid employee ID is required." });
            }

            await using var transaction = await _Localdb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var employee = await _Localdb.Employee.FirstOrDefaultAsync(
                record => record.Employee_Id == employeeId,
                cancellationToken);
            // Return HTTP 404 Not Found when `employee == null` is true.
            if (employee == null)
            {
                return NotFound(new { message = "The employee could not be found." });
            }
            // Return HTTP 409 Conflict when `!employee.IsArchived` is true.
            if (!employee.IsArchived)
            {
                return Conflict(new { message = "This employee is not archived." });
            }

            employee.IsArchived = false;
            employee.Date_Resigned = null;
            await SetEmployeeAccountsActiveAsync(employeeId, true, cancellationToken);
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(employee);
        }

        /// <summary>Adds the local employee.</summary>
        [HttpPost("AddLocalEmployee")]
        public async Task<ActionResult<Employee>> AddLocalEmployee(
            [FromBody] Employee? employee,
            CancellationToken cancellationToken)
        {
            // Return HTTP 400 Bad Request when `employee == null` is true.
            if (employee == null)
            {
                return BadRequest(new { message = "Employee details are required." });
            }

            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Return HTTP 400 Bad Request when `employee.Employee_Id < 0` is true.
            if (employee.Employee_Id < 0)
            {
                return BadRequest(new { message = "Employee ID cannot be negative." });
            }

            // Return HTTP 400 Bad Request when `string.IsNullOrWhiteSpace(employee.FirstName) || string.IsNullOrWhiteSpace(employee.LastName) || string.IsNullOrWhiteSpace(employee.status)` is true.
            if (string.IsNullOrWhiteSpace(employee.FirstName) ||
                string.IsNullOrWhiteSpace(employee.LastName) ||
                string.IsNullOrWhiteSpace(employee.status))
            {
                return BadRequest(new { message = "First name, last name, and status are required." });
            }

            const string employeeNamePattern = @"^[\p{L}\p{M} ]+$";
            // Return HTTP 400 Bad Request when `!Regex.IsMatch(employee.FirstName.Trim(), employeeNamePattern) || !Regex.IsMatch(employee.LastName.Trim(), employeeNamePattern) || (!string.IsNullOrWhiteSpace(employee.MiddleName) && !Regex.IsMatch(employee.MiddleName.Trim(), employeeNamePattern))` is true.
            if (!Regex.IsMatch(employee.FirstName.Trim(), employeeNamePattern) ||
                !Regex.IsMatch(employee.LastName.Trim(), employeeNamePattern) ||
                (!string.IsNullOrWhiteSpace(employee.MiddleName) &&
                 !Regex.IsMatch(employee.MiddleName.Trim(), employeeNamePattern)))
            {
                return BadRequest(new { message = "Employee names may contain letters and spaces only." });
            }

            // Return HTTP 400 Bad Request when `!string.IsNullOrWhiteSpace(employee.ContactNumber) && !Regex.IsMatch(employee.ContactNumber.Trim(), @)` is true.
            if (!string.IsNullOrWhiteSpace(employee.ContactNumber) &&
                !Regex.IsMatch(employee.ContactNumber.Trim(), @"^[0-9]{11}$"))
            {
                return BadRequest(new { message = "Enter exactly 11 digits for the phone number." });
            }

            var department = employee.Department_ID.HasValue
                ? await _Localdb.Departments.AsNoTracking().FirstOrDefaultAsync(
                    record => record.Department_ID == employee.Department_ID.Value,
                    cancellationToken)
                : null;
            // Return HTTP 400 Bad Request when `department == null` is true.
            if (department == null)
            {
                return BadRequest(new { message = "Select a valid department." });
            }

            var role = employee.Role_ID.HasValue
                ? await _Localdb.Roles.AsNoTracking().FirstOrDefaultAsync(
                    record => record.Role_ID == employee.Role_ID.Value,
                    cancellationToken)
                : null;
            // Return HTTP 400 Bad Request when `role == null` is true.
            if (role == null)
            {
                return BadRequest(new { message = "Select a valid role." });
            }
            // Return HTTP 400 Bad Request when `ModuleAccess.IsAdministratorRole(role.Role_Name) && !string.Equals( department.Department_Name.Trim(), , StringComparison.OrdinalIgnoreCase)` is true.
            if (ModuleAccess.IsAdministratorRole(role.Role_Name) &&
                !string.Equals(
                    department.Department_Name.Trim(),
                    "Admin",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "Admin and Super Admin roles can only be assigned to the Admin department."
                });
            }

            await using var transaction = await _Localdb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            // Enter this branch when `employee.Employee_Id == 0` is true.
            if (employee.Employee_Id == 0)
            {
                var lastEmployeeId = await _Localdb.Employee
                    .MaxAsync(record => (int?)record.Employee_Id, cancellationToken) ?? 0;
                employee.Employee_Id = checked(lastEmployeeId + 1);
            }
            // Return HTTP 409 Conflict when `await _Localdb.Employee.AnyAsync( record => record.Employee_Id == employee.Employee_Id, cancellationToken)` is true.
            else if (await _Localdb.Employee.AnyAsync(
                         record => record.Employee_Id == employee.Employee_Id,
                         cancellationToken))
            {
                return Conflict(new { message = $"Employee ID {employee.Employee_Id} is already in use." });
            }

            employee.FirstName = employee.FirstName.Trim();
            employee.LastName = employee.LastName.Trim();
            employee.MiddleName = string.IsNullOrWhiteSpace(employee.MiddleName)
                ? null
                : employee.MiddleName.Trim();
            employee.status = employee.status.Trim();
            employee.ContactNumber = string.IsNullOrWhiteSpace(employee.ContactNumber)
                ? null
                : employee.ContactNumber.Trim();
            employee.SSS_Number = string.IsNullOrWhiteSpace(employee.SSS_Number)
                ? null
                : employee.SSS_Number.Trim();
            employee.Philhealth_Number = string.IsNullOrWhiteSpace(employee.Philhealth_Number)
                ? null
                : employee.Philhealth_Number.Trim();
            employee.Pagibig_Number = string.IsNullOrWhiteSpace(employee.Pagibig_Number)
                ? null
                : employee.Pagibig_Number.Trim();
            employee.Date_Resigned = null;

            _Localdb.Employee.Add(employee);
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(employee);
        }

        /// <summary>Updates the local employee.</summary>
        [HttpPut("UpdateLocalEmployee")]
        public async Task<ActionResult<Employee>> UpdateLocalEmployee(
            [FromBody] Employee? employee,
            CancellationToken cancellationToken)
        {
            // Enter this branch when `employee == null || employee.Employee_Id <= 0` is true.
            if (employee == null || employee.Employee_Id <= 0)
            {
                // Coordinates the is API endpoints, including request handling and response generation.
                return BadRequest(new { message = "A valid employee record is required." });
            }

            var existingEmployee = await _Localdb.Employee.FirstOrDefaultAsync(
                record => record.Employee_Id == employee.Employee_Id,
                cancellationToken);
            // Return HTTP 404 Not Found when `existingEmployee == null` is true.
            if (existingEmployee == null)
            {
                return NotFound(new { message = "The employee could not be found." });
            }

            // Return HTTP 400 Bad Request when `string.IsNullOrWhiteSpace(employee.FirstName) || string.IsNullOrWhiteSpace(employee.LastName) || string.IsNullOrWhiteSpace(employee.status)` is true.
            if (string.IsNullOrWhiteSpace(employee.FirstName) ||
                string.IsNullOrWhiteSpace(employee.LastName) ||
                string.IsNullOrWhiteSpace(employee.status))
            {
                return BadRequest(new { message = "First name, last name, and status are required." });
            }

            const string employeeNamePattern = @"^[\p{L}\p{M} ]+$";
            // Return HTTP 400 Bad Request when `!Regex.IsMatch(employee.FirstName.Trim(), employeeNamePattern) || !Regex.IsMatch(employee.LastName.Trim(), employeeNamePattern) || (!string.IsNullOrWhiteSpace(employee.MiddleName) && !Regex.IsMatch(employee.MiddleName.Trim(), employeeNamePattern))` is true.
            if (!Regex.IsMatch(employee.FirstName.Trim(), employeeNamePattern) ||
                !Regex.IsMatch(employee.LastName.Trim(), employeeNamePattern) ||
                (!string.IsNullOrWhiteSpace(employee.MiddleName) &&
                 !Regex.IsMatch(employee.MiddleName.Trim(), employeeNamePattern)))
            {
                return BadRequest(new { message = "Employee names may contain letters and spaces only." });
            }

            // Return HTTP 400 Bad Request when `!string.IsNullOrWhiteSpace(employee.ContactNumber) && !Regex.IsMatch(employee.ContactNumber.Trim(), @)` is true.
            if (!string.IsNullOrWhiteSpace(employee.ContactNumber) &&
                !Regex.IsMatch(employee.ContactNumber.Trim(), @"^[0-9]{11}$"))
            {
                return BadRequest(new { message = "Enter exactly 11 digits for the phone number." });
            }

            // Return HTTP 400 Bad Request when `!string.IsNullOrWhiteSpace(employee.SSS_Number) && !Regex.IsMatch(employee.SSS_Number.Trim(), @)` is true.
            if (!string.IsNullOrWhiteSpace(employee.SSS_Number) &&
                !Regex.IsMatch(employee.SSS_Number.Trim(), @"^[0-9]{2}-[0-9]{7}-[0-9]$"))
            {
                return BadRequest(new { message = "Enter the SSS number as XX-XXXXXXX-X." });
            }

            // Return HTTP 400 Bad Request when `!string.IsNullOrWhiteSpace(employee.Philhealth_Number) && !Regex.IsMatch(employee.Philhealth_Number.Trim(), @)` is true.
            if (!string.IsNullOrWhiteSpace(employee.Philhealth_Number) &&
                !Regex.IsMatch(employee.Philhealth_Number.Trim(), @"^[0-9]{2}-[0-9]{9}-[0-9]$"))
            {
                return BadRequest(new { message = "Enter the PhilHealth number as XX-XXXXXXXXX-X." });
            }

            // Return HTTP 400 Bad Request when `!string.IsNullOrWhiteSpace(employee.Pagibig_Number) && !Regex.IsMatch(employee.Pagibig_Number.Trim(), @)` is true.
            if (!string.IsNullOrWhiteSpace(employee.Pagibig_Number) &&
                !Regex.IsMatch(employee.Pagibig_Number.Trim(), @"^[0-9]{4}-[0-9]{4}-[0-9]{4}$"))
            {
                return BadRequest(new { message = "Enter the Pag-IBIG number as XXXX-XXXX-XXXX." });
            }

            // Return HTTP 400 Bad Request when `!employee.Department_ID.HasValue || !await _Localdb.Departments.AnyAsync( department => department.Department_ID == employee.Department_ID.Value, cancellationToken)` is true.
            if (!employee.Department_ID.HasValue ||
                !await _Localdb.Departments.AnyAsync(
                    department => department.Department_ID == employee.Department_ID.Value,
                    cancellationToken))
            {
                return BadRequest(new { message = "Select a valid department." });
            }

            // Return HTTP 400 Bad Request when `!employee.Role_ID.HasValue || !await _Localdb.Roles.AnyAsync( role => role.Role_ID == employee.Role_ID.Value, cancellationToken)` is true.
            if (!employee.Role_ID.HasValue ||
                !await _Localdb.Roles.AnyAsync(
                    role => role.Role_ID == employee.Role_ID.Value,
                    cancellationToken))
            {
                return BadRequest(new { message = "Select a valid role." });
            }

            existingEmployee.LastName = employee.LastName.Trim();
            existingEmployee.FirstName = employee.FirstName.Trim();
            existingEmployee.MiddleName = string.IsNullOrWhiteSpace(employee.MiddleName)
                ? null
                : employee.MiddleName.Trim();
            existingEmployee.status = employee.status.Trim();
            existingEmployee.Department_ID = employee.Department_ID;
            existingEmployee.Role_ID = employee.Role_ID;
            existingEmployee.Birthday = employee.Birthday;
            existingEmployee.ContactNumber = string.IsNullOrWhiteSpace(employee.ContactNumber)
                ? null
                : employee.ContactNumber.Trim();
            existingEmployee.SSS_Number = string.IsNullOrWhiteSpace(employee.SSS_Number)
                ? null
                : employee.SSS_Number.Trim();
            existingEmployee.Philhealth_Number = string.IsNullOrWhiteSpace(employee.Philhealth_Number)
                ? null
                : employee.Philhealth_Number.Trim();
            existingEmployee.Pagibig_Number = string.IsNullOrWhiteSpace(employee.Pagibig_Number)
                ? null
                : employee.Pagibig_Number.Trim();
            existingEmployee.Date_Started = employee.Date_Started;
            existingEmployee.Date_Resigned = employee.Date_Resigned;

            await using var transaction = await _Localdb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            // Enter this branch when `existingEmployee.Date_Resigned.HasValue` is true.
            if (existingEmployee.Date_Resigned.HasValue)
            {
                await SetEmployeeAccountsActiveAsync(existingEmployee.Employee_Id, false, cancellationToken);
            }
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(existingEmployee);
        }

        /// <summary>Retrieves the local employee photo.</summary>
        [HttpGet("GetLocalEmployeePhoto")]
        public async Task<IActionResult> GetLocalEmployeePhoto(
            [FromQuery] int employeeId,
            CancellationToken cancellationToken)
        {
            // Return HTTP 400 Bad Request when `employeeId <= 0` is true.
            if (employeeId <= 0)
            {
                return BadRequest(new { message = "A valid employee ID is required." });
            }

            var photo = await _Localdb.EmployeeProfilePhotos
                .AsNoTracking()
                .FirstOrDefaultAsync(record => record.Employee_ID == employeeId, cancellationToken);
            return photo == null
                ? NotFound()
                : File(photo.PhotoData, photo.ContentType);
        }

        /// <summary>Persists the local employee photo.</summary>
        [HttpPut("SaveLocalEmployeePhoto")]
        [RequestSizeLimit(2 * 1024 * 1024 + 128 * 1024)]
        public async Task<IActionResult> SaveLocalEmployeePhoto(
            [FromQuery] int employeeId,
            IFormFile? photo,
            CancellationToken cancellationToken)
        {
            // Return HTTP 400 Bad Request when `employeeId <= 0` is true.
            if (employeeId <= 0)
            {
                return BadRequest(new { message = "A valid employee ID is required." });
            }

            // Return HTTP 400 Bad Request when `photo == null || photo.Length == 0` is true.
            if (photo == null || photo.Length == 0)
            {
                return BadRequest(new { message = "Choose a JPG or PNG employee photo." });
            }

            // Return HTTP 400 Bad Request when `photo.Length > 2 * 1024 * 1024` is true.
            if (photo.Length > 2 * 1024 * 1024)
            {
                return BadRequest(new { message = "Employee photos must be 2 MB or smaller." });
            }

            // Return HTTP 404 Not Found when `!await _Localdb.Employee.AnyAsync( employee => employee.Employee_Id == employeeId, cancellationToken)` is true.
            if (!await _Localdb.Employee.AnyAsync(
                    employee => employee.Employee_Id == employeeId,
                    cancellationToken))
            {
                return NotFound(new { message = "The employee could not be found." });
            }

            await using var memoryStream = new MemoryStream();
            await photo.CopyToAsync(memoryStream, cancellationToken);
            var photoBytes = memoryStream.ToArray();
            var validationError = UploadValidator.ValidateImage(photoBytes);
            // Return HTTP 400 Bad Request when `validationError != null` is true.
            if (validationError != null)
            {
                return BadRequest(new { message = validationError });
            }

            var contentType = photo.ContentType.ToLowerInvariant();
            // Return HTTP 400 Bad Request when `contentType is not ( or )` is true.
            if (contentType is not ("image/jpeg" or "image/png"))
            {
                return BadRequest(new { message = "Employee photos must be JPG or PNG images." });
            }

            var record = await _Localdb.EmployeeProfilePhotos
                .FirstOrDefaultAsync(item => item.Employee_ID == employeeId, cancellationToken);
            // Enter this branch when `record == null` is true.
            if (record == null)
            {
                record = new EmployeeProfilePhoto
                {
                    Employee_ID = employeeId,
                    PhotoData = photoBytes,
                    ContentType = contentType,
                    UpdatedAt = DateTime.UtcNow
                };
                _Localdb.EmployeeProfilePhotos.Add(record);
            }
            // Use this fallback path when the preceding condition is false.
            else
            {
                record.PhotoData = photoBytes;
                record.ContentType = contentType;
                record.UpdatedAt = DateTime.UtcNow;
            }

            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync(cancellationToken);
            return Ok(new { employeeId, message = "Employee photo saved." });
        }

        /// <summary>Retrieves all roles from the local database.</summary>
        [HttpGet("GetLocalRoles")]
        public List<Roles> GetAllLocalRoles()
        {
            return _Localdb.Roles.ToList();
        }

        /// <summary>Retrieves all departments from the local database.</summary>
        [HttpGet("GetLocalDeparments")]
        public List<Departments> GetAllLocalDepartments()
        {
            return _Localdb.Departments.ToList();
        }

        /// <summary>Retrieves the client access options.</summary>
        [HttpGet("ClientAccessOptions")]
        public async Task<IActionResult> GetClientAccessOptions(CancellationToken cancellationToken)
        {
            var marketingEmployees = await (
                from account in MarketingUsers().AsNoTracking()
                join employee in _Localdb.Employee.AsNoTracking()
                    on account.Employee_ID equals employee.Employee_Id
                select new
                {
                    account.User_ID,
                    Employee_ID = employee.Employee_Id,
                    employee.FirstName,
                    employee.LastName,
                    account.Username,
                    account.IsActive
                }).ToListAsync(cancellationToken);
            var orderedMarketingEmployees = marketingEmployees
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(employee => employee.LastName, StringComparer.OrdinalIgnoreCase)
                // Apply the ordering expression here before returning or materializing the results.
                .ThenBy(employee => employee.FirstName, StringComparer.OrdinalIgnoreCase)
                .Select(employee => new ClientAccessEmployeeDto
                {
                    User_ID = employee.User_ID,
                    Employee_ID = employee.Employee_ID,
                    Employee_Name = ((employee.FirstName ?? "") + " " + (employee.LastName ?? "")).Trim(),
                    Username = employee.Username,
                    IsActive = employee.IsActive
                })
                .ToList();

            var clientOwnerIds = await _Localdb.Clients
                .IgnoreQueryFilters()
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(client => client.CreatedByUserId.HasValue)
                .Select(client => client.CreatedByUserId.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
            var marketingOwners = orderedMarketingEmployees
                // Apply this predicate in the database query before materializing results.
                .Where(employee => clientOwnerIds.Contains(employee.User_ID))
                .ToList();

            return Ok(new
            {
                marketingOwners,
                marketingEmployees = orderedMarketingEmployees
            });
        }

        /// <summary>Retrieves the client access owner clients.</summary>
        [HttpGet("ClientAccessOwners/{userId:int}/Clients")]
        public async Task<IActionResult> GetClientAccessOwnerClients(
            int userId,
            CancellationToken cancellationToken)
        {
            // Return HTTP 400 Bad Request when `!await MarketingUsers().AnyAsync( account => account.User_ID == userId, cancellationToken)` is true.
            if (!await MarketingUsers().AnyAsync(
                    account => account.User_ID == userId,
                    cancellationToken))
            {
                return BadRequest(new { message = "Select an active Marketing employee with a user account." });
            }

            var clients = await _Localdb.Clients
                .IgnoreQueryFilters()
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(client => client.CreatedByUserId == userId)
                .Select(client => new
                {
                    client.Client_ID,
                    client.Client_Name
                })
                .ToListAsync(cancellationToken);

            return Ok(clients
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(client => client.Client_Name, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }

        /// <summary>Retrieves the client access grants.</summary>
        [HttpGet("ClientAccessGrants")]
        public async Task<ActionResult<List<ClientAccessGrantDto>>> GetClientAccessGrants(
            CancellationToken cancellationToken)
        {
            var grants = await (
                from grant in _Localdb.ClientAccessGrants.AsNoTracking()
                join client in _Localdb.Clients.IgnoreQueryFilters().AsNoTracking()
                    on grant.Client_ID equals client.Client_ID
                join account in _Localdb.Users.AsNoTracking()
                    on grant.User_ID equals account.User_ID
                join employee in _Localdb.Employee.AsNoTracking()
                    on account.Employee_ID equals employee.Employee_Id
                select new
                {
                    Client_ID = client.Client_ID,
                    Client_Name = client.Client_Name,
                    User_ID = account.User_ID,
                    Employee_Name = ((employee.FirstName ?? "") + " " + (employee.LastName ?? "")).Trim(),
                    employee.LastName,
                    employee.FirstName,
                    Username = account.Username,
                    GrantedAtUtc = grant.GrantedAtUtc
                }).ToListAsync(cancellationToken);

            return Ok(grants
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(grant => grant.Client_Name, StringComparer.OrdinalIgnoreCase)
                // Apply the ordering expression here before returning or materializing the results.
                .ThenBy(grant => grant.LastName, StringComparer.OrdinalIgnoreCase)
                // Apply the ordering expression here before returning or materializing the results.
                .ThenBy(grant => grant.FirstName, StringComparer.OrdinalIgnoreCase)
                .Select(grant => new ClientAccessGrantDto
                {
                    Client_ID = grant.Client_ID,
                    Client_Name = grant.Client_Name,
                    User_ID = grant.User_ID,
                    Employee_Name = grant.Employee_Name,
                    Username = grant.Username,
                    GrantedAtUtc = grant.GrantedAtUtc
                })
                .ToList());
        }

        /// <summary>Grants the specified user access to a client record.</summary>
        [HttpPost("ClientAccessGrants")]
        public async Task<IActionResult> GrantClientAccess(
            [FromBody] SaveClientAccessGrantDto request,
            CancellationToken cancellationToken)
        {
            // Return HTTP 400 Bad Request when `request.Client_ID <= 0 || request.User_ID <= 0` is true.
            if (request.Client_ID <= 0 || request.User_ID <= 0)
            {
                return BadRequest(new { message = "Select a valid client and Marketing employee." });
            }

            // Return HTTP 404 Not Found when `!await _Localdb.Clients.IgnoreQueryFilters() .AnyAsync(client => client.Client_ID == request.Client_ID, cancellationToken)` is true.
            if (!await _Localdb.Clients.IgnoreQueryFilters()
                    .AnyAsync(client => client.Client_ID == request.Client_ID, cancellationToken))
            {
                return NotFound(new { message = "The selected client could not be found." });
            }

            // Return HTTP 400 Bad Request when `!await ActiveMarketingUsers() .AnyAsync(account => account.User_ID == request.User_ID, cancellationToken)` is true.
            if (!await ActiveMarketingUsers()
                    .AnyAsync(account => account.User_ID == request.User_ID, cancellationToken))
            {
                return BadRequest(new { message = "Access can only be granted to an active Marketing employee with a user account." });
            }

            // Enter this branch when `!await _Localdb.ClientAccessGrants.AnyAsync( grant => grant.Client_ID == request.Client_ID && grant.User_ID == request.User_ID, cancellationToken)` is true.
            if (!await _Localdb.ClientAccessGrants.AnyAsync(
                    grant => grant.Client_ID == request.Client_ID && grant.User_ID == request.User_ID,
                    cancellationToken))
            {
                _Localdb.ClientAccessGrants.Add(new ClientAccessGrant
                {
                    Client_ID = request.Client_ID,
                    User_ID = request.User_ID,
                    GrantedAtUtc = DateTime.UtcNow
                });
                // Persist the pending entity changes to the database at this point.
                await _Localdb.SaveChangesAsync(cancellationToken);
            }

            var cloudSyncPending = false;
            try
            {
                var cloudClientExists = await _Clouddb.Clients.IgnoreQueryFilters()
                    .AnyAsync(client => client.Client_ID == request.Client_ID, cancellationToken);
                var cloudMarketingUserExists = await _Clouddb.Users.AsNoTracking()
                    .AnyAsync(account => account.User_ID == request.User_ID && account.IsActive, cancellationToken);

                // Enter this branch when `cloudClientExists && cloudMarketingUserExists && !await _Clouddb.ClientAccessGrants.AnyAsync( grant => grant.Client_ID == request.Client_ID && grant.User_ID == request.User_ID, cancellationToken)` is true.
                if (cloudClientExists && cloudMarketingUserExists &&
                    !await _Clouddb.ClientAccessGrants.AnyAsync(
                        grant => grant.Client_ID == request.Client_ID && grant.User_ID == request.User_ID,
                        cancellationToken))
                {
                    _Clouddb.ClientAccessGrants.Add(new ClientAccessGrant
                    {
                        Client_ID = request.Client_ID,
                        User_ID = request.User_ID,
                        GrantedAtUtc = DateTime.UtcNow
                    });
                    // Persist the pending entity changes to the database at this point.
                    await _Clouddb.SaveChangesAsync(cancellationToken);
                }
            }
            // Handle `SqlException exception` raised by the preceding operation.
            catch (SqlException exception)
            {
                cloudSyncPending = true;
                _logger.LogWarning(exception, "Client access was saved locally, but cloud synchronization failed.");
            }
            // Handle `TimeoutException exception` raised by the preceding operation.
            catch (TimeoutException exception)
            {
                cloudSyncPending = true;
                _logger.LogWarning(exception, "Client access was saved locally, but cloud synchronization timed out.");
            }

            return Ok(new
            {
                message = cloudSyncPending
                    ? "Access was saved locally, but the cloud database could not be reached. Retry when the connection is available."
                    : "Client access granted to the Marketing employee.",
                cloudSyncPending
            });
        }

        /// <summary>Grants the selected employees access to a client record.</summary>
        [HttpPost("ClientAccessGrants/Bulk")]
        public async Task<IActionResult> GrantClientAccessToMultipleEmployees(
            [FromBody] SaveClientAccessGrantsDto request,
            CancellationToken cancellationToken)
        {
            var clientIds = request.Client_IDs?.Distinct().ToList() ?? new List<int>();
            var userIds = request.User_IDs?.Distinct().ToList() ?? new List<int>();
            // Return HTTP 400 Bad Request when `request.OwnerUser_ID <= 0 || clientIds.Count == 0 || userIds.Count == 0 || clientIds.Any(id => id <= 0) || userIds.Any(id => id <= 0) || userIds.Contains(request.OwnerUser_ID)` is true.
            if (request.OwnerUser_ID <= 0 ||
                clientIds.Count == 0 ||
                userIds.Count == 0 ||
                clientIds.Any(id => id <= 0) ||
                userIds.Any(id => id <= 0) ||
                userIds.Contains(request.OwnerUser_ID))
            {
                return BadRequest(new { message = "Select an owner, at least one client, and at least one different Marketing employee." });
            }

            // Return HTTP 400 Bad Request when `!await MarketingUsers().AnyAsync( account => account.User_ID == request.OwnerUser_ID, cancellationToken)` is true.
            if (!await MarketingUsers().AnyAsync(
                    account => account.User_ID == request.OwnerUser_ID,
                    cancellationToken))
            {
                return BadRequest(new { message = "The selected client owner is not an active Marketing employee." });
            }

            var ownedClientCount = await _Localdb.Clients
                .IgnoreQueryFilters()
                .CountAsync(
                    client => clientIds.Contains(client.Client_ID) &&
                        client.CreatedByUserId == request.OwnerUser_ID,
                    cancellationToken);
            // Return HTTP 400 Bad Request when `ownedClientCount != clientIds.Count` is true.
            if (ownedClientCount != clientIds.Count)
            {
                return BadRequest(new { message = "One or more selected clients do not belong to the selected employee." });
            }

            var eligibleUserIds = await ActiveMarketingUsers()
                // Apply this predicate in the database query before materializing results.
                .Where(account => userIds.Contains(account.User_ID))
                .Select(account => account.User_ID)
                .ToListAsync(cancellationToken);
            // Return HTTP 400 Bad Request when `eligibleUserIds.Count != userIds.Count` is true.
            if (eligibleUserIds.Count != userIds.Count)
            {
                return BadRequest(new { message = "One or more selected recipients are not active Marketing employees with user accounts." });
            }

            var existingLocalGrants = await _Localdb.ClientAccessGrants
                // Apply this predicate in the database query before materializing results.
                .Where(grant =>
                    clientIds.Contains(grant.Client_ID) &&
                    userIds.Contains(grant.User_ID))
                .ToListAsync(cancellationToken);
            var existingLocalPairs = existingLocalGrants
                .Select(grant => (grant.Client_ID, grant.User_ID))
                .ToHashSet();
            // Process each `clientId` returned by the collection expression in the loop header.
            foreach (var clientId in clientIds)
            {
                // Process each `userId` returned by the collection expression in the loop header.
                foreach (var userId in userIds)
                {
                    // Enter this branch when `!existingLocalPairs.Contains((clientId, userId))` is true.
                    if (!existingLocalPairs.Contains((clientId, userId)))
                    {
                        _Localdb.ClientAccessGrants.Add(new ClientAccessGrant
                        {
                            Client_ID = clientId,
                            User_ID = userId,
                            GrantedAtUtc = DateTime.UtcNow
                        });
                    }
                }
            }
            // Enter this branch when `_Localdb.ChangeTracker.HasChanges()` is true.
            if (_Localdb.ChangeTracker.HasChanges())
            {
                // Persist the pending entity changes to the database at this point.
                await _Localdb.SaveChangesAsync(cancellationToken);
            }

            var cloudSyncPending = false;
            try
            {
                var cloudClientIds = await _Clouddb.Clients
                    .IgnoreQueryFilters()
                    // Apply this predicate in the database query before materializing results.
                    .Where(client =>
                        clientIds.Contains(client.Client_ID) &&
                        client.CreatedByUserId == request.OwnerUser_ID)
                    .Select(client => client.Client_ID)
                    .ToListAsync(cancellationToken);
                var cloudUserIds = await _Clouddb.Users.AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(account => userIds.Contains(account.User_ID) && account.IsActive)
                    .Select(account => account.User_ID)
                    .ToListAsync(cancellationToken);
                // Enter this branch when `cloudClientIds.Count > 0 && cloudUserIds.Count > 0` is true.
                if (cloudClientIds.Count > 0 && cloudUserIds.Count > 0)
                {
                    var existingCloudGrants = await _Clouddb.ClientAccessGrants
                        // Apply this predicate in the database query before materializing results.
                        .Where(grant =>
                            cloudClientIds.Contains(grant.Client_ID) &&
                            cloudUserIds.Contains(grant.User_ID))
                        .ToListAsync(cancellationToken);
                    var existingCloudPairs = existingCloudGrants
                        .Select(grant => (grant.Client_ID, grant.User_ID))
                        .ToHashSet();
                    // Process each `clientId` returned by the collection expression in the loop header.
                    foreach (var clientId in cloudClientIds)
                    {
                        // Process each `userId` returned by the collection expression in the loop header.
                        foreach (var userId in cloudUserIds)
                        {
                            // Enter this branch when `!existingCloudPairs.Contains((clientId, userId))` is true.
                            if (!existingCloudPairs.Contains((clientId, userId)))
                            {
                                _Clouddb.ClientAccessGrants.Add(new ClientAccessGrant
                                {
                                    Client_ID = clientId,
                                    User_ID = userId,
                                    GrantedAtUtc = DateTime.UtcNow
                                });
                            }
                        }
                    }
                    // Enter this branch when `_Clouddb.ChangeTracker.HasChanges()` is true.
                    if (_Clouddb.ChangeTracker.HasChanges())
                    {
                        // Persist the pending entity changes to the database at this point.
                        await _Clouddb.SaveChangesAsync(cancellationToken);
                    }
                }
            }
            // Handle `SqlException exception` raised by the preceding operation.
            catch (SqlException exception)
            {
                cloudSyncPending = true;
                _logger.LogWarning(exception, "Client access was saved locally, but cloud synchronization failed.");
            }
            // Handle `TimeoutException exception` raised by the preceding operation.
            catch (TimeoutException exception)
            {
                cloudSyncPending = true;
                _logger.LogWarning(exception, "Client access was saved locally, but cloud synchronization timed out.");
            }

            return Ok(new
            {
                message = cloudSyncPending
                    ? $"Access was granted locally to {userIds.Count} employee(s) for {clientIds.Count} client(s), but cloud synchronization failed. Retry when the connection is available."
                    : $"Access granted to {userIds.Count} employee(s) for {clientIds.Count} client(s).",
                cloudSyncPending,
                clientCount = clientIds.Count,
                employeeCount = userIds.Count
            });
        }

        /// <summary>Revokes the specified user's access to a client record.</summary>
        [HttpDelete("ClientAccessGrants/{clientId:int}/{userId:int}")]
        public async Task<IActionResult> RevokeClientAccess(
            int clientId,
            int userId,
            CancellationToken cancellationToken)
        {
            var localGrant = await _Localdb.ClientAccessGrants
                .FirstOrDefaultAsync(
                    grant => grant.Client_ID == clientId && grant.User_ID == userId,
                    cancellationToken);
            // Enter this branch when `localGrant != null` is true.
            if (localGrant != null)
            {
                _Localdb.ClientAccessGrants.Remove(localGrant);
                // Persist the pending entity changes to the database at this point.
                await _Localdb.SaveChangesAsync(cancellationToken);
            }

            var cloudSyncPending = false;
            var cloudGrantRemoved = false;
            try
            {
                var cloudGrant = await _Clouddb.ClientAccessGrants
                    .FirstOrDefaultAsync(
                        grant => grant.Client_ID == clientId && grant.User_ID == userId,
                        cancellationToken);
                // Enter this branch when `cloudGrant != null` is true.
                if (cloudGrant != null)
                {
                    _Clouddb.ClientAccessGrants.Remove(cloudGrant);
                    // Persist the pending entity changes to the database at this point.
                    await _Clouddb.SaveChangesAsync(cancellationToken);
                    cloudGrantRemoved = true;
                }
            }
            // Handle `SqlException exception` raised by the preceding operation.
            catch (SqlException exception)
            {
                cloudSyncPending = true;
                _logger.LogWarning(exception, "Client access was revoked locally, but cloud synchronization failed.");
            }
            // Handle `TimeoutException exception` raised by the preceding operation.
            catch (TimeoutException exception)
            {
                cloudSyncPending = true;
                _logger.LogWarning(exception, "Client access was revoked locally, but cloud synchronization timed out.");
            }

            // Return HTTP 404 Not Found when `localGrant == null && !cloudGrantRemoved && !cloudSyncPending` is true.
            if (localGrant == null && !cloudGrantRemoved && !cloudSyncPending)
            {
                return NotFound(new { message = "The client access grant could not be found." });
            }

            return Ok(new
            {
                message = cloudSyncPending
                    ? localGrant == null
                        ? "Cloud access could not be verified because the cloud database is unavailable."
                        : "Access was revoked locally, but the cloud database could not be reached. Retry when the connection is available."
                    : "Client access revoked.",
                cloudSyncPending
            });
        }

        /// <summary>Retrieves the access permissions.</summary>
        [HttpGet("AccessPermissions")]
        public async Task<ActionResult<List<AccessPermissionValueDto>>> GetAccessPermissions(
            [FromQuery] int? roleId,
            [FromQuery] int? departmentId,
            [FromQuery] int? userId,
            CancellationToken cancellationToken)
        {
            // Enter this branch when `userId.HasValue` is true.
            if (userId.HasValue)
            {
                // Return HTTP 404 Not Found when `!await _Localdb.Users.AnyAsync(user => user.User_ID == userId.Value, cancellationToken)` is true.
                if (!await _Localdb.Users.AnyAsync(user => user.User_ID == userId.Value, cancellationToken))
                    return NotFound(new { message = "The selected user account could not be found." });
            }
            // Return HTTP 400 Bad Request when `!roleId.HasValue && !departmentId.HasValue` is true.
            else if (!roleId.HasValue && !departmentId.HasValue)
            {
                return BadRequest(new { message = "Select a role, department, or user." });
            }

            // Return HTTP 400 Bad Request when `roleId.HasValue && !await _Localdb.Roles.AnyAsync(role => role.Role_ID == roleId.Value, cancellationToken)` is true.
            if (roleId.HasValue &&
                !await _Localdb.Roles.AnyAsync(role => role.Role_ID == roleId.Value, cancellationToken))
                return BadRequest(new { message = "The selected role could not be found." });
            // Return HTTP 400 Bad Request when `departmentId.HasValue && !await _Localdb.Departments.AnyAsync(department => department.Department_ID == departmentId.Value, cancellationToken)` is true.
            if (departmentId.HasValue &&
                !await _Localdb.Departments.AnyAsync(department => department.Department_ID == departmentId.Value, cancellationToken))
                return BadRequest(new { message = "The selected department could not be found." });

            return Ok(await _accessPermissions.GetEffectivePermissionsAsync(
                userId, roleId, departmentId, cancellationToken));
        }

        /// <summary>Retrieves the access permission active status.</summary>
        [HttpGet("AccessPermissions/active-status")]
        public async Task<ActionResult> GetAccessPermissionActiveStatus(
            [FromQuery] int? roleId,
            [FromQuery] int? departmentId,
            [FromQuery] int? userId,
            CancellationToken cancellationToken)
        {
            // Return HTTP 400 Bad Request when `userId.HasValue && (roleId.HasValue || departmentId.HasValue)` is true.
            if (userId.HasValue && (roleId.HasValue || departmentId.HasValue))
                return BadRequest(new { message = "Select either a specific user or a role/department scope." });
            // Return HTTP 400 Bad Request when `!userId.HasValue && !roleId.HasValue && !departmentId.HasValue` is true.
            if (!userId.HasValue && !roleId.HasValue && !departmentId.HasValue)
                return BadRequest(new { message = "Select a role, department, or user." });

            // Return HTTP 404 Not Found when `userId.HasValue && !await _Localdb.Users.AnyAsync(user => user.User_ID == userId.Value, cancellationToken)` is true.
            if (userId.HasValue &&
                !await _Localdb.Users.AnyAsync(user => user.User_ID == userId.Value, cancellationToken))
                return NotFound(new { message = "The selected user account could not be found." });
            // Return HTTP 400 Bad Request when `roleId.HasValue && !await _Localdb.Roles.AnyAsync(role => role.Role_ID == roleId.Value, cancellationToken)` is true.
            if (roleId.HasValue &&
                !await _Localdb.Roles.AnyAsync(role => role.Role_ID == roleId.Value, cancellationToken))
                return BadRequest(new { message = "The selected role could not be found." });
            // Return HTTP 400 Bad Request when `departmentId.HasValue && !await _Localdb.Departments.AnyAsync(department => department.Department_ID == departmentId.Value, cancellationToken)` is true.
            if (departmentId.HasValue &&
                !await _Localdb.Departments.AnyAsync(department => department.Department_ID == departmentId.Value, cancellationToken))
                return BadRequest(new { message = "The selected department could not be found." });

            IQueryable<Users> affectedUsers = _Localdb.Users.AsNoTracking();
            // Enter this branch when `userId.HasValue` is true.
            if (userId.HasValue)
                // Apply this predicate in the database query before materializing results.
                affectedUsers = affectedUsers.Where(user => user.User_ID == userId.Value);
            // Enter this branch when `roleId.HasValue` is true.
            if (roleId.HasValue)
                // Apply this predicate in the database query before materializing results.
                affectedUsers = affectedUsers.Where(user => user.Role_ID == roleId.Value);
            // Enter this branch when `departmentId.HasValue` is true.
            if (departmentId.HasValue)
                // Apply this predicate in the database query before materializing results.
                affectedUsers = affectedUsers.Where(user => user.Department_ID == departmentId.Value);

            var affectedUserIds = await affectedUsers
                .Select(user => user.User_ID)
                .ToListAsync(cancellationToken);
            var hasActiveUsers = await _userSessions.HasActiveSessionsAsync(
                affectedUserIds, cancellationToken);
            return Ok(new { hasActiveUsers });
        }

        /// <summary>Persists the access permissions.</summary>
        [HttpPut("AccessPermissions")]
        public async Task<IActionResult> SaveAccessPermissions(
            [FromBody] SaveAccessPermissionsDto request,
            CancellationToken cancellationToken)
        {
            // Return HTTP 400 Bad Request when `request.User_ID.HasValue && (request.Role_ID.HasValue || request.Department_ID.HasValue)` is true.
            if (request.User_ID.HasValue && (request.Role_ID.HasValue || request.Department_ID.HasValue))
                return BadRequest(new { message = "User-specific changes cannot update role or department permissions." });
            // Return HTTP 400 Bad Request when `!request.User_ID.HasValue && !request.Role_ID.HasValue && !request.Department_ID.HasValue` is true.
            if (!request.User_ID.HasValue && !request.Role_ID.HasValue && !request.Department_ID.HasValue)
                return BadRequest(new { message = "Select a role, department, or user before saving permissions." });

            // Return HTTP 404 Not Found when `request.User_ID.HasValue && !await _Localdb.Users.AnyAsync(user => user.User_ID == request.User_ID.Value, cancellationToken)` is true.
            if (request.User_ID.HasValue &&
                !await _Localdb.Users.AnyAsync(user => user.User_ID == request.User_ID.Value, cancellationToken))
                return NotFound(new { message = "The selected user account could not be found." });
            // Return HTTP 400 Bad Request when `request.Role_ID.HasValue && !await _Localdb.Roles.AnyAsync(role => role.Role_ID == request.Role_ID.Value, cancellationToken)` is true.
            if (request.Role_ID.HasValue &&
                !await _Localdb.Roles.AnyAsync(role => role.Role_ID == request.Role_ID.Value, cancellationToken))
                return BadRequest(new { message = "The selected role could not be found." });
            // Return HTTP 400 Bad Request when `request.Department_ID.HasValue && !await _Localdb.Departments.AnyAsync(department => department.Department_ID == request.Department_ID.Value, cancellationToken)` is true.
            if (request.Department_ID.HasValue &&
                !await _Localdb.Departments.AnyAsync(department => department.Department_ID == request.Department_ID.Value, cancellationToken))
                return BadRequest(new { message = "The selected department could not be found." });

            var permissions = request.Permissions ?? new List<AccessPermissionValueDto>();
            var expectedCount = AccessPermissionCatalog.Services.Length * AccessPermissionCatalog.Actions.Length;
            var distinctKeys = permissions
                .Select(permission => AccessPermissionService.Key(permission.ServiceKey, permission.ActionKey))
                .Distinct(StringComparer.Ordinal)
                .Count();
            // Return HTTP 400 Bad Request when `permissions.Count != expectedCount || distinctKeys != expectedCount || permissions.Any(permission => !AccessPermissionCatalog.IsValid(permission.ServiceKey, permission.ActionKey))` is true.
            if (permissions.Count != expectedCount ||
                distinctKeys != expectedCount ||
                permissions.Any(permission =>
                    !AccessPermissionCatalog.IsValid(permission.ServiceKey, permission.ActionKey)))
            {
                return BadRequest(new { message = "The permission matrix is incomplete or contains an unknown service or action." });
            }

            await using var transaction = await _Localdb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            IQueryable<Users> affectedUsers = _Localdb.Users.AsNoTracking();
            // Enter this branch when `request.User_ID.HasValue` is true.
            if (request.User_ID.HasValue)
                // Apply this predicate in the database query before materializing results.
                affectedUsers = affectedUsers.Where(user => user.User_ID == request.User_ID.Value);
            // Enter this branch when `request.Role_ID.HasValue` is true.
            if (request.Role_ID.HasValue)
                // Apply this predicate in the database query before materializing results.
                affectedUsers = affectedUsers.Where(user => user.Role_ID == request.Role_ID.Value);
            // Enter this branch when `request.Department_ID.HasValue` is true.
            if (request.Department_ID.HasValue)
                // Apply this predicate in the database query before materializing results.
                affectedUsers = affectedUsers.Where(user => user.Department_ID == request.Department_ID.Value);

            var affectedUserIds = await affectedUsers
                .Select(user => user.User_ID)
                .ToListAsync(cancellationToken);
            // Return HTTP 409 Conflict when `await _userSessions.HasActiveSessionsAsync(affectedUserIds, cancellationToken)` is true.
            if (await _userSessions.HasActiveSessionsAsync(affectedUserIds, cancellationToken))
            {
                return Conflict(new
                {
                    message = "Permissions cannot be changed while one or more affected users are signed in. Ask them to sign out and try again."
                });
            }

            var existing = await _Localdb.AccessPermissions
                // Apply this predicate in the database query before materializing results.
                .Where(permission =>
                    permission.User_ID == request.User_ID &&
                    permission.Role_ID == request.Role_ID &&
                    permission.Department_ID == request.Department_ID)
                .ToListAsync(cancellationToken);
            _Localdb.AccessPermissions.RemoveRange(existing);
            _Localdb.AccessPermissions.AddRange(permissions.Select(permission => new AccessPermission
            {
                User_ID = request.User_ID,
                Role_ID = request.User_ID.HasValue ? null : request.Role_ID,
                Department_ID = request.User_ID.HasValue ? null : request.Department_ID,
                ServiceKey = permission.ServiceKey,
                ActionKey = permission.ActionKey,
                IsAllowed = permission.Allowed
            }));
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new { message = "Access permissions saved successfully." });
        }

        // Returns user accounts whose linked employees belong to Marketing.
        private IQueryable<Users> MarketingUsers()
        {
            return
                from account in _Localdb.Users
                join employee in _Localdb.Employee
                    on account.Employee_ID equals employee.Employee_Id
                join department in _Localdb.Departments
                    on (employee.Department_ID ?? account.Department_ID) equals department.Department_ID
                where department.Department_Name.ToUpper().Contains("MARKETING")
                select account;
        }

        // Filters Marketing user accounts to those that are active.
        private IQueryable<Users> ActiveMarketingUsers() =>
            // Filter the query to rows satisfying `ers().Where(account => account.IsActive); } }` before materializing the results.
            MarketingUsers().Where(account => account.IsActive);
    }
}
