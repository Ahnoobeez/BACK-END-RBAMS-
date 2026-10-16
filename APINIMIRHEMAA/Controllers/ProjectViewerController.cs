using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO.ProjectViewer;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ProjectViewerController : ControllerBase
    {
        private readonly LocalDbContext _localDb;
        private readonly EncryptionService _encryptionService;

        public ProjectViewerController(LocalDbContext localDb, EncryptionService encryptionService)
        {
            _localDb = localDb;
            _encryptionService = encryptionService;
        }

        [HttpGet("Quotations")]
        public async Task<ActionResult<List<ProjectViewerQuotationOptionDto>>> GetQuotations()
        {
            var quotations = await _localDb.Quotation
                .AsNoTracking()
                .OrderByDescending(quotation => quotation.Quotation_ID)
                .ToListAsync();
            var clientIds = quotations.Select(quotation => quotation.Client_ID).Distinct().ToList();
            var clients = clientIds.Count == 0
                ? new List<Models.ClientEntity>()
                : await _localDb.Clients
                    .AsNoTracking()
                    .Where(client => clientIds.Contains(client.Client_ID))
                    .ToListAsync();
            var clientsById = clients.ToDictionary(client => client.Client_ID);

            var options = quotations.Select(quotation =>
            {
                clientsById.TryGetValue(quotation.Client_ID, out var client);
                return new ProjectViewerQuotationOptionDto
                {
                    QuotationId = quotation.Quotation_ID,
                    CompanyName = client?.Client_Name,
                    Title = quotation.Title
                };
            }).ToList();

            return Ok(options);
        }

        [HttpGet("Quotations/{quotationId:int}")]
        public async Task<ActionResult<ProjectViewerQuotationDetailsDto>> GetQuotationDetails(int quotationId)
        {
            if (quotationId <= 0)
            {
                return BadRequest("A valid quotation ID is required.");
            }

            var quotation = await _localDb.Quotation
                .AsNoTracking()
                .FirstOrDefaultAsync(record => record.Quotation_ID == quotationId);
            if (quotation == null)
            {
                return NotFound("The requested quotation was not found.");
            }

            var client = await _localDb.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(record => record.Client_ID == quotation.Client_ID);

            var stores = await _localDb.Quotation_Store
                .AsNoTracking()
                .Where(store => store.Quotation_ID == quotationId)
                .ToListAsync();
            var storeIds = stores.Select(store => store.QS_ID).ToList();
            var quotationItems = storeIds.Count == 0
                ? new List<Models.Quotation_Items>()
                : await _localDb.Quotation_Items
                    .AsNoTracking()
                    .Where(item => storeIds.Contains(item.QS_ID))
                    .ToListAsync();
            var storesById = stores.ToDictionary(store => store.QS_ID);
            var items = quotationItems.Select(item =>
            {
                storesById.TryGetValue(item.QS_ID, out var store);
                return new ProjectViewerQuotationItemDto
                {
                    Store = store?.Store,
                    Requirement = item.Requirements,
                    Description = item.Description,
                    Width = item.Width,
                    Height = item.Height,
                    MeasurementUnit = item.Measurement_Unit,
                    Quantity = item.Quantity,
                    PricePerSquareInch = item.Price_Per_Square_Inch,
                    UnitPrice = item.Unit_Price,
                    TotalAmount = item.Total_Amount
                };
            }).ToList();

            var conformes = await _localDb.Conforme
                .AsNoTracking()
                .Where(record => record.Quotation_ID == quotationId)
                .ToListAsync();
            var conformeIds = conformes.Select(record => record.FileID).ToList();
            var purchaseOrders = conformeIds.Count == 0
                ? new List<Models.PurchaseOrder>()
                : await _localDb.PurchaseOrder
                    .AsNoTracking()
                    .Where(record => conformeIds.Contains(record.Conforme_FileID))
                    .ToListAsync();
            var jobOrders = conformeIds.Count == 0
                ? new List<Models.JobOrder>()
                : await _localDb.JobOrder
                    .AsNoTracking()
                    .Where(record => conformeIds.Contains(record.Conforme_FileID))
                    .ToListAsync();
            var jobOrderIds = jobOrders.Select(record => record.JobOrder_ID).ToList();
            var jobOrderItems = jobOrderIds.Count == 0
                ? new List<Models.JobOrderItems>()
                : await _localDb.JobOrderItems
                    .AsNoTracking()
                    .Where(item => jobOrderIds.Contains(item.JobOrder_ID))
                    .ToListAsync();
            var deliveryReceipts = jobOrderIds.Count == 0
                ? new List<Models.DeliveryReceipt>()
                : await _localDb.DeliveryReceipt
                    .AsNoTracking()
                    .Where(record => record.JobOrderID.HasValue && jobOrderIds.Contains(record.JobOrderID.Value))
                    .ToListAsync();
            var deliveryIds = deliveryReceipts.Select(record => record.Delivery_ID).ToList();
            var deliveryReceiptItems = deliveryIds.Count == 0
                ? new List<Models.DeliveryReceiptItems>()
                : await _localDb.DeliveryReceiptItems
                    .AsNoTracking()
                    .Where(item => deliveryIds.Contains(item.Delivery_ID))
                    .ToListAsync();
            var deliveryFiles = deliveryIds.Count == 0
                ? new List<Models.DeliveryFiles>()
                : await _localDb.DeliveryFiles
                    .AsNoTracking()
                    .Where(file => deliveryIds.Contains(file.Delivery_ID))
                    .ToListAsync();
            var collectionReceipts = jobOrderIds.Count == 0
                ? new List<Models.CollectionReceipt>()
                : await _localDb.CollectionReceipt
                    .AsNoTracking()
                    .Where(record => jobOrderIds.Contains(record.JobOrderID))
                    .ToListAsync();
            var serviceInvoices = jobOrderIds.Count == 0
                ? new List<Models.ServiceInvoice>()
                : await _localDb.ServiceInvoice
                    .AsNoTracking()
                    .Where(record => record.JobOrder_ID.HasValue && jobOrderIds.Contains(record.JobOrder_ID.Value))
                    .ToListAsync();
            var installationReports = jobOrderIds.Count == 0
                ? new List<Models.InstallationReport>()
                : await _localDb.InstallationReport
                    .AsNoTracking()
                    .Where(record => jobOrderIds.Contains(record.JobOrder_ID))
                    .ToListAsync();

            if (!string.IsNullOrWhiteSpace(client?.Business_Style) &&
                client.Business_Style.StartsWith("CfDJ8", StringComparison.Ordinal))
            {
                client.Business_Style = _encryptionService.TryDecrypt(client.Business_Style);
            }

            if (!string.IsNullOrEmpty(quotation.Client_Subject) &&
                quotation.Client_Subject.StartsWith("CfDJ8", StringComparison.Ordinal))
            {
                quotation.Client_Subject = _encryptionService.TryDecrypt(quotation.Client_Subject);
            }

            return Ok(new ProjectViewerQuotationDetailsDto
            {
                Quotation = quotation,
                Client = client,
                Conformes = conformes,
                PurchaseOrders = purchaseOrders,
                JobOrders = jobOrders.Select(jobOrder => new ProjectViewerJobOrderDto
                {
                    Record = jobOrder,
                    Items = jobOrderItems
                        .Where(item => item.JobOrder_ID == jobOrder.JobOrder_ID)
                        .ToList()
                }).ToList(),
                DeliveryReceipts = deliveryReceipts.Select(receipt => new ProjectViewerDeliveryReceiptDto
                {
                    Record = receipt,
                    Items = deliveryReceiptItems
                        .Where(item => item.Delivery_ID == receipt.Delivery_ID)
                        .ToList(),
                    Files = deliveryFiles
                        .Where(file => file.Delivery_ID == receipt.Delivery_ID)
                        .ToList()
                }).ToList(),
                CollectionReceipts = collectionReceipts,
                ServiceInvoices = serviceInvoices,
                InstallationReports = installationReports,
                QuotationStores = stores,
                QuotationItems = quotationItems,
                Items = items
            });
        }
    }
}
