using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO.Admin;
using APINIMIRHEMAA.DTO.AuditTrail;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public AdminController(
            LocalDbContext Localcontext,
            CloudDbContext Cloudcontext,
            AccessPermissionService accessPermissions,
            EncryptionService encryptionService)
        {
            _Localdb = Localcontext;
            _Clouddb = Cloudcontext;
            _accessPermissions = accessPermissions;
            _encryptionService = encryptionService;
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
                DecryptClientSubjects(details.PreviousValues);
                DecryptClientSubjects(details.CurrentValues);
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
                ? FormatLegacyAuditAction(storedAction, legacyDocumentLabel)
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

        private void DecryptClientSubjects(Dictionary<string, string?>? values)
        {
            if (values == null)
            {
                return;
            }

            var subjectKeys = values.Keys
                .Where(key => key.Equals("Client_Subject", StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var key in subjectKeys)
            {
                values[key] = DecryptProtectedSubject(values[key]);
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
    }
}
