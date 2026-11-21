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
    // Coordinates the project viewer controller API endpoints, including request handling and response generation.
    public class ProjectViewerController : ControllerBase
    {
        // Injected or cached local db dependency/state used by this class.
        private readonly LocalDbContext _localDb;
        // Encryption service used to protect sensitive persisted values.
        private readonly EncryptionService _encryptionService;

        // Initializes a new project viewer controller instance.
        public ProjectViewerController(LocalDbContext localDb, EncryptionService encryptionService)
        {
            _localDb = localDb;
            _encryptionService = encryptionService;
        }

        /// <summary>Retrieves the quotations.</summary>
        [HttpGet("Quotations")]
        public async Task<ActionResult<List<ProjectViewerQuotationOptionDto>>> GetQuotations()
        {
            var quotations = await _localDb.Quotation
                .AsNoTracking()
                // Apply the ordering expression here before returning or materializing the results.
                .OrderByDescending(quotation => quotation.Quotation_ID)
                .ToListAsync();
            var clientIds = quotations.Select(quotation => quotation.Client_ID).Distinct().ToList();
            var clients = clientIds.Count == 0
                ? new List<Models.ClientEntity>()
                : await _localDb.Clients
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(client => clientIds.Contains(client.Client_ID))
                    .ToListAsync();
            var clientsById = clients.ToDictionary(client => client.Client_ID);

            var options = quotations.Select(quotation =>
            {
                // Combines project, quotation, and client data for the viewer response.
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

        /// <summary>Retrieves the quotation details.</summary>
        [HttpGet("Quotations/{quotationId:int}")]
        public async Task<ActionResult<ProjectViewerQuotationDetailsDto>> GetQuotationDetails(int quotationId)
        {
            // Return HTTP 400 Bad Request when `quotationId <= 0` is true.
            if (quotationId <= 0)
            {
                return BadRequest("A valid quotation ID is required.");
            }

            var quotation = await _localDb.Quotation
                .AsNoTracking()
                .FirstOrDefaultAsync(record => record.Quotation_ID == quotationId);
            // Return HTTP 404 Not Found when `quotation == null` is true.
            if (quotation == null)
            {
                return NotFound("The requested quotation was not found.");
            }

            var client = await _localDb.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(record => record.Client_ID == quotation.Client_ID);

            var stores = await _localDb.Quotation_Store
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(store => store.Quotation_ID == quotationId)
                .ToListAsync();
            var storeIds = stores.Select(store => store.QS_ID).ToList();
            var quotationItems = storeIds.Count == 0
                ? new List<Models.Quotation_Items>()
                : await _localDb.Quotation_Items
                    .AsNoTracking()
                    // Load the related `item => item.Materials` data with the parent records for the response or subsequent logic.
                    .Include(item => item.Materials)
                    // Apply this predicate in the database query before materializing results.
                    .Where(item => storeIds.Contains(item.QS_ID))
                    .ToListAsync();
            var storesById = stores.ToDictionary(store => store.QS_ID);
            var itemIds = quotationItems.Select(item => item.QI_ID).ToList();
            var storeLinks = itemIds.Count == 0
                ? new List<APINIMIRHEMAA.Models.QuotationItemStore>()
                : await _localDb.QuotationItemStores
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(link => itemIds.Contains(link.QI_ID))
                    .ToListAsync();
            var linkedStoreIdsByItem = storeLinks
                .GroupBy(link => link.QI_ID)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(link => link.QS_ID).ToHashSet());
            var items = quotationItems.Select(item =>
            {
                // Combines project, quotation, and client data for the viewer response.
                var storeIdsForItem = linkedStoreIdsByItem.TryGetValue(item.QI_ID, out var linkedStoreIds)
                    ? linkedStoreIds
                    : new HashSet<int>();
                storeIdsForItem.Add(item.QS_ID);
                // Apply the ordering expression here before returning or materializing the results.
                item.QuotationStoreIds = storeIdsForItem.OrderBy(storeId => storeId).ToList();
                var storeNames = item.QuotationStoreIds
                    // Apply this predicate in the database query before materializing results.
                    .Where(storesById.ContainsKey)
                    .Select(storeId => storesById[storeId].Store)
                    // Apply this predicate in the database query before materializing results.
                    .Where(storeName => !string.IsNullOrWhiteSpace(storeName));
                return new ProjectViewerQuotationItemDto
                {
                    QuotationItemId = item.QI_ID,
                    QuotationStoreId = item.QS_ID,
                    QuotationStoreIds = item.QuotationStoreIds,
                    Store = string.Join(", ", storeNames),
                    Requirement = item.Requirements,
                    Description = item.Description,
                    Width = item.Width,
                    Height = item.Height,
                    MeasurementUnit = item.Measurement_Unit,
                    Quantity = item.Quantity,
                    PricePerSquareInch = item.Price_Per_Square_Inch,
                    UnitPrice = item.Unit_Price,
                    TotalAmount = item.Total_Amount,
                    MaterialIds = item.Materials.Select(material => material.Material_ID).ToList()
                };
            }).ToList();

            var conformes = await _localDb.Conforme
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(record => record.Quotation_ID == quotationId)
                .ToListAsync();
            // Process each `conforme` returned by the collection expression in the loop header.
            foreach (var conforme in conformes)
            {
                conforme.FIleData = _encryptionService.TryDecrypt(conforme.FIleData);
            }
            var conformeIds = conformes.Select(record => record.FileID).ToList();
            var purchaseOrders = conformeIds.Count == 0
                ? new List<Models.PurchaseOrder>()
                : await _localDb.PurchaseOrder
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(record => conformeIds.Contains(record.Conforme_FileID))
                    .ToListAsync();
            // Process each `purchaseOrder` returned by the collection expression in the loop header.
            foreach (var purchaseOrder in purchaseOrders)
            {
                purchaseOrder.FileData = _encryptionService.TryDecrypt(purchaseOrder.FileData);
            }
            var jobOrders = conformeIds.Count == 0
                ? new List<Models.JobOrder>()
                : await _localDb.JobOrder
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(record => conformeIds.Contains(record.Conforme_FileID))
                    .ToListAsync();
            var jobOrderIds = jobOrders.Select(record => record.JobOrder_ID).ToList();
            var jobOrderItems = jobOrderIds.Count == 0
                ? new List<Models.JobOrderItems>()
                : await _localDb.JobOrderItems
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(item => jobOrderIds.Contains(item.JobOrder_ID))
                    .ToListAsync();
            var deliveryReceipts = jobOrderIds.Count == 0
                ? new List<Models.DeliveryReceipt>()
                : await _localDb.DeliveryReceipt
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(record => record.JobOrderID.HasValue && jobOrderIds.Contains(record.JobOrderID.Value))
                    .ToListAsync();
            var deliveryIds = deliveryReceipts.Select(record => record.Delivery_ID).ToList();
            var deliveryReceiptItems = deliveryIds.Count == 0
                ? new List<Models.DeliveryReceiptItems>()
                : await _localDb.DeliveryReceiptItems
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(item => deliveryIds.Contains(item.Delivery_ID))
                    .ToListAsync();
            var deliveryFiles = deliveryIds.Count == 0
                ? new List<Models.DeliveryFiles>()
                : await _localDb.DeliveryFiles
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(file => deliveryIds.Contains(file.Delivery_ID))
                    .ToListAsync();
            // Process each `deliveryFile` returned by the collection expression in the loop header.
            foreach (var deliveryFile in deliveryFiles)
            {
                deliveryFile.File_Data = _encryptionService.TryDecrypt(deliveryFile.File_Data);
            }
            var preparedByUsers = deliveryFiles
                // Apply this predicate in the database query before materializing results.
                .Where(file => string.Equals(file.File_Role, "PreparedBy", StringComparison.OrdinalIgnoreCase))
                .GroupBy(file => file.Delivery_ID)
                .ToDictionary(
                    group => group.Key,
                    // Apply the ordering expression here before returning or materializing the results.
                    group => group.OrderBy(file => file.File_ID)
                        .Select(file => file.CreatedByUserId)
                        .FirstOrDefault());
            deliveryFiles = deliveryFiles
                // Apply this predicate in the database query before materializing results.
                .Where(file =>
                    !string.Equals(file.File_Role, "ApprovedBy", StringComparison.OrdinalIgnoreCase) &&
                    (!string.Equals(file.File_Role, "ApproverSigned", StringComparison.OrdinalIgnoreCase) ||
                     (preparedByUsers.TryGetValue(file.Delivery_ID, out var preparedByUserId) &&
                      preparedByUserId.HasValue &&
                      file.CreatedByUserId.HasValue &&
                      preparedByUserId.Value != file.CreatedByUserId.Value)))
                .ToList();
            // Process each `deliveryReceipt` returned by the collection expression in the loop header.
            foreach (var deliveryReceipt in deliveryReceipts)
            {
                deliveryReceipt.ApprovalStatus = WorkflowRules.GetDeliveryReceiptApprovalStatus(
                    deliveryReceipt.Status,
                    deliveryFiles,
                    deliveryReceipt.Delivery_ID);
                deliveryReceipt.Status = WorkflowRules.GetDeliveryReceiptStatus(
                    WorkflowRules.HasReceivedByPhoto(deliveryFiles, deliveryReceipt.Delivery_ID));
            }
            var collectionReceipts = jobOrderIds.Count == 0
                ? new List<Models.CollectionReceipt>()
                : await _localDb.CollectionReceipt
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(record => jobOrderIds.Contains(record.JobOrderID))
                    .ToListAsync();
            var serviceInvoices = jobOrderIds.Count == 0
                ? new List<Models.ServiceInvoice>()
                : await _localDb.ServiceInvoice
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(record => record.JobOrder_ID.HasValue && jobOrderIds.Contains(record.JobOrder_ID.Value))
                    .ToListAsync();
            var serviceInvoiceFiles = jobOrderIds.Count == 0
                    ? new List<Models.ServiceInvoiceFile>()
                    : await _localDb.ServiceInvoiceFiles
                        .AsNoTracking()
                        // Apply this predicate in the database query before materializing results.
                        .Where(file => jobOrderIds.Contains(file.JobOrderID))
                        .ToListAsync();
            var serviceInvoiceIds = serviceInvoices
                    .Select(record => record.ServiceInvoice_ID)
                .ToList();
            var serviceInvoiceItems = serviceInvoiceIds.Count == 0
                ? new List<Models.ServiceInvoiceItem>()
                : await _localDb.ServiceInvoiceItems
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(item => serviceInvoiceIds.Contains(item.ServiceInvoice_ID))
                    .ToListAsync();
            var installationReports = jobOrderIds.Count == 0
                ? new List<Models.InstallationReport>()
                : await _localDb.InstallationReport
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(record => jobOrderIds.Contains(record.JobOrder_ID))
                    .ToListAsync();
            var installationReportIds = installationReports
                .Select(record => record.InstallationReport_ID)
                .ToArray();
            var installationReportFiles = installationReportIds.Length == 0
                ? new List<Models.InstallationReportFile>()
                : await _localDb.InstallationReportFiles
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(file => installationReportIds.Contains(file.InstallationReport_ID))
                    .ToListAsync();
            // Process each `file` returned by the collection expression in the loop header.
            foreach (var file in installationReportFiles)
            {
                file.File_Data = _encryptionService.TryDecrypt(file.File_Data);
            }
            // Process each `report` returned by the collection expression in the loop header.
            foreach (var report in installationReports)
            {
                var reportSignatures = installationReportFiles
                    // Apply this predicate in the database query before materializing results.
                    .Where(file => file.InstallationReport_ID == report.InstallationReport_ID)
                    .ToList();
                var approverSignature = reportSignatures.FirstOrDefault(file =>
                    string.Equals(file.File_Role, "ApproverSigned", StringComparison.OrdinalIgnoreCase));
                report.ApprovedBy = approverSignature?.CreatedBy;
                report.SignatureFiles = reportSignatures
                    // Apply this predicate in the database query before materializing results.
                    .Where(file => !string.Equals(file.File_Role, "ApprovedBy", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            // Enter this branch when `!string.IsNullOrWhiteSpace(client?.Business_Style) && client.Business_Style.StartsWith(, StringComparison.Ordinal)` is true.
            if (!string.IsNullOrWhiteSpace(client?.Business_Style) &&
                client.Business_Style.StartsWith("CfDJ8", StringComparison.Ordinal))
            {
                client.Business_Style = _encryptionService.TryDecrypt(client.Business_Style);
            }

            // Enter this branch when `!string.IsNullOrEmpty(quotation.Client_Subject) && quotation.Client_Subject.StartsWith(, StringComparison.Ordinal)` is true.
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
                        // Filter the query to rows satisfying `.Where(item => item.JobOrder_ID == jobOrder.JobOrder_ID) .ToList() }` before materializing the results.
                        .Where(item => item.JobOrder_ID == jobOrder.JobOrder_ID)
                        .ToList()
                }).ToList(),
                DeliveryReceipts = deliveryReceipts.Select(receipt => new ProjectViewerDeliveryReceiptDto
                {
                    Record = receipt,
                    Items = deliveryReceiptItems
                        // Apply this predicate in the database query before materializing results.
                        .Where(item => item.Delivery_ID == receipt.Delivery_ID)
                        .ToList(),
                    Files = deliveryFiles
                        // Filter the query to rows satisfying `.Where(file => file.Delivery_ID == receipt.Delivery_ID) .ToList() }` before materializing the results.
                        .Where(file => file.Delivery_ID == receipt.Delivery_ID)
                        .ToList()
                }).ToList(),
                CollectionReceipts = collectionReceipts,
                ServiceInvoices = serviceInvoices,
                ServiceInvoiceItems = serviceInvoiceItems,
                ServiceInvoiceFiles = serviceInvoiceFiles,
                InstallationReports = installationReports,
                QuotationStores = stores,
                QuotationItems = quotationItems,
                Items = items
            });
        }

    }
}
