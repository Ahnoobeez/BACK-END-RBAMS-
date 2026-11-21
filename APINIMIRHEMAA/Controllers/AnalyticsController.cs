using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]

    public class AnalyticsController : ControllerBase
    {

        private readonly LocalDbContext _localDb;

        private readonly EncryptionService _encryptionService;


        public AnalyticsController(LocalDbContext localDb, EncryptionService encryptionService)
        {
            _localDb = localDb;
            _encryptionService = encryptionService;
        }


        [HttpGet("GetCompanyOverview")]
        public ActionResult GetCompanyOverview()
        {
            var jobOrders = _localDb.JobOrder
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Select(job => new { job.Title, job.Status })
                .ToList();
            var jobStatuses = jobOrders.Select(job => GetStatusGroup(job.Status)).ToList();
            var inventory = _localDb.Inventory
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Select(item => new { item.Balance, item.Status })
                .ToList();
            var requisitionStatuses = _localDb.MaterialRequisition_Slip
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Select(request => request.Status)
                .ToList();
            var today = DateOnly.FromDateTime(DateTime.Today);
            var upcomingInstallations = (
                from schedule in _localDb.Installation_Schedule.IgnoreQueryFilters().AsNoTracking()
                join job in _localDb.JobOrder.IgnoreQueryFilters().AsNoTracking()
                    on schedule.JobOrder_ID equals job.JobOrder_ID
                where schedule.Date >= today
                orderby schedule.Date, schedule.Time
                select new
                {
                    job.Title,
                    schedule.Date,
                    schedule.Time,
                    schedule.Mall_Hours,
                    job.Status
                })
                .Take(8)
                .ToList();

            return Ok(new
            {
                clientCount = _localDb.Clients.IgnoreQueryFilters().AsNoTracking().Count(),
                quotationCount = _localDb.Quotation.IgnoreQueryFilters().AsNoTracking().Count(),
                jobOrderCount = jobOrders.Count,
                jobOrderStatuses = new
                {
                    pending = jobStatuses.Count(status => status == "pending"),
                    inProgress = jobStatuses.Count(status => status == "progress"),
                    completed = jobStatuses.Count(status => status == "completed"),
                    rejected = jobStatuses.Count(status => status == "rejected")
                },
                lowOrRestockInventoryCount = inventory.Count(item =>
                    (item.Balance ?? 0) <= 5 ||
                    (item.Status ?? "").Contains("low", StringComparison.OrdinalIgnoreCase) ||
                    (item.Status ?? "").Contains("restock", StringComparison.OrdinalIgnoreCase)),
                pendingMaterialRequestCount = requisitionStatuses.Count(IsPending),
                blockedJobOrderCount = jobOrders.Count(job =>
                    (job.Status ?? "").Contains("block", StringComparison.OrdinalIgnoreCase) ||
                    GetStatusGroup(job.Status) == "rejected"),
                upcomingInstallations = upcomingInstallations.Select(schedule => new
                {
                    jobOrderTitle = schedule.Title,
                    date = schedule.Date.ToString("yyyy-MM-dd"),
                    time = schedule.Time.ToString("HH:mm"),
                    mall_Hours = schedule.Mall_Hours,
                    status = schedule.Status
                })
            });
        }


        [Authorize(Policy = "marketing")]
        [HttpGet("GetMarketingOverview")]
        public async Task<ActionResult> GetMarketingOverview(CancellationToken cancellationToken)
        {
            var clients = await ClientsController.ClientsWithTelephoneAsText(_localDb.Clients)
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Select(client => new
                {
                    client.Client_ID,
                    client.Client_Name,
                    client.Client_Telephone,
                    client.Business_Style,
                    client.Payment_Terms
                })
                .ToListAsync(cancellationToken);
            var clientRows = clients.Select(client => new
            {
                client.Client_ID,
                client.Client_Name,
                client.Client_Telephone,
                Business_Style = !string.IsNullOrWhiteSpace(client.Business_Style) &&
                    client.Business_Style.StartsWith("CfDJ8", StringComparison.Ordinal)
                    ? _encryptionService.TryDecrypt(client.Business_Style)
                    : client.Business_Style,
                client.Payment_Terms
            });
            var quotations = await _localDb.Quotation
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Select(quotation => new
                {
                    quotation.Quotation_ID,
                    quotation.Client_ID,
                    quotation.Title,
                    quotation.Overall_Total,
                    quotation.Status,
                    quotation.Start_Date
                })
                .ToListAsync(cancellationToken);
            var jobs = await _localDb.JobOrder
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Select(jobOrder => new { jobOrder.JobOrder_ID })
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                clients = clientRows,
                quotations,
                jobs
            });
        }


        private static bool IsPending(string? status) =>
            string.IsNullOrWhiteSpace(status) || GetStatusGroup(status) == "pending";


        private static string GetStatusGroup(string? status)
        {
            var value = (status ?? "").Trim().ToLowerInvariant();

            if (value.Contains("reject") || value.Contains("cancel") || value.Contains("declin"))
                return "rejected";

            if (value.Contains("complete") || value.Contains("deliver") || value == "done")
                return "completed";

            if (value.Contains("progress") || value.Contains("ongoing") || value.Contains("active") ||
                value.Contains("started") || value.Contains("onsite"))
                return "progress";

            if (value.Contains("pending") || value.Contains("await") || value == "open")
                return "pending";
            return "";
        }
    }
}
