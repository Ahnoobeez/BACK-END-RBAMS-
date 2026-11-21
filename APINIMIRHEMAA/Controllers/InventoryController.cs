using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;
using System.Text;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize(Policy = "warehouse")]
    [Route("api/[controller]")]
    [ApiController]
    // Coordinates the inventory controller API endpoints, including request handling and response generation.
    public class InventoryController : ControllerBase
    {
        // Injected or cached localdb dependency/state used by this class.
        private LocalDbContext _Localdb;
        // Injected or cached cloud db dependency/state used by this class.
        private CloudDbContext _CloudDb;
        // Injected or cached notification service dependency/state used by this class.
        private readonly NotificationService _notificationService;

        // Initializes a new inventory controller instance.
        public InventoryController(
            LocalDbContext Localcontext,
            CloudDbContext Cloudcontext,
            NotificationService notificationService)
        {
            _Localdb = Localcontext;
            _CloudDb = Cloudcontext;
            _notificationService = notificationService;
        }

        // Calculates the purchase amount.
        private static string? CalculatePurchaseAmount(Purchased_Material purchase, out int amount)
        {
            amount = 0;
            // Exit the current operation when `purchase.Quantity <= 0` is true.
            if (purchase.Quantity <= 0)
                return "Quantity must be greater than zero.";
            // Exit the current operation when `purchase.Unit_price < 0` is true.
            if (purchase.Unit_price < 0)
                return "Unit price cannot be negative.";

            var total = (long)purchase.Quantity * purchase.Unit_price;
            // Exit the current operation when `total > int.MaxValue` is true.
            if (total > int.MaxValue)
                return "The purchase amount exceeds the supported limit.";

            amount = (int)total;
            return null;
        }

        // --------------------------- INVENTORY HELPERS (LOCAL) ---------------------------

        private static bool IsPending(string? status) =>
            string.IsNullOrWhiteSpace(status) ||
            string.Equals(status.Trim(), "Pending", StringComparison.OrdinalIgnoreCase);

        // Checks whether the current user is the warehouse department head.
        private bool IsWarehouseDepartmentHead() =>
            _Localdb.IsDepartmentManager &&
            ModuleAccess.GetModuleForDepartment(_Localdb.CurrentDepartmentName) == "warehouse";

        // Retrieves the pending demand.
        private int GetPendingDemand(int materialId, int? excludingRequisitionId = null)
        {
            var query = _Localdb.MaterialRequisition_Slip
                .AsNoTracking()
                .IgnoreQueryFilters()
                // Apply this predicate in the database query before materializing results.
                .Where(s => s.Material_ID == materialId &&
                    (s.Status == null || s.Status.Trim() == "" ||
                     s.Status.Trim().ToUpper() == "PENDING"));

            // Enter this branch when `excludingRequisitionId.HasValue` is true.
            if (excludingRequisitionId.HasValue)
            {
                // Apply this predicate in the database query before materializing results.
                query = query.Where(s => s.MR_ID != excludingRequisitionId.Value);
            }

            return query.Sum(s => s.Quantity ?? 0);
        }

        // Reverses the inventory changes applied by an approved requisition.
        private string? ReverseApprovedRequisitionEffect(MaterialRequesition_Slip slip)
        {
            var inventory = _Localdb.Inventory.FirstOrDefault(x => x.Material_ID == slip.Material_ID);
            // Enter this branch when `inventory == null` is true.
            if (inventory == null)
                // Coordinates the found API endpoints, including request handling and response generation.
                return $"No inventory record found for Material_ID {slip.Material_ID}.";

            InventoryRules.Recalculate(inventory);
            var quantity = slip.Quantity ?? 0;
            // Exit the current operation when `quantity <= 0` is true.
            if (quantity <= 0)
                return $"Requisition {slip.MR_ID} has an invalid quantity.";

            // Enter this branch when `InventoryRules.IsAdminSupplierReturn(slip.FromWho, slip.ToWho)` is true.
            if (InventoryRules.IsAdminSupplierReturn(slip.FromWho, slip.ToWho))
            {
                // Exit the current operation when `(inventory.Request_Purchased ?? 0) < quantity` is true.
                if ((inventory.Request_Purchased ?? 0) < quantity)
                    return $"Requisition {slip.MR_ID} cannot be removed because its purchased-stock deduction is no longer recorded.";
                inventory.Request_Purchased -= quantity;
            }
            // Enter this branch when `InventoryRules.IsWarehouse(slip.FromWho)` is true.
            else if (InventoryRules.IsWarehouse(slip.FromWho))
            {
                var remaining = quantity;
                var fromBuffer = Math.Min(remaining, Math.Max(inventory.Request_Buffer ?? 0, 0));
                remaining -= fromBuffer;
                var fromPurchased = Math.Min(remaining, Math.Max(inventory.Request_Purchased ?? 0, 0));
                remaining -= fromPurchased;
                var fromBalance = Math.Min(remaining, Math.Max(inventory.Request_Balance ?? 0, 0));
                remaining -= fromBalance;
                // Exit the current operation when `remaining > 0` is true.
                if (remaining > 0)
                    return $"Requisition {slip.MR_ID} cannot be removed because its approved quantity is no longer recorded.";

                inventory.Request_Buffer = (inventory.Request_Buffer ?? 0) - fromBuffer;
                inventory.Request_Purchased = (inventory.Request_Purchased ?? 0) - fromPurchased;
                inventory.Request_Balance = (inventory.Request_Balance ?? 0) - fromBalance;

                var restoredToBuffer = Math.Min(
                    fromBuffer,
                    Math.Max(InventoryRules.BufferTarget - (inventory.Buffer ?? 0), 0));
                inventory.Buffer = (inventory.Buffer ?? 0) + restoredToBuffer;
                inventory.Purchased = (inventory.Purchased ?? 0) + fromBuffer - restoredToBuffer;
            }
            // Exit the current operation when `InventoryRules.IsWarehouse(slip.ToWho)` is true.
            else if (InventoryRules.IsWarehouse(slip.ToWho))
            {
                var returned = (inventory.Returned ?? 0) - quantity;
                // Exit the current operation when `returned < 0` is true.
                if (returned < 0)
                    return $"Requisition {slip.MR_ID} cannot be removed because its returned quantity is no longer recorded.";
                inventory.Returned = returned;
            }
            // Use this fallback path when the preceding condition is false.
            else
            {
                return $"Requisition {slip.MR_ID} does not involve the Warehouse.";
            }

            InventoryRules.Recalculate(inventory);
            return null;
        }


        // --------------------------- MATERIALS ---------------------------
        [HttpGet("GetLocalMaterials")]
        // Retrieves all local materials. Returns List<Materials>.
        public List<Materials> GetAllLocalMaterials()
        {
            return _Localdb.Materials.ToList();
        }

        /// <summary>Adds the local material.</summary>
        [HttpPost("AddLocalMaterials")]
        public ActionResult<Materials> AddLocalMaterial([FromBody] Materials materials)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Materials.Add(materials);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(materials);
        }

        /// <summary>Updates the local material.</summary>
        [HttpPost("UpdateLocalMaterials")]
        public ActionResult<Materials> UpdateLocalMaterial([FromBody] Materials materials)
        {
            // Return HTTP 400 Bad Request when `materials == null` is true.
            if (materials == null)
            {
                return BadRequest(materials);
            }

            var localMaterial = _Localdb.Materials.FirstOrDefault(x => x.Material_ID == materials.Material_ID);
            // Return HTTP 404 Not Found when `localMaterial == null` is true.
            if (localMaterial == null)
            {
                return NotFound();
            }

            localMaterial.Material_ID = materials.Material_ID;
            localMaterial.Code_Name = materials.Code_Name;
            localMaterial.Item_Name = materials.Item_Name;
            localMaterial.Category = materials.Category;
            localMaterial.Unit = materials.Unit;
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();

            return Ok(localMaterial);
        }

        /// <summary>Deletes the local material.</summary>
        [HttpPut("DeleteLocalMaterials")]
        public ActionResult<Materials> DeleteLocalMaterial(Int32 Id)
        {

            var localMaterial = _Localdb.Materials.FirstOrDefault(x => x.Material_ID == Id);
            // Return HTTP 404 Not Found when `localMaterial == null` is true.
            if (localMaterial == null)
            {
                return NotFound();
            }
            _Localdb.Remove(localMaterial);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();

            return NoContent();
        }

        /// <summary>Retrieves all materials from the cloud database.</summary>
        [HttpGet("GetCloudMaterials")]
        public List<Materials> GetAllCloudMaterials()
        {
            return _CloudDb.Materials.ToList();
        }

        /// <summary>Adds the cloud material.</summary>
        [HttpPost("AddCloudMaterials")]
        public ActionResult<Materials> AddCloudMaterial([FromBody] Materials materials)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Materials.Add(materials);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(materials);
        }

        /// <summary>Updates the cloud material.</summary>
        [HttpPost("UpdateCloudMaterials")]
        public ActionResult<Materials> UpdateCloudMaterial([FromBody] Materials materials)
        {
            // Return HTTP 400 Bad Request when `materials == null` is true.
            if (materials == null)
            {
                return BadRequest(materials);
            }

            var cloudMaterial = _CloudDb.Materials.FirstOrDefault(x => x.Material_ID == materials.Material_ID);
            // Return HTTP 404 Not Found when `cloudMaterial == null` is true.
            if (cloudMaterial == null)
            {
                return NotFound();
            }

            cloudMaterial.Material_ID = materials.Material_ID;
            cloudMaterial.Code_Name = materials.Code_Name;
            cloudMaterial.Item_Name = materials.Item_Name;
            cloudMaterial.Category = materials.Category;
            cloudMaterial.Unit = materials.Unit;
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();

            return Ok(cloudMaterial);
        }

        /// <summary>Deletes the cloud material.</summary>
        [HttpPut("DeleteCloudMaterials")]
        public ActionResult<Materials> DeleteCloudMaterial(Int32 Id)
        {

            var cloudMaterial = _CloudDb.Materials.FirstOrDefault(x => x.Material_ID == Id);
            // Return HTTP 404 Not Found when `cloudMaterial == null` is true.
            if (cloudMaterial == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(cloudMaterial);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();

            return NoContent();
        }

        // --------------------------- MATERIALS ---------------------------

        // --------------------------- INVENTORY ---------------------------

        [HttpGet("GetMaterialRequisitionDepartments")]
        // Retrieves material requisition departments. Inputs: cancellationToken (cancellation token for the asynchronous operation). Returns the HTTP response for the requested operation.
        public async Task<IActionResult> GetMaterialRequisitionDepartments(
            CancellationToken cancellationToken)
        {
            var departments = await _Localdb.Departments
                .AsNoTracking()
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(department => department.Department_Name)
                .Select(department => new
                {
                    department.Department_ID,
                    department.Department_Name
                })
                .ToListAsync(cancellationToken);

            return Ok(departments);
        }

        /// <summary>Retrieves the material requisition job orders.</summary>
        [HttpGet("GetMaterialRequisitionJobOrders")]
        public async Task<IActionResult> GetMaterialRequisitionJobOrders(
            CancellationToken cancellationToken)
        {
            var jobOrders = await (
                from jobOrder in _Localdb.JobOrder.IgnoreQueryFilters().AsNoTracking()
                join conforme in _Localdb.Conforme.IgnoreQueryFilters().AsNoTracking()
                    on jobOrder.Conforme_FileID equals conforme.FileID
                join quotation in _Localdb.Quotation.IgnoreQueryFilters().AsNoTracking()
                    on conforme.Quotation_ID equals quotation.Quotation_ID
                join client in _Localdb.Clients.IgnoreQueryFilters().AsNoTracking()
                    on quotation.Client_ID equals client.Client_ID
                let status = (jobOrder.Status ?? "").Trim().ToUpper()
                where status == "PENDING"
                    || status == "IN PROGRESS (0/2)"
                    || status == "IN PROGRESS (1/2)"
                    || (status == ""
                        && !jobOrder.Technical_Completed
                        && !jobOrder.Production_Completed)
                orderby jobOrder.JobOrder_ID descending
                select new
                {
                    JobOrderId = jobOrder.JobOrder_ID,
                    ClientName = client.Client_Name,
                    Title = jobOrder.Title,
                    Status = status == ""
                        ? "Pending"
                        : status == "PENDING"
                            ? "Pending"
                            : status == "IN PROGRESS (0/2)"
                                ? "In Progress (0/2)"
                                : "In Progress (1/2)"
                }).ToListAsync(cancellationToken);

            return Ok(jobOrders);
        }

        /// <summary>Retrieves all inventory records from the local database.</summary>
        [HttpGet("GetLocalInventory")]
        public List<Inventory> GetAllLocalInventory()
        {
            var inventoryRecords = _Localdb.Inventory.AsNoTracking().ToList();
            var pendingRequests = _Localdb.MaterialRequisition_Slip
                .AsNoTracking()
                .IgnoreQueryFilters()
                // Apply this predicate in the database query before materializing results.
                .Where(s => s.Status == null || s.Status.Trim() == "" ||
                    s.Status.Trim().ToUpper() == "PENDING")
                .GroupBy(s => s.Material_ID)
                .Select(group => new
                {
                    MaterialId = group.Key,
                    Quantity = group.Sum(s => s.Quantity ?? 0)
                })
                .ToList()
                .ToDictionary(group => group.MaterialId, group => group.Quantity);

            // Process each `inventory` returned by the collection expression in the loop header.
            foreach (var inventory in inventoryRecords)
            {
                inventory.Request = pendingRequests.TryGetValue(inventory.Material_ID, out var quantity)
                    ? quantity
                    : 0;
                InventoryRules.Recalculate(inventory);
            }

            return inventoryRecords;
        }

        /// <summary>Adds the local inventory.</summary>
        [HttpPost("AddLocalInventory")]
        public async Task<ActionResult<Inventory>> AddLocalInventory([FromBody] Inventory inventory)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            inventory.Beginning_Balance = 0;
            inventory.Buffer = 0;
            inventory.Purchased = 0;
            inventory.Request = 0;
            inventory.Returned = 0;
            inventory.Balance = 0;
            inventory.Request_Balance = inventory.Request_Purchased = inventory.Request_Buffer = 0;
            InventoryRules.Recalculate(inventory);

            _Localdb.Inventory.Add(inventory);
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();

            return Ok(inventory);
        }

        /// <summary>Updates the local inventory.</summary>
        [HttpPost("UpdateLocalInventory")]
        public async Task<ActionResult<Inventory>> UpdateLocalInventory([FromBody] Inventory incoming)
        {
            // Return HTTP 400 Bad Request when `incoming == null` is true.
            if (incoming == null)
            {
                return BadRequest("Inventory data is required.");
            }

            var existing = await _Localdb.Inventory
                .FirstOrDefaultAsync(x => x.Material_ID == incoming.Material_ID);
            // Return HTTP 404 Not Found when `existing == null` is true.
            if (existing == null)
            {
                return NotFound();
            }
            existing.Request = GetPendingDemand(existing.Material_ID);
            InventoryRules.Recalculate(existing);

            // Return HTTP 400 Bad Request when `(incoming.Buffer.HasValue && incoming.Buffer.Value != existing.Buffer) || (incoming.Balance.HasValue && incoming.Balance.Value != existing.Balance) || (incoming.Status != null && !string.Equals(incoming.Status, existing.Status, StringComparison.Ordinal))` is true.
            if ((incoming.Buffer.HasValue && incoming.Buffer.Value != existing.Buffer) ||
                (incoming.Balance.HasValue && incoming.Balance.Value != existing.Balance) ||
                (incoming.Status != null &&
                    !string.Equals(incoming.Status, existing.Status, StringComparison.Ordinal)))
            {
                return BadRequest("Buffer, balance, and status are managed by the inventory system and cannot be edited.");
            }

            // Return HTTP 400 Bad Request when `(incoming.Returned ?? 0) < 0` is true.
            if ((incoming.Returned ?? 0) < 0)
            {
                return BadRequest("Returned stock cannot be negative.");
            }

            // Validate the incoming values before applying them
            if (incoming.Beginning_Balance < existing.Request_Balance)
            {
                return BadRequest("Beginning balance cannot be lower than what has already been issued.");
            }

            existing.Beginning_Balance = incoming.Beginning_Balance;
            existing.Returned = incoming.Returned;
            // never copy Purchased, Request*, Balance

            InventoryRules.Recalculate(existing);
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();

            return Ok(existing);
        }

        /// <summary>Deletes the local inventory.</summary>
        [HttpPut("DeleteLocalInventory")]
        public ActionResult<Inventory> DeleteLocalInventory(Int32 Id)
        {

            var _inventory = _Localdb.Inventory.FirstOrDefault(x => x.Inventory_ID == Id);
            // Return HTTP 404 Not Found when `_inventory == null` is true.
            if (_inventory == null)
            {
                return NotFound();
            }
            _Localdb.Remove(_inventory);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();

            return NoContent();
        }

        /// <summary>Retrieves all inventory records from the cloud database.</summary>
        [HttpGet("GetCloudInventory")]
        public List<Inventory> GetAllCloudInventory()
        {
            var inventoryRecords = _CloudDb.Inventory.ToList();
            // Process each `inventory` returned by the collection expression in the loop header.
            foreach (var inventory in inventoryRecords)
            {
                InventoryRules.Recalculate(inventory);
            }
            return inventoryRecords;
        }

        /// <summary>Adds the cloud inventory.</summary>
        [HttpPost("AddCloudInventory")]
        public ActionResult<Inventory> AddCloudInventory([FromBody] Inventory inventory)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            inventory.Beginning_Balance = 0;
            inventory.Buffer = 0;
            inventory.Purchased = 0;
            inventory.Request = 0;
            inventory.Returned = 0;
            inventory.Balance = 0;
            inventory.Request_Balance = inventory.Request_Purchased = inventory.Request_Buffer = 0;
            InventoryRules.Recalculate(inventory);
            _CloudDb.Inventory.Add(inventory);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(inventory);
        }

        /// <summary>Updates the cloud inventory.</summary>
        [HttpPost("UpdateCloudInventory")]
        public ActionResult<Inventory> UpdateCloudInventory([FromBody] Inventory inventory)
        {
            // Return HTTP 400 Bad Request when `inventory == null` is true.
            if (inventory == null)
            {
                return BadRequest(inventory);
            }

            var _inventory = _CloudDb.Inventory.FirstOrDefault(x => x.Material_ID == inventory.Material_ID);
            // Return HTTP 404 Not Found when `_inventory == null` is true.
            if (_inventory == null)
            {
                return NotFound();
            }

            InventoryRules.Recalculate(_inventory);
            // Return HTTP 400 Bad Request when `(inventory.Buffer.HasValue && inventory.Buffer.Value != _inventory.Buffer) || (inventory.Balance.HasValue && inventory.Balance.Value != _inventory.Balance) || (inventory.Status != null && !string.Equals(inventory.Status, _inventory.Status, StringComparison.Ordinal))` is true.
            if ((inventory.Buffer.HasValue && inventory.Buffer.Value != _inventory.Buffer) ||
                (inventory.Balance.HasValue && inventory.Balance.Value != _inventory.Balance) ||
                (inventory.Status != null &&
                    !string.Equals(inventory.Status, _inventory.Status, StringComparison.Ordinal)))
            {
                return BadRequest("Buffer, balance, and status are managed by the inventory system and cannot be edited.");
            }

            _inventory.Inventory_ID = inventory.Inventory_ID;
            _inventory.Beginning_Balance = inventory.Beginning_Balance;
            _inventory.Purchased = inventory.Purchased;
            _inventory.Request = inventory.Request;
            _inventory.Returned = inventory.Returned;
            _inventory.Material_ID = inventory.Material_ID;
            InventoryRules.Recalculate(_inventory);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();

            return Ok(_inventory);
        }

        /// <summary>Deletes the cloud inventory.</summary>
        [HttpPut("DeleteCloudInventory")]
        public ActionResult<Inventory> DeleteCloudInventory(Int32 Id)
        {

            var _inventory = _CloudDb.Inventory.FirstOrDefault(x => x.Inventory_ID == Id);
            // Return HTTP 404 Not Found when `_inventory == null` is true.
            if (_inventory == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(_inventory);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();

            return NoContent();
        }

        // --------------------------- INVENTORY ---------------------------

        // --------------------------- PURCHASE MATERIAL ---------------------------

        [HttpGet("GetLocalPurchaseMaterial")]
        // Retrieves all local purchase material. Returns List<Purchased_Material>.
        public List<Purchased_Material> GetAllLocalPurchaseMaterial()
        {
            var purchases = _Localdb.Purchased_Material.ToList();
            var linkedPurchaseIds = _Localdb.MaterialRequisition_Slip
                .IgnoreQueryFilters()
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(slip => slip.Purchased_ID.HasValue)
                .Select(slip => slip.Purchased_ID.GetValueOrDefault())
                .ToList()
                .ToHashSet();
            // Process each `purchase` returned by the collection expression in the loop header.
            foreach (var purchase in purchases)
                purchase.IsLockedByMaterialRequisition = linkedPurchaseIds.Contains(purchase.Purchased_ID);
            return purchases;
        }

        /// <summary>Adds the local purchase material.</summary>
        [HttpPost("AddLocalPurchaseMaterial")]
        public ActionResult<Purchased_Material> AddLocalPurchaseMaterial([FromBody] Purchased_Material p)
        {
            // Return HTTP 400 Bad Request when `p == null` is true.
            if (p == null) return BadRequest("Purchase details are required.");
            var amountError = CalculatePurchaseAmount(p, out var amount);
            // Return HTTP 400 Bad Request when `amountError != null` is true.
            if (amountError != null) return BadRequest(amountError);

            using var tx = _Localdb.Database.BeginTransaction(IsolationLevel.Serializable);

            // Return HTTP 409 Conflict when `_Localdb.Purchased_Material.Any(existing => existing.SI_No == p.SI_No)` is true.
            if (_Localdb.Purchased_Material.Any(existing => existing.SI_No == p.SI_No))
                return Conflict("This SI No. is already used by another purchase record.");

            var inv = _Localdb.Inventory.FirstOrDefault(i => i.Material_ID == p.Material_ID);
            // Return HTTP 400 Bad Request when `inv == null` is true.
            if (inv == null) return BadRequest("This material has no inventory record.");

            InventoryRules.Recalculate(inv);
            p.Purchased_ID = 0;
            p.Amount = amount;
            _Localdb.Purchased_Material.Add(p);

            var bufferAdded = InventoryRules.BufferAllocation(inv.Buffer ?? 0, p.Quantity);
            inv.Buffer = (inv.Buffer ?? 0) + bufferAdded;
            inv.Purchased = (inv.Purchased ?? 0) + p.Quantity - bufferAdded;
            InventoryRules.Recalculate(inv);

            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            tx.Commit();
            return Ok(p);
        }

        /// <summary>Validates a requisition and returns a preview of its inventory effects without submitting it.</summary>
        [HttpPost("PreviewRequisition")]
        public ActionResult PreviewRequisition([FromBody] RequisitionPreviewDto dto)
        {
            var result = MaterialRequisitionWorkflowService.Preview(_Localdb, dto);
            return result.Error != null ? BadRequest(result.Error) : Ok(result.Response);
        }

        /// <summary>Adds the material requisition batch.</summary>
        [HttpPost("AddLocalMaterialRequisitionSlipBatch")]
        public async Task<ActionResult> AddMaterialRequisitionBatch(
            [FromBody] List<MaterialRequesition_Slip> slips,
            CancellationToken cancellationToken)
        {
            // Return HTTP 401 Unauthorized when `!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var submitterUserId)` is true.
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var submitterUserId))
            {
                return Unauthorized("Your session is invalid. Please sign in again.");
            }
            var result = await MaterialRequisitionWorkflowService.SubmitBatchAsync(
                _Localdb,
                _notificationService,
                slips,
                submitterUserId,
                null,
                cancellationToken);
            return result.Error != null
                ? BadRequest(result.Error)
                : Ok(new { saved = result.Saved });
        }

        /// <summary>Approves the specified material requisitions and applies their inventory effects.</summary>
        [HttpPost("ApproveRequisitions")]
        public async Task<ActionResult> ApproveRequisitions(
            [FromBody] List<int> ids,
            CancellationToken cancellationToken)
        {
            // Return HTTP 403 Forbidden when `!IsWarehouseDepartmentHead()` is true.
            if (!IsWarehouseDepartmentHead()) return Forbid();
            // Return HTTP 400 Bad Request when `ids == null || ids.Count == 0` is true.
            if (ids == null || ids.Count == 0) return BadRequest("No requisitions selected.");
            // Return HTTP 401 Unauthorized when `!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var approverUserId)` is true.
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var approverUserId))
            {
                return Unauthorized("Your session is invalid. Please sign in again.");
            }
            var approverSignature = await _Localdb.UserSignatures
                .AsNoTracking()
                .SingleOrDefaultAsync(signature => signature.User_ID == approverUserId, cancellationToken);
            // Return HTTP 400 Bad Request when `approverSignature == null || approverSignature.SignatureData.Length == 0` is true.
            if (approverSignature == null || approverSignature.SignatureData.Length == 0)
            {
                return BadRequest("Add a signature in Profile Settings before approving a Material Requisition.");
            }
            var approvedBySignature =
                $"data:image/png;base64,{Convert.ToBase64String(approverSignature.SignatureData)}";
            ids = ids.Distinct().ToList();

            using var tx = _Localdb.Database.BeginTransaction(IsolationLevel.Serializable);

            var slips = _Localdb.MaterialRequisition_Slip
                .IgnoreQueryFilters()
                // Apply this predicate in the database query before materializing results.
                .Where(s => ids.Contains(s.MR_ID))
                .ToList();
            // Return HTTP 404 Not Found when `slips.Count != ids.Count` is true.
            if (slips.Count != ids.Count) return NotFound("A selected requisition no longer exists.");
            var initializedRequests = new HashSet<int>();

            // Process each `slip` returned by the collection expression in the loop header.
            foreach (var slip in slips)
            {
                // Return HTTP 409 Conflict when `!IsPending(slip.Status)` is true.
                if (!IsPending(slip.Status))
                    return Conflict($"Requisition {slip.MR_ID} is already {slip.Status}.");

                var isAdminSupplierReturn = InventoryRules.IsAdminSupplierReturn(slip.FromWho, slip.ToWho);
                var directionError = isAdminSupplierReturn
                    ? null
                    : InventoryRules.CheckDirection(slip.FromWho, slip.ToWho);
                // Return HTTP 409 Conflict when `directionError != null` is true.
                if (directionError != null)
                    return Conflict($"Requisition {slip.MR_ID}: {directionError}");

                var inv = _Localdb.Inventory.FirstOrDefault(i => i.Material_ID == slip.Material_ID);
                // Return HTTP 400 Bad Request when `inv == null` is true.
                if (inv == null) return BadRequest($"\"{slip.Material_Name}\" has no inventory record.");

                var qty = slip.Quantity ?? 0;
                // Return HTTP 400 Bad Request when `qty <= 0` is true.
                if (qty <= 0) return BadRequest($"Requisition {slip.MR_ID} has an invalid quantity.");

                // Enter this branch when `initializedRequests.Add(slip.Material_ID)` is true.
                if (initializedRequests.Add(slip.Material_ID))
                {
                    inv.Request = GetPendingDemand(slip.Material_ID);
                }
                // Return HTTP 409 Conflict when `(inv.Request ?? 0) < qty` is true.
                if ((inv.Request ?? 0) < qty)
                    return Conflict($"Requisition {slip.MR_ID} is not included in the pending request total.");
                inv.Request -= qty;

                // Enter this branch when `isAdminSupplierReturn` is true.
                if (isAdminSupplierReturn)
                {
                    var plan = InventoryRules.PlanPurchasedIssue(inv, qty);
                    // Return HTTP 409 Conflict when `plan.Error != null` is true.
                    if (plan.Error != null) return Conflict($"\"{slip.Material_Name}\": {plan.Error}");

                    InventoryRules.Apply(inv, plan);
                }
                // Enter this branch when `InventoryRules.IsWarehouse(slip.FromWho)` is true.
                else if (InventoryRules.IsWarehouse(slip.FromWho))           // material leaves the warehouse
                {
                    var plan = InventoryRules.PlanIssue(inv, qty);
                    // Return HTTP 409 Conflict when `plan.Error != null` is true.
                    if (plan.Error != null) return Conflict($"\"{slip.Material_Name}\": {plan.Error}");

                    InventoryRules.Apply(inv, plan);
                }
                // Enter this branch when `InventoryRules.IsWarehouse(slip.ToWho)` is true.
                else if (InventoryRules.IsWarehouse(slip.ToWho))        // material comes into the warehouse
                {
                    inv.Returned = (inv.Returned ?? 0) + qty;
                    InventoryRules.Recalculate(inv);
                }
                // Use this fallback path when the preceding condition is false.
                else return BadRequest($"Requisition {slip.MR_ID} does not involve the Warehouse.");

                slip.Status = "Approved";
                slip.Approved_By = approvedBySignature;
            }

            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            await _notificationService.NotifyMaterialRequisitionDecisionAsync(
                slips,
                "Approved",
                cancellationToken);
            tx.Commit();
            return Ok(new { approved = slips.Count });
        }

        /// <summary>Rejects the specified material requisitions without applying their inventory issue.</summary>
        [HttpPost("RejectRequisitions")]
        public async Task<ActionResult> RejectRequisitions(
            [FromBody] List<int> ids,
            CancellationToken cancellationToken)
        {
            // Return HTTP 403 Forbidden when `!IsWarehouseDepartmentHead()` is true.
            if (!IsWarehouseDepartmentHead()) return Forbid();
            // Return HTTP 400 Bad Request when `ids == null || ids.Count == 0` is true.
            if (ids == null || ids.Count == 0) return BadRequest("No requisitions selected.");
            using var transaction = _Localdb.Database.BeginTransaction(IsolationLevel.Serializable);
            var slips = _Localdb.MaterialRequisition_Slip
                .IgnoreQueryFilters()
                // Apply this predicate in the database query before materializing results.
                .Where(s => ids.Contains(s.MR_ID))
                .ToList()
                // Apply this predicate in the database query before materializing results.
                .Where(s => IsPending(s.Status))
                .ToList();
            // Process each `s` returned by the collection expression in the loop header.
            foreach (var s in slips) s.Status = "Rejected";

            // Process each `group` returned by the collection expression in the loop header.
            foreach (var group in slips.GroupBy(s => s.Material_ID))
            {
                var inventory = _Localdb.Inventory.FirstOrDefault(i => i.Material_ID == group.Key);
                // Enter this branch when `inventory != null` is true.
                if (inventory != null)
                {
                    inventory.Request = Math.Max(
                        GetPendingDemand(group.Key) - group.Sum(s => s.Quantity ?? 0),
                        0);
                }
            }

            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            await _notificationService.NotifyMaterialRequisitionDecisionAsync(
                slips,
                "Rejected",
                cancellationToken);
            transaction.Commit();
            return Ok(new { rejected = slips.Count });
        }

        /// <summary>Updates the local purchase material.</summary>
        [HttpPost("UpdateLocalPurchaseMaterial")]
        public ActionResult<Purchased_Material> UpdateLocalPurchaseMaterial([FromBody] Purchased_Material purchased_material)
        {
            // Return HTTP 400 Bad Request when `purchased_material == null` is true.
            if (purchased_material == null)
            {
                return BadRequest(purchased_material);
            }
            var amountError = CalculatePurchaseAmount(purchased_material, out var amount);
            // Return HTTP 400 Bad Request when `amountError != null` is true.
            if (amountError != null) return BadRequest(amountError);

            using var tx = _Localdb.Database.BeginTransaction(IsolationLevel.Serializable);

            // Return HTTP 409 Conflict when `_Localdb.Purchased_Material.Any(existing => existing.SI_No == purchased_material.SI_No && existing.Purchased_ID != purchased_material.Purchased_ID)` is true.
            if (_Localdb.Purchased_Material.Any(existing =>
                    existing.SI_No == purchased_material.SI_No &&
                    existing.Purchased_ID != purchased_material.Purchased_ID))
            {
                return Conflict("This SI No. is already used by another purchase record.");
            }

            var _purchased_material = _Localdb.Purchased_Material.FirstOrDefault(x => x.Purchased_ID == purchased_material.Purchased_ID);
            // Return HTTP 404 Not Found when `_purchased_material == null` is true.
            if (_purchased_material == null)
            {
                return NotFound();
            }
            // Return HTTP 409 Conflict when `_Localdb.MaterialRequisition_Slip.IgnoreQueryFilters() .Any(slip => slip.Purchased_ID == purchased_material.Purchased_ID)` is true.
            if (_Localdb.MaterialRequisition_Slip.IgnoreQueryFilters()
                .Any(slip => slip.Purchased_ID == purchased_material.Purchased_ID))
            {
                return Conflict("This purchase receipt is linked to a saved Material Requisition and cannot be edited.");
            }

            _purchased_material.Purchased_ID = purchased_material.Purchased_ID;
            _purchased_material.Date_Received_Item = purchased_material.Date_Received_Item;
            _purchased_material.SI_Date = purchased_material.SI_Date;
            _purchased_material.SI_No = purchased_material.SI_No;
            _purchased_material.Supplier_Name = purchased_material.Supplier_Name;
            _purchased_material.Remarks = purchased_material.Remarks;
            _purchased_material.Quantity = purchased_material.Quantity;
            _purchased_material.Unit_price = purchased_material.Unit_price;
            _purchased_material.Amount = amount;
            _purchased_material.Material_ID = purchased_material.Material_ID;
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            tx.Commit();

            return Ok(_purchased_material);
        }

        /// <summary>Deletes the local purchase material.</summary>
        [HttpPut("DeleteLocalPurchaseMaterial")]
        public ActionResult<Purchased_Material> DeleteLocalPurchaseMaterial(Int32 Id)
        {
            using var tx = _Localdb.Database.BeginTransaction(IsolationLevel.Serializable);
            var purchased_material = _Localdb.Purchased_Material.FirstOrDefault(x => x.Purchased_ID == Id);
            // Return HTTP 404 Not Found when `purchased_material == null` is true.
            if (purchased_material == null)
            {
                return NotFound();
            }
            // Return HTTP 409 Conflict when `_Localdb.MaterialRequisition_Slip.IgnoreQueryFilters() .Any(slip => slip.Purchased_ID == Id)` is true.
            if (_Localdb.MaterialRequisition_Slip.IgnoreQueryFilters()
                .Any(slip => slip.Purchased_ID == Id))
            {
                return Conflict("This purchase receipt is linked to a saved Material Requisition and cannot be deleted.");
            }
            _Localdb.Remove(purchased_material);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            tx.Commit();

            return NoContent();
        }

        /// <summary>Retrieves all purchased materials from the cloud database.</summary>
        [HttpGet("GetCloudPurchaseMaterial")]
        public List<Purchased_Material> GetAllCloudPurchaseMaterial()
        {
            return _Localdb.Purchased_Material.ToList();
        }

        /// <summary>Adds the cloud purchase material.</summary>
        [HttpPost("AddCloudPurchaseMaterial")]
        public ActionResult<Purchased_Material> AddCloudPurchaseMaterial([FromBody] Purchased_Material purchased_material)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            // Return HTTP 400 Bad Request when `purchased_material == null` is true.
            if (purchased_material == null) return BadRequest("Purchase details are required.");
            var amountError = CalculatePurchaseAmount(purchased_material, out var amount);
            // Return HTTP 400 Bad Request when `amountError != null` is true.
            if (amountError != null) return BadRequest(amountError);

            using var tx = _Localdb.Database.BeginTransaction(IsolationLevel.Serializable);
            // Return HTTP 409 Conflict when `_Localdb.Purchased_Material.Any(existing => existing.SI_No == purchased_material.SI_No)` is true.
            if (_Localdb.Purchased_Material.Any(existing => existing.SI_No == purchased_material.SI_No))
                return Conflict("This SI No. is already used by another purchase record.");

            purchased_material.Amount = amount;
            _Localdb.Purchased_Material.Add(purchased_material);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            tx.Commit();
            return Ok(purchased_material);
        }

        /// <summary>Updates the cloud purchase material.</summary>
        [HttpPost("UpdateCloudPurchaseMaterial")]
        public ActionResult<Purchased_Material> UpdateCloudPurchaseMaterial([FromBody] Purchased_Material purchased_material)
        {
            // Return HTTP 400 Bad Request when `purchased_material == null` is true.
            if (purchased_material == null)
            {
                return BadRequest(purchased_material);
            }
            var amountError = CalculatePurchaseAmount(purchased_material, out var amount);
            // Return HTTP 400 Bad Request when `amountError != null` is true.
            if (amountError != null) return BadRequest(amountError);

            using var tx = _Localdb.Database.BeginTransaction(IsolationLevel.Serializable);
            // Return HTTP 409 Conflict when `_Localdb.Purchased_Material.Any(existing => existing.SI_No == purchased_material.SI_No && existing.Purchased_ID != purchased_material.Purchased_ID)` is true.
            if (_Localdb.Purchased_Material.Any(existing =>
                    existing.SI_No == purchased_material.SI_No &&
                    existing.Purchased_ID != purchased_material.Purchased_ID))
            {
                return Conflict("This SI No. is already used by another purchase record.");
            }

            var _purchased_material = _Localdb.Purchased_Material.FirstOrDefault(x => x.Purchased_ID == purchased_material.Purchased_ID);
            // Return HTTP 404 Not Found when `_purchased_material == null` is true.
            if (_purchased_material == null)
            {
                return NotFound();
            }

            _purchased_material.Purchased_ID = purchased_material.Purchased_ID;
            _purchased_material.Date_Received_Item = purchased_material.Date_Received_Item;
            _purchased_material.SI_Date = purchased_material.SI_Date;
            _purchased_material.SI_No = purchased_material.SI_No;
            _purchased_material.Supplier_Name = purchased_material.Supplier_Name;
            _purchased_material.Remarks = purchased_material.Remarks;
            _purchased_material.Quantity = purchased_material.Quantity;
            _purchased_material.Unit_price = purchased_material.Unit_price;
            _purchased_material.Amount = amount;
            _purchased_material.Material_ID = purchased_material.Material_ID;
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            tx.Commit();

            return Ok(_purchased_material);
        }

        /// <summary>Deletes the cloud purchase material.</summary>
        [HttpPut("DeleteCloudPurchaseMaterial")]
        public ActionResult<Purchased_Material> DeleteCloudPurchaseMaterial(Int32 Id)
        {

            var purchased_material = _Localdb.Purchased_Material.FirstOrDefault(x => x.Purchased_ID == Id);
            // Return HTTP 404 Not Found when `purchased_material == null` is true.
            if (purchased_material == null)
            {
                return NotFound();
            }
            _Localdb.Remove(purchased_material);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();

            return NoContent();
        }

        // --------------------------- PURCHASE MATERIAL ---------------------------

        // --------------------------- MATERIALS REQUESITION SLIP ---------------------------

        [HttpGet("GetLocalMaterialRequesitionSlips")]
        // Retrieves all local material requesition slips. Returns List<MaterialRequesition_Slip>.
        public List<MaterialRequesition_Slip> GetAllLocalMaterialRequesitionSlips()
        {
            var query = _Localdb.MaterialRequisition_Slip.AsNoTracking();
            // Enter this branch when `IsWarehouseDepartmentHead()` is true.
            if (IsWarehouseDepartmentHead())
            {
                query = query.IgnoreQueryFilters();
            }
            return query.ToList();
        }

        /// <summary>Retrieves the local material requesition slip by id.</summary>
        [HttpGet("GetLocalMaterialRequesitionSlipsByID")]
        public ActionResult<MaterialRequesition_Slip> GetLocalMaterialRequesition_SlipByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var materialRequesitionSlip = _Localdb.MaterialRequisition_Slip.FirstOrDefault(x => x.MR_ID == id);
            // Return HTTP 404 Not Found when `materialRequesitionSlip == null` is true.
            if (materialRequesitionSlip == null)
            {
                return NotFound();
            }
            return Ok(materialRequesitionSlip);
        }

        /// <summary>Adds the local material requesition slips.</summary>
        [HttpPost("AddLocalMaterialRequesitionSlips")]
        public async Task<ActionResult<MaterialRequesition_Slip>> AddLocalMaterialRequesitionSlips(
            MaterialRequesition_Slip materialRequesitionSlip,
            CancellationToken cancellationToken)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Return HTTP 400 Bad Request when `(materialRequesitionSlip.Quantity ?? 0) <= 0` is true.
            if ((materialRequesitionSlip.Quantity ?? 0) <= 0)
                return BadRequest("Quantity must be greater than zero.");
            var quantity = materialRequesitionSlip.Quantity.GetValueOrDefault();

            var directionError = InventoryRules.CheckDirection(
                materialRequesitionSlip.FromWho,
                materialRequesitionSlip.ToWho);
            // Return HTTP 400 Bad Request when `directionError != null` is true.
            if (directionError != null) return BadRequest(directionError);

            var inventory = _Localdb.Inventory
                .FirstOrDefault(x => x.Material_ID == materialRequesitionSlip.Material_ID);
            // Return HTTP 400 Bad Request when `inventory == null` is true.
            if (inventory == null) return BadRequest("This material has no inventory record.");

            // Enter this branch when `InventoryRules.IsWarehouse(materialRequesitionSlip.FromWho)` is true.
            if (InventoryRules.IsWarehouse(materialRequesitionSlip.FromWho))
            {
                var plan = InventoryRules.PlanIssue(inventory, quantity);
                // Return HTTP 400 Bad Request when `plan.Error != null` is true.
                if (plan.Error != null) return BadRequest(plan.Error);
            }

            await using var transaction = await _Localdb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            inventory.Request = GetPendingDemand(inventory.Material_ID)
                + quantity;
            materialRequesitionSlip.Status = "Pending";
            materialRequesitionSlip.Approved_By = null;
            materialRequesitionSlip.Total_Cost =
                materialRequesitionSlip.Quantity * materialRequesitionSlip.Unit_Cost;
            _Localdb.Add(materialRequesitionSlip);
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync(cancellationToken);
            await _notificationService.NotifyMaterialRequisitionSubmittedAsync(
                new[] { materialRequesitionSlip },
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(materialRequesitionSlip);
        }

        /// <summary>Updates the local material requesition slip.</summary>
        [HttpPost("UpdateLocalMaterialRequesition_Slip")]
        public ActionResult<MaterialRequesition_Slip> UpdateLocalMaterialRequesition_Slip(Int32 Id, [FromBody] MaterialRequesition_Slip materialRequesitionSlip)
        {
            // Return HTTP 400 Bad Request when `materialRequesitionSlip == null` is true.
            if (materialRequesitionSlip == null)
            {
                return BadRequest(materialRequesitionSlip);
            }
            var updateMaterialRequesitionSlip = _Localdb.MaterialRequisition_Slip.FirstOrDefault(x => x.MR_ID == Id);
            // Return HTTP 404 Not Found when `updateMaterialRequesitionSlip == null` is true.
            if (updateMaterialRequesitionSlip == null)
            {
                return NotFound();
            }

            // Return HTTP 409 Conflict when `!IsPending(updateMaterialRequesitionSlip.Status)` is true.
            if (!IsPending(updateMaterialRequesitionSlip.Status))
                return Conflict("Only pending requisitions can be edited.");

            var directionError = InventoryRules.CheckDirection(
                materialRequesitionSlip.FromWho,
                materialRequesitionSlip.ToWho);
            // Return HTTP 400 Bad Request when `directionError != null` is true.
            if (directionError != null) return BadRequest(directionError);

            // Return HTTP 400 Bad Request when `(materialRequesitionSlip.Quantity ?? 0) <= 0` is true.
            if ((materialRequesitionSlip.Quantity ?? 0) <= 0)
                return BadRequest("Quantity must be greater than zero.");
            var quantity = materialRequesitionSlip.Quantity.GetValueOrDefault();

            var updatedInventory = _Localdb.Inventory
                .FirstOrDefault(x => x.Material_ID == materialRequesitionSlip.Material_ID);
            // Return HTTP 400 Bad Request when `updatedInventory == null` is true.
            if (updatedInventory == null) return BadRequest("This material has no inventory record.");

            // Enter this branch when `InventoryRules.IsWarehouse(materialRequesitionSlip.FromWho)` is true.
            if (InventoryRules.IsWarehouse(materialRequesitionSlip.FromWho))
            {
                var plan = InventoryRules.PlanIssue(updatedInventory, quantity);
                // Return HTTP 400 Bad Request when `plan.Error != null` is true.
                if (plan.Error != null) return BadRequest(plan.Error);
            }

            var currentMaterialId = updateMaterialRequesitionSlip.Material_ID;
            var currentQuantity = updateMaterialRequesitionSlip.Quantity ?? 0;
            // Enter this branch when `currentMaterialId == materialRequesitionSlip.Material_ID` is true.
            if (currentMaterialId == materialRequesitionSlip.Material_ID)
            {
                updatedInventory.Request = Math.Max(
                    GetPendingDemand(currentMaterialId) - currentQuantity
                        + quantity,
                    0);
            }
            // Use this fallback path when the preceding condition is false.
            else
            {
                var currentInventory = _Localdb.Inventory
                    .FirstOrDefault(x => x.Material_ID == currentMaterialId);
                // Return HTTP 400 Bad Request when `currentInventory == null` is true.
                if (currentInventory == null)
                    return BadRequest("The current material has no inventory record.");

                currentInventory.Request = Math.Max(
                    GetPendingDemand(currentMaterialId) - currentQuantity,
                    0);
                updatedInventory.Request = GetPendingDemand(materialRequesitionSlip.Material_ID)
                    + quantity;
            }

            updateMaterialRequesitionSlip.Control_Number = materialRequesitionSlip.Control_Number;
            updateMaterialRequesitionSlip.Material_ID = materialRequesitionSlip.Material_ID;
            updateMaterialRequesitionSlip.Date = materialRequesitionSlip.Date;
            updateMaterialRequesitionSlip.FromWho = materialRequesitionSlip.FromWho;
            updateMaterialRequesitionSlip.ToWho = materialRequesitionSlip.ToWho;
            updateMaterialRequesitionSlip.Material_Name = materialRequesitionSlip.Material_Name;
            updateMaterialRequesitionSlip.Project_Name = materialRequesitionSlip.Project_Name;
            updateMaterialRequesitionSlip.Supplier = materialRequesitionSlip.Supplier;
            updateMaterialRequesitionSlip.Quantity = materialRequesitionSlip.Quantity;
            updateMaterialRequesitionSlip.Unit_Cost = materialRequesitionSlip.Unit_Cost;
            updateMaterialRequesitionSlip.Total_Cost = materialRequesitionSlip.Total_Cost;
            updateMaterialRequesitionSlip.Noted_By = materialRequesitionSlip.Noted_By;
            updateMaterialRequesitionSlip.Submmited_By = materialRequesitionSlip.Submmited_By;
            updateMaterialRequesitionSlip.Received_By = materialRequesitionSlip.Received_By;
            updateMaterialRequesitionSlip.Status = "Pending";

            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(updateMaterialRequesitionSlip);
        }

        /// <summary>Deletes the local material requesition slip.</summary>
        [HttpPut("DeleteLocalMaterialRequesitionSlip")]
        public ActionResult<MaterialRequesition_Slip> DeleteLocalMaterialRequesitionSlip(int id)
        {
            var materialRequesitionSlip = _Localdb.MaterialRequisition_Slip.FirstOrDefault(x => x.MR_ID == id);
            // Return HTTP 404 Not Found when `materialRequesitionSlip == null` is true.
            if (materialRequesitionSlip == null)
            {
                return NotFound();
            }

            // Enter this branch when `string.Equals(materialRequesitionSlip.Status, , StringComparison.OrdinalIgnoreCase)` is true.
            if (string.Equals(materialRequesitionSlip.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                var error = ReverseApprovedRequisitionEffect(materialRequesitionSlip);
                // Return HTTP 409 Conflict when `error != null` is true.
                if (error != null) return Conflict(error);
            }
            // Enter this branch when `IsPending(materialRequesitionSlip.Status)` is true.
            else if (IsPending(materialRequesitionSlip.Status))
            {
                var inventory = _Localdb.Inventory
                    .FirstOrDefault(x => x.Material_ID == materialRequesitionSlip.Material_ID);
                // Enter this branch when `inventory != null` is true.
                if (inventory != null)
                {
                    inventory.Request = Math.Max(
                        GetPendingDemand(materialRequesitionSlip.Material_ID)
                            - (materialRequesitionSlip.Quantity ?? 0),
                        0);
                }
            }

            _Localdb.Remove(materialRequesitionSlip);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }

        /// <summary>Retrieves all material requisition slips from the cloud database.</summary>
        [HttpGet("GetCloudMaterialRequesitionSlips")]
        public List<MaterialRequesition_Slip> GetAllCloudMaterialRequesitionSlips()
        {
            return _CloudDb.MaterailsRequisition_Slip.ToList();
        }

        /// <summary>Retrieves the cloud material requesition slip by id.</summary>
        [HttpGet("GetCloudMaterialRequesitionSlipsByID")]
        public ActionResult<MaterialRequesition_Slip> GetCloudMaterialRequesition_SlipByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var materialRequesitionSlip = _CloudDb.MaterailsRequisition_Slip.FirstOrDefault(x => x.MR_ID == id);
            // Return HTTP 404 Not Found when `materialRequesitionSlip == null` is true.
            if (materialRequesitionSlip == null)
            {
                return NotFound();
            }
            return Ok(materialRequesitionSlip);
        }

        /// <summary>Adds the cloud material requesition slips.</summary>
        [HttpPost("AddCloudMaterialRequesitionSlips")]
        public ActionResult<MaterialRequesition_Slip> AddCloudMaterialRequesitionSlips(MaterialRequesition_Slip materialRequesitionSlip)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var departmentError = InventoryRules.CheckSameDepartment(
                materialRequesitionSlip.FromWho,
                materialRequesitionSlip.ToWho);
            // Return HTTP 400 Bad Request when `departmentError != null` is true.
            if (departmentError != null) return BadRequest(departmentError);
            materialRequesitionSlip.Approved_By = null;
            _CloudDb.Add(materialRequesitionSlip);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(materialRequesitionSlip);
        }

        /// <summary>Updates the cloud material requesition slip.</summary>
        [HttpPost("UpdateCloudMaterialRequesition_Slip")]
        public ActionResult<MaterialRequesition_Slip> UpdateCloudMaterialRequesition_Slip(Int32 Id, [FromBody] MaterialRequesition_Slip materialRequesitionSlip)
        {
            // Return HTTP 400 Bad Request when `materialRequesitionSlip == null` is true.
            if (materialRequesitionSlip == null)
            {
                return BadRequest(materialRequesitionSlip);
            }
            var departmentError = InventoryRules.CheckSameDepartment(
                materialRequesitionSlip.FromWho,
                materialRequesitionSlip.ToWho);
            // Return HTTP 400 Bad Request when `departmentError != null` is true.
            if (departmentError != null) return BadRequest(departmentError);
            var updateMaterialRequesitionSlip = _CloudDb.MaterailsRequisition_Slip.FirstOrDefault(x => x.MR_ID == Id);
            // Return HTTP 404 Not Found when `updateMaterialRequesitionSlip == null` is true.
            if (updateMaterialRequesitionSlip == null)
            {
                return NotFound();
            }
            updateMaterialRequesitionSlip.Control_Number = materialRequesitionSlip.Control_Number;
            updateMaterialRequesitionSlip.Date = materialRequesitionSlip.Date;
            updateMaterialRequesitionSlip.FromWho = materialRequesitionSlip.FromWho;
            updateMaterialRequesitionSlip.ToWho = materialRequesitionSlip.ToWho;
            updateMaterialRequesitionSlip.Material_Name = materialRequesitionSlip.Material_Name;
            updateMaterialRequesitionSlip.Project_Name = materialRequesitionSlip.Project_Name;
            updateMaterialRequesitionSlip.Supplier = materialRequesitionSlip.Supplier;
            updateMaterialRequesitionSlip.Quantity = materialRequesitionSlip.Quantity;
            updateMaterialRequesitionSlip.Unit_Cost = materialRequesitionSlip.Unit_Cost;
            updateMaterialRequesitionSlip.Total_Cost = materialRequesitionSlip.Total_Cost;
            updateMaterialRequesitionSlip.Noted_By = materialRequesitionSlip.Noted_By;
            updateMaterialRequesitionSlip.Submmited_By = materialRequesitionSlip.Submmited_By;
            updateMaterialRequesitionSlip.Received_By = materialRequesitionSlip.Received_By;

            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(updateMaterialRequesitionSlip);
        }

        /// <summary>Deletes the cloud material requesition slip.</summary>
        [HttpPut("DeleteCloudMaterialRequesitionSlip")]
        public ActionResult<MaterialRequesition_Slip> DeleteCloudMaterialRequesitionSlip(int id)
        {
            var materialRequesitionSlip = _CloudDb.MaterailsRequisition_Slip.FirstOrDefault(x => x.MR_ID == id);
            // Return HTTP 404 Not Found when `materialRequesitionSlip == null` is true.
            if (materialRequesitionSlip == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(materialRequesitionSlip);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // --------------------------- MATERIALS REQUESITION SLIP ---------------------------

        // --------------------------- STOCK TRANSFER SLIP ---------------------------

        [HttpGet("GetLocalStockTransferSlips")]
        // Retrieves all local stock transfer slips. Returns List<StockTransfer_Slip>.
        public List<StockTransfer_Slip> GetAllLocalStockTransferSlips()
        {
            return _Localdb.StockTransfer_Slip.ToList();
        }

        /// <summary>Retrieves the local stock transfer slip by id.</summary>
        [HttpGet("GetLocalStockTransferSlipByID")]
        public ActionResult<StockTransfer_Slip> GetLocalStockTransferSlipByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var stockTransferSlip = _Localdb.StockTransfer_Slip.FirstOrDefault(x => x.Control_Number == id);
            // Return HTTP 404 Not Found when `stockTransferSlip == null` is true.
            if (stockTransferSlip == null)
            {
                return NotFound();
            }
            return Ok(stockTransferSlip);
        }

        /// <summary>Adds the local stock transfer slip.</summary>
        [HttpPost("AddLocalStockTransfer_Slip")]
        public ActionResult<StockTransfer_Slip> AddLocalStockTransferSlip(StockTransfer_Slip stockTransferSlip)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(stockTransferSlip);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(stockTransferSlip);
        }

        /// <summary>Updates the local stock transfer slip.</summary>
        [HttpPost("UpdateLocalStockTransfer_Slip")]
        public ActionResult<StockTransfer_Slip> UpdateLocalStockTransferSlip(Int32 Id, [FromBody] StockTransfer_Slip stockTransferSlip)
        {
            // Return HTTP 400 Bad Request when `stockTransferSlip == null` is true.
            if (stockTransferSlip == null)
            {
                return BadRequest(stockTransferSlip);
            }
            var updateStockTransferSlip = _Localdb.StockTransfer_Slip.FirstOrDefault(x => x.Control_Number == Id);
            // Return HTTP 404 Not Found when `updateStockTransferSlip == null` is true.
            if (updateStockTransferSlip == null)
            {
                return NotFound();
            }
            updateStockTransferSlip.Control_Number = stockTransferSlip.Control_Number;
            updateStockTransferSlip.Date = stockTransferSlip.Date;
            updateStockTransferSlip.Fromwho = stockTransferSlip.Fromwho;
            updateStockTransferSlip.Towho = stockTransferSlip.Towho;
            updateStockTransferSlip.Quantity = stockTransferSlip.Quantity;
            updateStockTransferSlip.Material_Name = stockTransferSlip.Material_Name;
            updateStockTransferSlip.Stock_Width = stockTransferSlip.Stock_Width;
            updateStockTransferSlip.Stock_Height = stockTransferSlip.Stock_Height;

            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(updateStockTransferSlip);
        }

        /// <summary>Deletes the local stock transfer slip.</summary>
        [HttpPut("DeleteLocalStockTransfer_Slip")]
        public ActionResult<StockTransfer_Slip> DeleteLocalStockTransferSlip(int id)
        {
            var stockTransferSlip = _Localdb.StockTransfer_Slip.FirstOrDefault(x => x.Control_Number == id);
            // Return HTTP 404 Not Found when `stockTransferSlip == null` is true.
            if (stockTransferSlip == null)
            {
                return NotFound();
            }
            _Localdb.Remove(stockTransferSlip);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }



        /// <summary>Retrieves all stock transfer slips from the cloud database.</summary>
        [HttpGet("GetCloudStockTransferSlips")]
        public List<StockTransfer_Slip> GetAllCloudStockTransferSlips()
        {
            return _CloudDb.StockTransfer_Slip.ToList();
        }

        /// <summary>Retrieves the cloud stock transfer slip by id.</summary>
        [HttpGet("GetCloudStockTransferSlipByID")]
        public ActionResult<StockTransfer_Slip> GetCloudStockTransferSlipByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var stockTransferSlip = _CloudDb.StockTransfer_Slip.FirstOrDefault(x => x.Control_Number == id);
            // Return HTTP 404 Not Found when `stockTransferSlip == null` is true.
            if (stockTransferSlip == null)
            {
                return NotFound();
            }
            return Ok(stockTransferSlip);
        }

        /// <summary>Adds the cloud stock transfer slip.</summary>
        [HttpPost("AddCloudStockTransfer_Slip")]
        public ActionResult<StockTransfer_Slip> AddCloudStockTransferSlip(StockTransfer_Slip stockTransferSlip)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(stockTransferSlip);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(stockTransferSlip);
        }

        /// <summary>Updates the cloud stock transfer slip.</summary>
        [HttpPost("UpdateCloudStockTransfer_Slip")]
        public ActionResult<StockTransfer_Slip> UpdateCloudStockTransferSlip(Int32 Id, [FromBody] StockTransfer_Slip stockTransferSlip)
        {
            // Return HTTP 400 Bad Request when `stockTransferSlip == null` is true.
            if (stockTransferSlip == null)
            {
                return BadRequest(stockTransferSlip);
            }
            var updateStockTransferSlip = _CloudDb.StockTransfer_Slip.FirstOrDefault(x => x.Control_Number == Id);
            // Return HTTP 404 Not Found when `updateStockTransferSlip == null` is true.
            if (updateStockTransferSlip == null)
            {
                return NotFound();
            }
            updateStockTransferSlip.Control_Number = stockTransferSlip.Control_Number;
            updateStockTransferSlip.Date = stockTransferSlip.Date;
            updateStockTransferSlip.Fromwho = stockTransferSlip.Fromwho;
            updateStockTransferSlip.Towho = stockTransferSlip.Towho;
            updateStockTransferSlip.Quantity = stockTransferSlip.Quantity;
            updateStockTransferSlip.Material_Name = stockTransferSlip.Material_Name;
            updateStockTransferSlip.Stock_Width = stockTransferSlip.Stock_Width;
            updateStockTransferSlip.Stock_Height = stockTransferSlip.Stock_Height;

            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(updateStockTransferSlip);
        }

        /// <summary>Deletes the cloud stock transfer slip.</summary>
        [HttpPut("DeleteCloudStockTransfer_Slip")]
        public ActionResult<StockTransfer_Slip> DeleteCloudStockTransferSlip(int id)
        {
            var stockTransferSlip = _CloudDb.StockTransfer_Slip.FirstOrDefault(x => x.Control_Number == id);
            // Return HTTP 404 Not Found when `stockTransferSlip == null` is true.
            if (stockTransferSlip == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(stockTransferSlip);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // --------------------------- STOCK TRANSFER SLIP ---------------------------

        // --------------------------- TRANSMITTAL SLIP ---------------------------

        [HttpGet("GetLocalTransmittalSlips")]
        // Retrieves all transmittal slips. Returns List<Transmittal_Slip>.
        public List<Transmittal_Slip> GetAllTransmittalSlips()
        {
            return _Localdb.Transmittal_Slip.ToList();
        }

        /// <summary>Retrieves the local transmittal slip by id.</summary>
        [HttpGet("GetLocalTransmittalSlipsByID")]
        public ActionResult<Transmittal_Slip> GetLocalTransmittalSlipByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var transmittalSlip = _Localdb.Transmittal_Slip.FirstOrDefault(x => x.Control_Number == id);
            // Return HTTP 404 Not Found when `transmittalSlip == null` is true.
            if (transmittalSlip == null)
            {
                return NotFound();
            }
            return Ok(transmittalSlip);
        }

        /// <summary>Adds the local transmittal slip.</summary>
        [HttpPost("AddLocalTransmittal_Slip")]
        public ActionResult<Transmittal_Slip> AddLocalTransmittalSlip(Transmittal_Slip transmittalSlip)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(transmittalSlip);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(transmittalSlip);
        }

        /// <summary>Updates the local transmittal slip.</summary>
        [HttpPost("UpdateLocalTransmittal_Slip")]
        public ActionResult<Transmittal_Slip> UpdateLocalTransmittalSlip(Int32 Id, [FromBody] Transmittal_Slip transmittalSlip)
        {
            // Return HTTP 400 Bad Request when `transmittalSlip == null` is true.
            if (transmittalSlip == null)
            {
                return BadRequest(transmittalSlip);
            }
            var updateTransmittalSlip = _Localdb.Transmittal_Slip.FirstOrDefault(x => x.Control_Number == Id);
            // Return HTTP 404 Not Found when `updateTransmittalSlip == null` is true.
            if (updateTransmittalSlip == null)
            {
                return NotFound();
            }
            updateTransmittalSlip.Control_Number = transmittalSlip.Control_Number;
            updateTransmittalSlip.Date = transmittalSlip.Date;
            updateTransmittalSlip.Fromwho = transmittalSlip.Fromwho;
            updateTransmittalSlip.Towho = transmittalSlip.Towho;
            updateTransmittalSlip.Machine = transmittalSlip.Machine;
            updateTransmittalSlip.Client_Name = transmittalSlip.Client_Name;
            updateTransmittalSlip.Materials_Used = transmittalSlip.Materials_Used;
            updateTransmittalSlip.Trans_Width = transmittalSlip.Trans_Width;
            updateTransmittalSlip.Trans_Height = transmittalSlip.Trans_Height;
            updateTransmittalSlip.Remarks = transmittalSlip.Remarks;
            updateTransmittalSlip.Signed_By = transmittalSlip.Signed_By;
            updateTransmittalSlip.Submmited_By = transmittalSlip.Submmited_By;
            updateTransmittalSlip.Received_By = transmittalSlip.Received_By;

            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(updateTransmittalSlip);
        }

        /// <summary>Deletes the local transmittal slip.</summary>
        [HttpPut("DeleteLocalTransmittal_Slip")]
        public ActionResult<Transmittal_Slip> DeleteLocalTransmittalSlip(int id)
        {
            var transmittalSlip = _Localdb.Transmittal_Slip.FirstOrDefault(x => x.Control_Number == id);
            // Return HTTP 404 Not Found when `transmittalSlip == null` is true.
            if (transmittalSlip == null)
            {
                return NotFound();
            }
            _Localdb.Remove(transmittalSlip);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }



        /// <summary>Retrieves all transmittal slips from the cloud database.</summary>
        [HttpGet("GetCloudTransmittalSlips")]
        public List<Transmittal_Slip> GetAllCloudTransmittalSlips()
        {
            return _CloudDb.Transmittal_Slip.ToList();
        }

        /// <summary>Retrieves the cloud transmittal slip by id.</summary>
        [HttpGet("GetCloudTransmittalSlipsByID")]
        public ActionResult<Transmittal_Slip> GetCloudTransmittalSlipByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var transmittalSlip = _CloudDb.Transmittal_Slip.FirstOrDefault(x => x.Control_Number == id);
            // Return HTTP 404 Not Found when `transmittalSlip == null` is true.
            if (transmittalSlip == null)
            {
                return NotFound();
            }
            return Ok(transmittalSlip);
        }

        /// <summary>Adds the cloud transmittal slip.</summary>
        [HttpPost("AddCloudTransmittal_Slip")]
        public ActionResult<Transmittal_Slip> AddCloudTransmittalSlip(Transmittal_Slip transmittalSlip)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(transmittalSlip);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(transmittalSlip);
        }

        /// <summary>Updates the cloud transmittal slip.</summary>
        [HttpPost("UpdateCloudTransmittal_Slip")]
        public ActionResult<Transmittal_Slip> UpdateCloudTransmittalSlip(Int32 Id, [FromBody] Transmittal_Slip transmittalSlip)
        {
            // Return HTTP 400 Bad Request when `transmittalSlip == null` is true.
            if (transmittalSlip == null)
            {
                return BadRequest(transmittalSlip);
            }
            var updateTransmittalSlip = _CloudDb.Transmittal_Slip.FirstOrDefault(x => x.Control_Number == Id);
            // Return HTTP 404 Not Found when `updateTransmittalSlip == null` is true.
            if (updateTransmittalSlip == null)
            {
                return NotFound();
            }
            updateTransmittalSlip.Control_Number = transmittalSlip.Control_Number;
            updateTransmittalSlip.Date = transmittalSlip.Date;
            updateTransmittalSlip.Fromwho = transmittalSlip.Fromwho;
            updateTransmittalSlip.Towho = transmittalSlip.Towho;
            updateTransmittalSlip.Machine = transmittalSlip.Machine;
            updateTransmittalSlip.Client_Name = transmittalSlip.Client_Name;
            updateTransmittalSlip.Materials_Used = transmittalSlip.Materials_Used;
            updateTransmittalSlip.Trans_Width = transmittalSlip.Trans_Width;
            updateTransmittalSlip.Trans_Height = transmittalSlip.Trans_Height;
            updateTransmittalSlip.Remarks = transmittalSlip.Remarks;
            updateTransmittalSlip.Signed_By = transmittalSlip.Signed_By;
            updateTransmittalSlip.Submmited_By = transmittalSlip.Submmited_By;
            updateTransmittalSlip.Received_By = transmittalSlip.Received_By;

            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(updateTransmittalSlip);
        }

        /// <summary>Deletes the cloud transmittal slip.</summary>
        [HttpPut("DeleteCloudTransmittal_Slip")]
        public ActionResult<Transmittal_Slip> DeleteCloudTransmittalSlip(int id)
        {
            var transmittalSlip = _CloudDb.Transmittal_Slip.FirstOrDefault(x => x.Control_Number == id);
            // Return HTTP 404 Not Found when `transmittalSlip == null` is true.
            if (transmittalSlip == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(transmittalSlip);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // --------------------------- TRANSMITTAL SLIP ---------------------------

    }
}
