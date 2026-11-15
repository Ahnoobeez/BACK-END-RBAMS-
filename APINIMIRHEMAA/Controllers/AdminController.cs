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
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize(Policy = "admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private LocalDbContext _Localdb;
        private CloudDbContext _Clouddb;
        private readonly AccessPermissionService _accessPermissions;
        private readonly EncryptionService _encryptionService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            LocalDbContext Localcontext,
            CloudDbContext Cloudcontext,
            AccessPermissionService accessPermissions,
            EncryptionService encryptionService,
            ILogger<AdminController> logger)
        {
            _Localdb = Localcontext;
            _Clouddb = Cloudcontext;
            _accessPermissions = accessPermissions;
            _encryptionService = encryptionService;
            _logger = logger;
        }

        [HttpGet("GetLocalAuditTrail")]
        public List<AuditTrailEntryDTO> GetAllLocalAuditTrail()
        {
            var records = _Localdb.AuditTrail
                .AsNoTracking()
                .OrderByDescending(record => record.Date)
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

        [HttpGet("GetReturnedMaterials")]
        public async Task<ActionResult<List<MaterialRequesition_Slip>>> GetReturnedMaterials(
            CancellationToken cancellationToken)
        {
            var records = await _Localdb.MaterialRequisition_Slip
                .AsNoTracking()
                .Where(slip =>
                    slip.FromWho != null && slip.FromWho.Trim().ToUpper() == "ADMIN" &&
                    slip.ToWho.Trim().ToUpper() == "SUPPLIER" &&
                    slip.Status != null && slip.Status.Trim().ToUpper() == "APPROVED")
                .OrderByDescending(slip => slip.Date)
                .ThenByDescending(slip => slip.MR_ID)
                .ToListAsync(cancellationToken);
            return Ok(records);
        }

        [HttpPost("AddMaterialRequisitionBatch")]
        public async Task<IActionResult> AddMaterialRequisitionBatch(
            [FromBody] List<AdminMaterialRequisitionRequest> requests,
            CancellationToken cancellationToken)
        {
            if (requests == null || requests.Count == 0)
            {
                return BadRequest("Add at least one material before submitting.");
            }

            using var transaction = await _Localdb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var materialIds = requests.Select(request => request.Material_ID).Distinct().ToList();
            var materials = await _Localdb.Materials
                .Where(material => materialIds.Contains(material.Material_ID))
                .ToDictionaryAsync(material => material.Material_ID, cancellationToken);
            var inventories = await _Localdb.Inventory
                .Where(inventory => materialIds.Contains(inventory.Material_ID))
                .ToDictionaryAsync(inventory => inventory.Material_ID, cancellationToken);
            var pendingDemandByMaterial = new Dictionary<int, int>();
            var slips = new List<MaterialRequesition_Slip>(requests.Count);
            var submitter = User.FindFirstValue(ClaimTypes.Name);
            var today = DateOnly.FromDateTime(DateTime.Now);

            for (var index = 0; index < requests.Count; index++)
            {
                var request = requests[index];
                var label = $"Row {index + 1}";
                if (string.IsNullOrWhiteSpace(request.Control_Number))
                    return BadRequest($"{label}: control number is required.");
                if (!materials.TryGetValue(request.Material_ID, out var material) ||
                    !inventories.TryGetValue(request.Material_ID, out var inventory))
                    return BadRequest($"{label}: choose a material with a Warehouse inventory record.");
                if (request.Quantity <= 0)
                    return BadRequest($"{label}: quantity must be greater than zero.");
                if (request.Unit_Cost < 0 ||
                    (long)request.Quantity * request.Unit_Cost > int.MaxValue)
                    return BadRequest($"{label}: unit cost or total cost exceeds the supported limit.");

                if (!pendingDemandByMaterial.ContainsKey(request.Material_ID))
                {
                    pendingDemandByMaterial[request.Material_ID] = await _Localdb.MaterialRequisition_Slip
                        .Where(slip => slip.Material_ID == request.Material_ID &&
                            (slip.Status == null || slip.Status.Trim() == "" ||
                             slip.Status.Trim().ToUpper() == "PENDING"))
                        .SumAsync(slip => slip.Quantity ?? 0, cancellationToken);
                }
                pendingDemandByMaterial[request.Material_ID] += request.Quantity;
                inventory.Request = pendingDemandByMaterial[request.Material_ID];

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
                    Date = today,
                    Status = "Pending"
                });
            }

            _Localdb.MaterialRequisition_Slip.AddRange(slips);
            await _Localdb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(new { saved = slips.Count });
        }

        [HttpGet("GetLocalAuditTrailByID")]
        public ActionResult<AuditTrailEntryDTO> GetLocalAuditTrailByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid Audit Trail ID.");
            }
            var auditTrail = _Localdb.AuditTrail.FirstOrDefault(x => x.AuditTrail_ID == id);
            if (auditTrail == null)
            {
                return NotFound();
            }
            var legacyLabel = ResolveLegacyAuditLabels(new[] { auditTrail })
                .GetValueOrDefault(auditTrail.AuditTrail_ID);
            return Ok(MapAuditTrailEntry(auditTrail, legacyLabel));
        }

        private Dictionary<int, string> ResolveLegacyAuditLabels(
            IReadOnlyCollection<AuditTrail> records)
        {
            var references = records
                .Select(record => new
                {
                    record.AuditTrail_ID,
                    Reference = ParseLegacyAuditReference(record.Action)
                })
                .Where(item => item.Reference != null)
                .Select(item => new
                {
                    item.AuditTrail_ID,
                    Entity = item.Reference!.Value.Entity,
                    Id = item.Reference.Value.Id
                })
                .ToList();

            var quotationIds = references
                .Where(item => item.Entity == nameof(Quotation))
                .Select(item => item.Id)
                .Distinct()
                .ToList();
            var storeIds = references
                .Where(item => item.Entity == nameof(Quotation_Store))
                .Select(item => item.Id)
                .Distinct()
                .ToList();
            var itemIds = references
                .Where(item => item.Entity == nameof(Quotation_Items))
                .Select(item => item.Id)
                .Distinct()
                .ToList();
            var materialIds = references
                .Where(item => item.Entity == nameof(QuotationItemMaterial))
                .Select(item => item.Id)
                .Distinct()
                .ToList();

            var materialsById = materialIds.Count == 0
                ? new Dictionary<int, QuotationItemMaterial>()
                : _Localdb.QuotationItemMaterials.AsNoTracking()
                    .Where(item => materialIds.Contains(item.QuotationItemMaterial_ID))
                    .ToDictionary(item => item.QuotationItemMaterial_ID);
            var relatedItemIds = itemIds
                .Concat(materialsById.Values.Select(material => material.QI_ID))
                .Distinct()
                .ToList();
            var itemsById = relatedItemIds.Count == 0
                ? new Dictionary<int, Quotation_Items>()
                : _Localdb.Quotation_Items.AsNoTracking()
                    .Where(item => relatedItemIds.Contains(item.QI_ID))
                    .ToDictionary(item => item.QI_ID);
            var relatedStoreIds = storeIds
                .Concat(itemsById.Values.Select(item => item.QS_ID))
                .Concat(materialsById.Values
                    .Where(material => itemsById.ContainsKey(material.QI_ID))
                    .Select(material => itemsById[material.QI_ID].QS_ID))
                .Distinct()
                .ToList();
            var storesById = relatedStoreIds.Count == 0
                ? new Dictionary<int, Quotation_Store>()
                : _Localdb.Quotation_Store.AsNoTracking()
                    .Where(store => relatedStoreIds.Contains(store.QS_ID))
                    .ToDictionary(store => store.QS_ID);
            quotationIds.AddRange(storesById.Values.Select(store => store.Quotation_ID));
            quotationIds = quotationIds.Distinct().ToList();

            var quotationsById = quotationIds.Count == 0
                ? new Dictionary<int, Quotation>()
                : _Localdb.Quotation.AsNoTracking()
                    .Where(quotation => quotationIds.Contains(quotation.Quotation_ID))
                    .ToDictionary(quotation => quotation.Quotation_ID);
            var clientIds = quotationsById.Values
                .Select(quotation => quotation.Client_ID)
                .Distinct()
                .ToList();
            var clientsById = clientIds.Count == 0
                ? new Dictionary<int, ClientEntity>()
                : _Localdb.Clients.AsNoTracking()
                    .Where(client => clientIds.Contains(client.Client_ID))
                    .ToDictionary(client => client.Client_ID);

            var labels = new Dictionary<int, string>();
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
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!.Trim()));
                if (!string.IsNullOrWhiteSpace(label))
                {
                    labels[reference.AuditTrail_ID] = label;
                }
            }

            return labels;
        }

        private static (string Entity, int Id)? ParseLegacyAuditReference(string? action)
        {
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

        private AuditTrailEntryDTO MapAuditTrailEntry(
            AuditTrail record,
            string? legacyDocumentLabel)
        {
            const string detailsPrefix = "AUDIT_DETAILS_V1:";
            AuditTrailDetailsDTO? details = null;
            var storedAction = record.Action ?? "";
            if (storedAction.StartsWith(detailsPrefix, StringComparison.Ordinal))
            {
                try
                {
                    details = JsonSerializer.Deserialize<AuditTrailDetailsDTO>(
                        storedAction[detailsPrefix.Length..]);
                }
                catch (JsonException exception)
                {
                    Console.Error.WriteLine(
                        $"Audit trail details could not be read for record {record.AuditTrail_ID}: {exception.Message}");
                }
            }

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

        private static List<AuditTrailEntryDTO> ConsolidateAuditBatchEntries(
            List<AuditTrailEntryDTO> entries)
        {
            var consolidated = new List<AuditTrailEntryDTO>();
            foreach (var group in entries
                         .Where(entry => !string.IsNullOrWhiteSpace(entry.Details?.BatchId))
                         .GroupBy(entry => new
                         {
                             entry.Details!.BatchId,
                             entry.Name,
                             entry.Department
                         }))
            {
                var batch = group.ToList();
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
                    .OrderBy(entry => entry.Date)
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
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Details?.BatchId))
                .Select(entry => entry.AuditTrail_ID)
                .ToHashSet();
            consolidated.AddRange(entries.Where(entry => !batchedIds.Contains(entry.AuditTrail_ID)));
            return consolidated
                .OrderByDescending(entry => entry.Date)
                .ThenByDescending(entry => entry.Time)
                .ToList();
        }

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

        private static string ResolveAuditFormName(IEnumerable<string> entities)
        {
            var names = entities.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (names.Contains(nameof(PurchaseOrder)))
            {
                return "Purchase Order";
            }
            if (names.Contains(nameof(Conforme)))
            {
                return "Conforme";
            }
            if (names.Contains(nameof(Quotation)) ||
                names.Contains(nameof(Quotation_Store)) ||
                names.Contains(nameof(Quotation_Items)) ||
                names.Contains(nameof(QuotationItemMaterial)))
            {
                return "Quotation";
            }
            if (names.Contains(nameof(JobOrder)) || names.Contains(nameof(JobOrderItems)))
            {
                return "Job Order";
            }
            if (names.Contains(nameof(DeliveryReceipt)) ||
                names.Contains(nameof(DeliveryReceiptItems)) ||
                names.Contains(nameof(DeliveryFiles)))
            {
                return "Delivery Receipt";
            }
            if (names.Contains(nameof(ServiceInvoice)) ||
                names.Contains(nameof(ServiceInvoiceItem)) ||
                names.Contains(nameof(ServiceInvoiceFile)))
            {
                return "Service Invoice";
            }
            if (names.Contains(nameof(Installation_Schedule)) ||
                names.Contains(nameof(InstallationScheduleEmployee)))
            {
                return "Installation Schedule";
            }
            if (names.Contains(nameof(CollectionReceipt)))
            {
                return "Collection Receipt";
            }
            if (names.Contains(nameof(InstallationReport)))
            {
                return "Installation Report";
            }
            if (names.Contains(nameof(MaterialRequesition_Slip)))
            {
                return "Material Requisition";
            }
            if (names.Contains(nameof(StockTransfer_Slip)))
            {
                return "Stock Transfer";
            }

            return FormatAuditEntityName(names.FirstOrDefault() ?? "Form");
        }

        private void DecryptProtectedValues(Dictionary<string, string?>? values)
        {
            if (values == null)
            {
                return;
            }

            foreach (var key in values.Keys.ToList())
            {
                if (values[key] is { } value)
                {
                    values[key] = DecryptProtectedSegments(value);
                }
            }
        }

        private string DecryptProtectedSegments(string value) =>
            Regex.Replace(value, @"CfDJ8[A-Za-z0-9_-]+={0,2}", match =>
                DecryptProtectedSubject(match.Value));

        private string DecryptProtectedSubject(string? value)
        {
            if (string.IsNullOrEmpty(value) ||
                !value.StartsWith("CfDJ8", StringComparison.Ordinal))
            {
                return value ?? "";
            }

            return _encryptionService.TryDecrypt(value);
        }

        private static string FormatLegacyAuditAction(string action, string? documentLabel)
        {
            var match = Regex.Match(
                action,
                @"^(?<entity>[A-Za-z0-9_]+)#\s*\d+\s*(?<description>.*)$");
            if (!match.Success)
            {
                return action;
            }

            return ComposeAuditAction(
                FormatAuditEntityName(match.Groups["entity"].Value),
                documentLabel,
                match.Groups["description"].Value);
        }

        private static string FormatAuditAction(AuditTrailDetailsDTO details)
        {
            var description = details.ActionDescription;
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

        private static string ComposeAuditAction(
            string formName,
            string? documentLabel,
            string? description)
        {
            var parts = new List<string> { formName };
            if (!string.IsNullOrWhiteSpace(documentLabel) &&
                !documentLabel.Equals(formName, StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(documentLabel);
            }
            if (!string.IsNullOrWhiteSpace(description))
            {
                parts.Add(description);
            }

            return string.Join(" - ", parts);
        }

        private static string FormatAuditEntityName(string entityName) =>
            string.Concat(entityName.Select((character, index) =>
                index > 0 && char.IsUpper(character) &&
                !char.IsUpper(entityName[index - 1])
                    ? $" {character}"
                    : character.ToString()))
                .Replace('_', ' ')
                .Trim();

        [HttpPost("AddLocalAuditTrail")]
        public ActionResult<AuditTrail> AddLocalAuditTrail(AuditTrail auditTrail)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(auditTrail);
            _Localdb.SaveChanges();
            return Ok(auditTrail);
        }


        [HttpGet("GetLocalEmployees")]
        public List<Employee> GetAllLocalEmployee()
        {
            return _Localdb.Employee.ToList();
        }

        [HttpPost("AddLocalEmployee")]
        public async Task<ActionResult<Employee>> AddLocalEmployee(
            [FromBody] Employee? employee,
            CancellationToken cancellationToken)
        {
            if (employee == null)
            {
                return BadRequest(new { message = "Employee details are required." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (employee.Employee_Id < 0)
            {
                return BadRequest(new { message = "Employee ID cannot be negative." });
            }

            if (string.IsNullOrWhiteSpace(employee.FirstName) ||
                string.IsNullOrWhiteSpace(employee.LastName) ||
                string.IsNullOrWhiteSpace(employee.status))
            {
                return BadRequest(new { message = "First name, last name, and status are required." });
            }

            if (!employee.Department_ID.HasValue ||
                !await _Localdb.Departments.AnyAsync(
                    department => department.Department_ID == employee.Department_ID.Value,
                    cancellationToken))
            {
                return BadRequest(new { message = "Select a valid department." });
            }

            if (!employee.Role_ID.HasValue ||
                !await _Localdb.Roles.AnyAsync(
                    role => role.Role_ID == employee.Role_ID.Value,
                    cancellationToken))
            {
                return BadRequest(new { message = "Select a valid role." });
            }

            await using var transaction = await _Localdb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            if (employee.Employee_Id == 0)
            {
                var lastEmployeeId = await _Localdb.Employee
                    .MaxAsync(record => (int?)record.Employee_Id, cancellationToken) ?? 0;
                employee.Employee_Id = checked(lastEmployeeId + 1);
            }
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

            _Localdb.Employee.Add(employee);
            await _Localdb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(employee);
        }

        [HttpGet("GetLocalRoles")]
        public List<Roles> GetAllLocalRoles()
        {
            return _Localdb.Roles.ToList();
        }

        [HttpGet("GetLocalDeparments")]
        public List<Departments> GetAllLocalDepartments()
        {
            return _Localdb.Departments.ToList();
        }

        [HttpGet("ClientAccessOptions")]
        public async Task<IActionResult> GetClientAccessOptions(CancellationToken cancellationToken)
        {
            var marketingEmployees = await (
                from account in MarketingUsers().AsNoTracking()
                join employee in _Localdb.Employee.AsNoTracking()
                    on account.Employee_ID equals employee.Employee_Id
                orderby employee.LastName, employee.FirstName
                select new ClientAccessEmployeeDto
                {
                    User_ID = account.User_ID,
                    Employee_ID = employee.Employee_Id,
                    Employee_Name = ((employee.FirstName ?? "") + " " + (employee.LastName ?? "")).Trim(),
                    Username = account.Username,
                    IsActive = account.IsActive
                }).ToListAsync(cancellationToken);

            var clientOwnerIds = await _Localdb.Clients
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(client => client.CreatedByUserId.HasValue)
                .Select(client => client.CreatedByUserId.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
            var marketingOwners = marketingEmployees
                .Where(employee => clientOwnerIds.Contains(employee.User_ID))
                .ToList();

            return Ok(new
            {
                marketingOwners,
                marketingEmployees
            });
        }

        [HttpGet("ClientAccessOwners/{userId:int}/Clients")]
        public async Task<IActionResult> GetClientAccessOwnerClients(
            int userId,
            CancellationToken cancellationToken)
        {
            if (!await MarketingUsers().AnyAsync(
                    account => account.User_ID == userId,
                    cancellationToken))
            {
                return BadRequest(new { message = "Select an active Marketing employee with a user account." });
            }

            var clients = await _Localdb.Clients
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(client => client.CreatedByUserId == userId)
                .OrderBy(client => client.Client_Name)
                .Select(client => new
                {
                    client.Client_ID,
                    client.Client_Name
                })
                .ToListAsync(cancellationToken);

            return Ok(clients);
        }

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
                orderby client.Client_Name, employee.LastName, employee.FirstName
                select new ClientAccessGrantDto
                {
                    Client_ID = client.Client_ID,
                    Client_Name = client.Client_Name,
                    User_ID = account.User_ID,
                    Employee_Name = ((employee.FirstName ?? "") + " " + (employee.LastName ?? "")).Trim(),
                    Username = account.Username,
                    GrantedAtUtc = grant.GrantedAtUtc
                }).ToListAsync(cancellationToken);

            return Ok(grants);
        }

        [HttpPost("ClientAccessGrants")]
        public async Task<IActionResult> GrantClientAccess(
            [FromBody] SaveClientAccessGrantDto request,
            CancellationToken cancellationToken)
        {
            if (request.Client_ID <= 0 || request.User_ID <= 0)
            {
                return BadRequest(new { message = "Select a valid client and Marketing employee." });
            }

            if (!await _Localdb.Clients.IgnoreQueryFilters()
                    .AnyAsync(client => client.Client_ID == request.Client_ID, cancellationToken))
            {
                return NotFound(new { message = "The selected client could not be found." });
            }

            if (!await ActiveMarketingUsers()
                    .AnyAsync(account => account.User_ID == request.User_ID, cancellationToken))
            {
                return BadRequest(new { message = "Access can only be granted to an active Marketing employee with a user account." });
            }

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
                await _Localdb.SaveChangesAsync(cancellationToken);
            }

            var cloudSyncPending = false;
            try
            {
                var cloudClientExists = await _Clouddb.Clients.IgnoreQueryFilters()
                    .AnyAsync(client => client.Client_ID == request.Client_ID, cancellationToken);
                var cloudMarketingUserExists = await _Clouddb.Users.AsNoTracking()
                    .AnyAsync(account => account.User_ID == request.User_ID && account.IsActive, cancellationToken);

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
                    await _Clouddb.SaveChangesAsync(cancellationToken);
                }
            }
            catch (SqlException exception)
            {
                cloudSyncPending = true;
                _logger.LogWarning(exception, "Client access was saved locally, but cloud synchronization failed.");
            }
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

        [HttpPost("ClientAccessGrants/Bulk")]
        public async Task<IActionResult> GrantClientAccessToMultipleEmployees(
            [FromBody] SaveClientAccessGrantsDto request,
            CancellationToken cancellationToken)
        {
            var clientIds = request.Client_IDs?.Distinct().ToList() ?? new List<int>();
            var userIds = request.User_IDs?.Distinct().ToList() ?? new List<int>();
            if (request.OwnerUser_ID <= 0 ||
                clientIds.Count == 0 ||
                userIds.Count == 0 ||
                clientIds.Any(id => id <= 0) ||
                userIds.Any(id => id <= 0) ||
                userIds.Contains(request.OwnerUser_ID))
            {
                return BadRequest(new { message = "Select an owner, at least one client, and at least one different Marketing employee." });
            }

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
            if (ownedClientCount != clientIds.Count)
            {
                return BadRequest(new { message = "One or more selected clients do not belong to the selected employee." });
            }

            var eligibleUserIds = await ActiveMarketingUsers()
                .Where(account => userIds.Contains(account.User_ID))
                .Select(account => account.User_ID)
                .ToListAsync(cancellationToken);
            if (eligibleUserIds.Count != userIds.Count)
            {
                return BadRequest(new { message = "One or more selected recipients are not active Marketing employees with user accounts." });
            }

            var existingLocalGrants = await _Localdb.ClientAccessGrants
                .Where(grant =>
                    clientIds.Contains(grant.Client_ID) &&
                    userIds.Contains(grant.User_ID))
                .ToListAsync(cancellationToken);
            var existingLocalPairs = existingLocalGrants
                .Select(grant => (grant.Client_ID, grant.User_ID))
                .ToHashSet();
            foreach (var clientId in clientIds)
            {
                foreach (var userId in userIds)
                {
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
            if (_Localdb.ChangeTracker.HasChanges())
            {
                await _Localdb.SaveChangesAsync(cancellationToken);
            }

            var cloudSyncPending = false;
            try
            {
                var cloudClientIds = await _Clouddb.Clients
                    .IgnoreQueryFilters()
                    .Where(client =>
                        clientIds.Contains(client.Client_ID) &&
                        client.CreatedByUserId == request.OwnerUser_ID)
                    .Select(client => client.Client_ID)
                    .ToListAsync(cancellationToken);
                var cloudUserIds = await _Clouddb.Users.AsNoTracking()
                    .Where(account => userIds.Contains(account.User_ID) && account.IsActive)
                    .Select(account => account.User_ID)
                    .ToListAsync(cancellationToken);
                if (cloudClientIds.Count > 0 && cloudUserIds.Count > 0)
                {
                    var existingCloudGrants = await _Clouddb.ClientAccessGrants
                        .Where(grant =>
                            cloudClientIds.Contains(grant.Client_ID) &&
                            cloudUserIds.Contains(grant.User_ID))
                        .ToListAsync(cancellationToken);
                    var existingCloudPairs = existingCloudGrants
                        .Select(grant => (grant.Client_ID, grant.User_ID))
                        .ToHashSet();
                    foreach (var clientId in cloudClientIds)
                    {
                        foreach (var userId in cloudUserIds)
                        {
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
                    if (_Clouddb.ChangeTracker.HasChanges())
                    {
                        await _Clouddb.SaveChangesAsync(cancellationToken);
                    }
                }
            }
            catch (SqlException exception)
            {
                cloudSyncPending = true;
                _logger.LogWarning(exception, "Client access was saved locally, but cloud synchronization failed.");
            }
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
            if (localGrant != null)
            {
                _Localdb.ClientAccessGrants.Remove(localGrant);
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
                if (cloudGrant != null)
                {
                    _Clouddb.ClientAccessGrants.Remove(cloudGrant);
                    await _Clouddb.SaveChangesAsync(cancellationToken);
                    cloudGrantRemoved = true;
                }
            }
            catch (SqlException exception)
            {
                cloudSyncPending = true;
                _logger.LogWarning(exception, "Client access was revoked locally, but cloud synchronization failed.");
            }
            catch (TimeoutException exception)
            {
                cloudSyncPending = true;
                _logger.LogWarning(exception, "Client access was revoked locally, but cloud synchronization timed out.");
            }

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

        [HttpGet("AccessPermissions")]
        public async Task<ActionResult<List<AccessPermissionValueDto>>> GetAccessPermissions(
            [FromQuery] int? roleId,
            [FromQuery] int? departmentId,
            [FromQuery] int? userId,
            CancellationToken cancellationToken)
        {
            if (userId.HasValue)
            {
                if (!await _Localdb.Users.AnyAsync(user => user.User_ID == userId.Value, cancellationToken))
                    return NotFound(new { message = "The selected user account could not be found." });
            }
            else if (!roleId.HasValue && !departmentId.HasValue)
            {
                return BadRequest(new { message = "Select a role, department, or user." });
            }

            if (roleId.HasValue &&
                !await _Localdb.Roles.AnyAsync(role => role.Role_ID == roleId.Value, cancellationToken))
                return BadRequest(new { message = "The selected role could not be found." });
            if (departmentId.HasValue &&
                !await _Localdb.Departments.AnyAsync(department => department.Department_ID == departmentId.Value, cancellationToken))
                return BadRequest(new { message = "The selected department could not be found." });

            return Ok(await _accessPermissions.GetEffectivePermissionsAsync(
                userId, roleId, departmentId, cancellationToken));
        }

        [HttpPut("AccessPermissions")]
        public async Task<IActionResult> SaveAccessPermissions(
            [FromBody] SaveAccessPermissionsDto request,
            CancellationToken cancellationToken)
        {
            if (request.User_ID.HasValue && (request.Role_ID.HasValue || request.Department_ID.HasValue))
                return BadRequest(new { message = "User-specific changes cannot update role or department permissions." });
            if (!request.User_ID.HasValue && !request.Role_ID.HasValue && !request.Department_ID.HasValue)
                return BadRequest(new { message = "Select a role, department, or user before saving permissions." });

            if (request.User_ID.HasValue &&
                !await _Localdb.Users.AnyAsync(user => user.User_ID == request.User_ID.Value, cancellationToken))
                return NotFound(new { message = "The selected user account could not be found." });
            if (request.Role_ID.HasValue &&
                !await _Localdb.Roles.AnyAsync(role => role.Role_ID == request.Role_ID.Value, cancellationToken))
                return BadRequest(new { message = "The selected role could not be found." });
            if (request.Department_ID.HasValue &&
                !await _Localdb.Departments.AnyAsync(department => department.Department_ID == request.Department_ID.Value, cancellationToken))
                return BadRequest(new { message = "The selected department could not be found." });

            var permissions = request.Permissions ?? new List<AccessPermissionValueDto>();
            var expectedCount = AccessPermissionCatalog.Services.Length * AccessPermissionCatalog.Actions.Length;
            var distinctKeys = permissions
                .Select(permission => AccessPermissionService.Key(permission.ServiceKey, permission.ActionKey))
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (permissions.Count != expectedCount ||
                distinctKeys != expectedCount ||
                permissions.Any(permission =>
                    !AccessPermissionCatalog.IsValid(permission.ServiceKey, permission.ActionKey)))
            {
                return BadRequest(new { message = "The permission matrix is incomplete or contains an unknown service or action." });
            }

            await using var transaction = await _Localdb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var existing = await _Localdb.AccessPermissions
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
            await _Localdb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new { message = "Access permissions saved successfully." });
        }

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

        private IQueryable<Users> ActiveMarketingUsers() =>
            MarketingUsers().Where(account => account.IsActive);
    }
}
