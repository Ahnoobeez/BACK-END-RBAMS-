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
    public class InventoryController : ControllerBase
    {
        private LocalDbContext _Localdb;
        private CloudDbContext _CloudDb;
        public InventoryController(LocalDbContext Localcontext, CloudDbContext Cloudcontext)
        {
            _Localdb = Localcontext;
            _CloudDb = Cloudcontext;
        }

        private static string? CalculatePurchaseAmount(Purchased_Material purchase, out int amount)
        {
            amount = 0;
            if (purchase.Quantity <= 0)
                return "Quantity must be greater than zero.";
            if (purchase.Unit_price < 0)
                return "Unit price cannot be negative.";

            var total = (long)purchase.Quantity * purchase.Unit_price;
            if (total > int.MaxValue)
                return "The purchase amount exceeds the supported limit.";

            amount = (int)total;
            return null;
        }

        // --------------------------- INVENTORY HELPERS (LOCAL) ---------------------------

        private static bool IsPending(string? status) =>
            string.IsNullOrWhiteSpace(status) ||
            string.Equals(status.Trim(), "Pending", StringComparison.OrdinalIgnoreCase);

        private int GetPendingDemand(int materialId, int? excludingRequisitionId = null)
        {
            var query = _Localdb.MaterialRequisition_Slip
                .AsNoTracking()
                .Where(s => s.Material_ID == materialId &&
                    (s.Status == null || s.Status.Trim() == "" ||
                     s.Status.Trim().ToUpper() == "PENDING"));

            if (excludingRequisitionId.HasValue)
            {
                query = query.Where(s => s.MR_ID != excludingRequisitionId.Value);
            }

            return query.Sum(s => s.Quantity ?? 0);
        }

        private string? ReverseApprovedRequisitionEffect(MaterialRequesition_Slip slip)
        {
            var inventory = _Localdb.Inventory.FirstOrDefault(x => x.Material_ID == slip.Material_ID);
            if (inventory == null)
                return $"No inventory record found for Material_ID {slip.Material_ID}.";

            InventoryRules.Recalculate(inventory);
            var quantity = slip.Quantity ?? 0;
            if (quantity <= 0)
                return $"Requisition {slip.MR_ID} has an invalid quantity.";

            if (InventoryRules.IsWarehouse(slip.FromWho))
            {
                var remaining = quantity;
                var fromBuffer = Math.Min(remaining, Math.Max(inventory.Request_Buffer ?? 0, 0));
                remaining -= fromBuffer;
                var fromPurchased = Math.Min(remaining, Math.Max(inventory.Request_Purchased ?? 0, 0));
                remaining -= fromPurchased;
                var fromBalance = Math.Min(remaining, Math.Max(inventory.Request_Balance ?? 0, 0));
                remaining -= fromBalance;
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
            else if (InventoryRules.IsWarehouse(slip.ToWho))
            {
                var returned = (inventory.Returned ?? 0) - quantity;
                if (returned < 0)
                    return $"Requisition {slip.MR_ID} cannot be removed because its returned quantity is no longer recorded.";
                inventory.Returned = returned;
            }
            else
            {
                return $"Requisition {slip.MR_ID} does not involve the Warehouse.";
            }

            InventoryRules.Recalculate(inventory);
            return null;
        }


        // --------------------------- MATERIALS ---------------------------
        [HttpGet("GetLocalMaterials")]
        public List<Materials> GetAllLocalMaterials()
        {
            return _Localdb.Materials.ToList();
        }

        [HttpPost("AddLocalMaterials")]
        public ActionResult<Materials> AddLocalMaterial([FromBody] Materials materials)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Materials.Add(materials);
            _Localdb.SaveChanges();
            return Ok(materials);
        }

        [HttpPost("UpdateLocalMaterials")]
        public ActionResult<Materials> UpdateLocalMaterial([FromBody] Materials materials)
        {
            if (materials == null)
            {
                return BadRequest(materials);
            }

            var localMaterial = _Localdb.Materials.FirstOrDefault(x => x.Material_ID == materials.Material_ID);
            if (localMaterial == null)
            {
                return NotFound();
            }

            localMaterial.Material_ID = materials.Material_ID;
            localMaterial.Code_Name = materials.Code_Name;
            localMaterial.Item_Name = materials.Item_Name;
            localMaterial.Category = materials.Category;
            localMaterial.Unit = materials.Unit;
            _Localdb.SaveChanges();

            return Ok(localMaterial);
        }

        [HttpPut("DeleteLocalMaterials")]
        public ActionResult<Materials> DeleteLocalMaterial(Int32 Id)
        {

            var localMaterial = _Localdb.Materials.FirstOrDefault(x => x.Material_ID == Id);
            if (localMaterial == null)
            {
                return NotFound();
            }
            _Localdb.Remove(localMaterial);
            _Localdb.SaveChanges();

            return NoContent();
        }

        [HttpGet("GetCloudMaterials")]
        public List<Materials> GetAllCloudMaterials()
        {
            return _CloudDb.Materials.ToList();
        }

        [HttpPost("AddCloudMaterials")]
        public ActionResult<Materials> AddCloudMaterial([FromBody] Materials materials)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Materials.Add(materials);
            _CloudDb.SaveChanges();
            return Ok(materials);
        }

        [HttpPost("UpdateCloudMaterials")]
        public ActionResult<Materials> UpdateCloudMaterial([FromBody] Materials materials)
        {
            if (materials == null)
            {
                return BadRequest(materials);
            }

            var cloudMaterial = _CloudDb.Materials.FirstOrDefault(x => x.Material_ID == materials.Material_ID);
            if (cloudMaterial == null)
            {
                return NotFound();
            }

            cloudMaterial.Material_ID = materials.Material_ID;
            cloudMaterial.Code_Name = materials.Code_Name;
            cloudMaterial.Item_Name = materials.Item_Name;
            cloudMaterial.Category = materials.Category;
            cloudMaterial.Unit = materials.Unit;
            _CloudDb.SaveChanges();

            return Ok(cloudMaterial);
        }

        [HttpPut("DeleteCloudMaterials")]
        public ActionResult<Materials> DeleteCloudMaterial(Int32 Id)
        {

            var cloudMaterial = _CloudDb.Materials.FirstOrDefault(x => x.Material_ID == Id);
            if (cloudMaterial == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(cloudMaterial);
            _CloudDb.SaveChanges();

            return NoContent();
        }

        // --------------------------- MATERIALS ---------------------------

        // --------------------------- INVENTORY ---------------------------

        [HttpGet("GetLocalInventory")]
        public List<Inventory> GetAllLocalInventory()
        {
            var inventoryRecords = _Localdb.Inventory.AsNoTracking().ToList();
            var pendingRequests = _Localdb.MaterialRequisition_Slip
                .AsNoTracking()
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

            foreach (var inventory in inventoryRecords)
            {
                inventory.Request = pendingRequests.TryGetValue(inventory.Material_ID, out var quantity)
                    ? quantity
                    : 0;
                InventoryRules.Recalculate(inventory);
            }

            return inventoryRecords;
        }

        [HttpPost("AddLocalInventory")]
        public async Task<ActionResult<Inventory>> AddLocalInventory([FromBody] Inventory inventory)
        {
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
            await _Localdb.SaveChangesAsync();

            return Ok(inventory);
        }

        [HttpPost("UpdateLocalInventory")]
        public async Task<ActionResult<Inventory>> UpdateLocalInventory([FromBody] Inventory incoming)
        {
            if (incoming == null)
            {
                return BadRequest("Inventory data is required.");
            }

            var existing = await _Localdb.Inventory
                .FirstOrDefaultAsync(x => x.Material_ID == incoming.Material_ID);
            if (existing == null)
            {
                return NotFound();
            }
            if ((incoming.Buffer ?? 0) < 0 || (incoming.Returned ?? 0) < 0)
            {
                return BadRequest("Buffer and returned stock cannot be negative.");
            }
            existing.Request = GetPendingDemand(existing.Material_ID);
            InventoryRules.Recalculate(existing);

            // Validate the incoming values before applying them
            if (incoming.Buffer < existing.Request_Buffer ||
                incoming.Beginning_Balance < existing.Request_Balance)
            {
                return BadRequest("Beginning balance and buffer cannot be lower than what has already been issued.");
            }

            existing.Beginning_Balance = incoming.Beginning_Balance;
            existing.Buffer = incoming.Buffer;
            existing.Returned = incoming.Returned;
            // never copy Purchased, Request*, Balance

            InventoryRules.Recalculate(existing);
            await _Localdb.SaveChangesAsync();

            return Ok(existing);
        }

        [HttpPut("DeleteLocalInventory")]
        public ActionResult<Inventory> DeleteLocalInventory(Int32 Id)
        {

            var _inventory = _Localdb.Inventory.FirstOrDefault(x => x.Inventory_ID == Id);
            if (_inventory == null)
            {
                return NotFound();
            }
            _Localdb.Remove(_inventory);
            _Localdb.SaveChanges();

            return NoContent();
        }

        [HttpGet("GetCloudInventory")]
        public List<Inventory> GetAllCloudInventory()
        {
            var inventoryRecords = _CloudDb.Inventory.ToList();
            foreach (var inventory in inventoryRecords)
            {
                InventoryRules.Recalculate(inventory);
            }
            return inventoryRecords;
        }

        [HttpPost("AddCloudInventory")]
        public ActionResult<Inventory> AddCloudInventory([FromBody] Inventory inventory)
        {
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
            _CloudDb.SaveChanges();
            return Ok(inventory);
        }

        [HttpPost("UpdateCloudInventory")]
        public ActionResult<Inventory> UpdateCloudInventory([FromBody] Inventory inventory)
        {
            if (inventory == null)
            {
                return BadRequest(inventory);
            }

            var _inventory = _CloudDb.Inventory.FirstOrDefault(x => x.Material_ID == inventory.Material_ID);
            if (_inventory == null)
            {
                return NotFound();
            }

            _inventory.Inventory_ID = inventory.Inventory_ID;
            _inventory.Beginning_Balance = inventory.Beginning_Balance;
            _inventory.Buffer = inventory.Buffer;
            _inventory.Purchased = inventory.Purchased;
            _inventory.Request = inventory.Request;
            _inventory.Returned = inventory.Returned;
            _inventory.Balance = inventory.Balance;
            _inventory.Material_ID = inventory.Material_ID;
            InventoryRules.Recalculate(_inventory);
            _CloudDb.SaveChanges();

            return Ok(_inventory);
        }

        [HttpPut("DeleteCloudInventory")]
        public ActionResult<Inventory> DeleteCloudInventory(Int32 Id)
        {

            var _inventory = _CloudDb.Inventory.FirstOrDefault(x => x.Inventory_ID == Id);
            if (_inventory == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(_inventory);
            _CloudDb.SaveChanges();

            return NoContent();
        }

        // --------------------------- INVENTORY ---------------------------

        // --------------------------- PURCHASE MATERIAL ---------------------------

        [HttpGet("GetLocalPurchaseMaterial")]
        public List<Purchased_Material> GetAllLocalPurchaseMaterial()
        {
            return _Localdb.Purchased_Material.ToList();
        }

        [HttpPost("AddLocalPurchaseMaterial")]
        public ActionResult<Purchased_Material> AddLocalPurchaseMaterial([FromBody] Purchased_Material p)
        {
            if (p == null) return BadRequest("Purchase details are required.");
            var amountError = CalculatePurchaseAmount(p, out var amount);
            if (amountError != null) return BadRequest(amountError);

            using var tx = _Localdb.Database.BeginTransaction(IsolationLevel.Serializable);

            var inv = _Localdb.Inventory.FirstOrDefault(i => i.Material_ID == p.Material_ID);
            if (inv == null) return BadRequest("This material has no inventory record.");

            InventoryRules.Recalculate(inv);
            p.Purchased_ID = 0;
            p.Amount = amount;
            _Localdb.Purchased_Material.Add(p);

            var bufferAdded = InventoryRules.BufferAllocation(inv.Buffer ?? 0, p.Quantity);
            inv.Buffer = (inv.Buffer ?? 0) + bufferAdded;
            inv.Purchased = (inv.Purchased ?? 0) + p.Quantity - bufferAdded;
            InventoryRules.Recalculate(inv);

            _Localdb.SaveChanges();
            tx.Commit();
            return Ok(p);
        }

        [HttpPost("PreviewRequisition")]
        public ActionResult PreviewRequisition([FromBody] RequisitionPreviewDto dto)
        {
            var dirError = InventoryRules.CheckDirection(dto.FromWho, dto.ToWho);
            if (dirError != null) return BadRequest(dirError);

            if (InventoryRules.IsWarehouse(dto.ToWho))
                return Ok(new { usesBuffer = false, fromBuffer = 0, fromPurchased = 0, fromBalance = 0 });

            var inv = _Localdb.Inventory.AsNoTracking().FirstOrDefault(i => i.Material_ID == dto.Material_ID);
            if (inv == null) return BadRequest("This material has no inventory record.");

            var plan = InventoryRules.PlanIssue(inv, dto.Quantity);
            if (plan.Error != null) return BadRequest(plan.Error);

            return Ok(new
            {
                available = InventoryRules.Available(inv)
            });
        }

        [HttpPost("AddLocalMaterialRequisitionSlipBatch")]
        public ActionResult AddMaterialRequisitionBatch([FromBody] List<MaterialRequesition_Slip> slips)
        {
            if (slips == null || slips.Count == 0) return BadRequest("No materials.");

            var submitter = User.FindFirstValue(ClaimTypes.Name);
            var today = DateOnly.FromDateTime(DateTime.Now);
            var initializedRequests = new HashSet<int>();

            for (int i = 0; i < slips.Count; i++)
            {
                var s = slips[i];
                var label = $"Row {i + 1} ({s.Material_Name})";

                if (string.IsNullOrWhiteSpace(s.Control_Number)) return BadRequest($"{label}: control number is required.");
                if ((s.Quantity ?? 0) <= 0) return BadRequest($"{label}: quantity must be greater than zero.");
                var quantity = s.Quantity.GetValueOrDefault();
                if ((s.Unit_Cost ?? 0) < 0) return BadRequest($"{label}: unit cost cannot be negative.");

                var dirError = InventoryRules.CheckDirection(s.FromWho, s.ToWho);
                if (dirError != null) return BadRequest($"{label}: {dirError}");

                var inv = _Localdb.Inventory.FirstOrDefault(x => x.Material_ID == s.Material_ID);
                if (inv == null) return BadRequest($"{label}: this material has no inventory record.");

                if (initializedRequests.Add(s.Material_ID))
                {
                    inv.Request = GetPendingDemand(s.Material_ID);
                }

                if (InventoryRules.IsWarehouse(s.FromWho))
                {
                    var plan = InventoryRules.PlanIssue(inv, quantity);
                    if (plan.Error != null) return BadRequest($"{label}: {plan.Error}");
                }

                inv.Request = (inv.Request ?? 0) + quantity;
                s.MR_ID = 0;
                s.Total_Cost = s.Quantity * s.Unit_Cost;       // computed here
                s.Status = "Pending";
                s.Submmited_By = submitter;                    // from the token
                s.Date = today;                                // server date
                _Localdb.MaterialRequisition_Slip.Add(s);
            }

            _Localdb.SaveChanges();                            // all rows or none
            return Ok(new { saved = slips.Count });
        }

        [HttpPost("ApproveRequisitions")]
        public ActionResult ApproveRequisitions([FromBody] List<int> ids)
        {
            if (ids == null || ids.Count == 0) return BadRequest("No requisitions selected.");
            ids = ids.Distinct().ToList();

            using var tx = _Localdb.Database.BeginTransaction(IsolationLevel.Serializable);

            var slips = _Localdb.MaterialRequisition_Slip.Where(s => ids.Contains(s.MR_ID)).ToList();
            if (slips.Count != ids.Count) return NotFound("A selected requisition no longer exists.");
            var initializedRequests = new HashSet<int>();

            foreach (var slip in slips)
            {
                if (!IsPending(slip.Status))
                    return Conflict($"Requisition {slip.MR_ID} is already {slip.Status}.");

                var directionError = InventoryRules.CheckDirection(slip.FromWho, slip.ToWho);
                if (directionError != null)
                    return Conflict($"Requisition {slip.MR_ID}: {directionError}");

                var inv = _Localdb.Inventory.FirstOrDefault(i => i.Material_ID == slip.Material_ID);
                if (inv == null) return BadRequest($"\"{slip.Material_Name}\" has no inventory record.");

                var qty = slip.Quantity ?? 0;
                if (qty <= 0) return BadRequest($"Requisition {slip.MR_ID} has an invalid quantity.");

                if (initializedRequests.Add(slip.Material_ID))
                {
                    inv.Request = GetPendingDemand(slip.Material_ID);
                }
                if ((inv.Request ?? 0) < qty)
                    return Conflict($"Requisition {slip.MR_ID} is not included in the pending request total.");
                inv.Request -= qty;

                if (InventoryRules.IsWarehouse(slip.FromWho))           // material leaves the warehouse
                {
                    var plan = InventoryRules.PlanIssue(inv, qty);
                    if (plan.Error != null) return Conflict($"\"{slip.Material_Name}\": {plan.Error}");

                    InventoryRules.Apply(inv, plan);
                }
                else if (InventoryRules.IsWarehouse(slip.ToWho))        // material comes into the warehouse
                {
                    inv.Returned = (inv.Returned ?? 0) + qty;
                    InventoryRules.Recalculate(inv);
                }
                else return BadRequest($"Requisition {slip.MR_ID} does not involve the Warehouse.");

                slip.Status = "Approved";
            }

            _Localdb.SaveChanges();
            tx.Commit();
            return Ok(new { approved = slips.Count });
        }

        [HttpPost("RejectRequisitions")]
        public ActionResult RejectRequisitions([FromBody] List<int> ids)
        {
            if (ids == null || ids.Count == 0) return BadRequest("No requisitions selected.");
            var slips = _Localdb.MaterialRequisition_Slip
                .Where(s => ids.Contains(s.MR_ID))
                .ToList()
                .Where(s => IsPending(s.Status))
                .ToList();
            foreach (var s in slips) s.Status = "Rejected";

            foreach (var group in slips.GroupBy(s => s.Material_ID))
            {
                var inventory = _Localdb.Inventory.FirstOrDefault(i => i.Material_ID == group.Key);
                if (inventory != null)
                {
                    inventory.Request = Math.Max(
                        GetPendingDemand(group.Key) - group.Sum(s => s.Quantity ?? 0),
                        0);
                }
            }

            _Localdb.SaveChanges();
            return Ok(new { rejected = slips.Count });
        }

        [HttpPost("UpdateLocalPurchaseMaterial")]
        public ActionResult<Purchased_Material> UpdateLocalPurchaseMaterial([FromBody] Purchased_Material purchased_material)
        {
            if (purchased_material == null)
            {
                return BadRequest(purchased_material);
            }
            var amountError = CalculatePurchaseAmount(purchased_material, out var amount);
            if (amountError != null) return BadRequest(amountError);

            var _purchased_material = _Localdb.Purchased_Material.FirstOrDefault(x => x.Purchased_ID == purchased_material.Purchased_ID);
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
            _Localdb.SaveChanges();

            return Ok(_purchased_material);
        }

        [HttpPut("DeleteLocalPurchaseMaterial")]
        public ActionResult<Purchased_Material> DeleteLocalPurchaseMaterial(Int32 Id)
        {

            var purchased_material = _Localdb.Purchased_Material.FirstOrDefault(x => x.Purchased_ID == Id);
            if (purchased_material == null)
            {
                return NotFound();
            }
            _Localdb.Remove(purchased_material);
            _Localdb.SaveChanges();

            return NoContent();
        }

        [HttpGet("GetCloudPurchaseMaterial")]
        public List<Purchased_Material> GetAllCloudPurchaseMaterial()
        {
            return _Localdb.Purchased_Material.ToList();
        }

        [HttpPost("AddCloudPurchaseMaterial")]
        public ActionResult<Purchased_Material> AddCloudPurchaseMaterial([FromBody] Purchased_Material purchased_material)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (purchased_material == null) return BadRequest("Purchase details are required.");
            var amountError = CalculatePurchaseAmount(purchased_material, out var amount);
            if (amountError != null) return BadRequest(amountError);

            purchased_material.Amount = amount;
            _Localdb.Purchased_Material.Add(purchased_material);
            _Localdb.SaveChanges();
            return Ok(purchased_material);
        }

        [HttpPost("UpdateCloudPurchaseMaterial")]
        public ActionResult<Purchased_Material> UpdateCloudPurchaseMaterial([FromBody] Purchased_Material purchased_material)
        {
            if (purchased_material == null)
            {
                return BadRequest(purchased_material);
            }
            var amountError = CalculatePurchaseAmount(purchased_material, out var amount);
            if (amountError != null) return BadRequest(amountError);

            var _purchased_material = _Localdb.Purchased_Material.FirstOrDefault(x => x.Purchased_ID == purchased_material.Purchased_ID);
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
            _Localdb.SaveChanges();

            return Ok(_purchased_material);
        }

        [HttpPut("DeleteCloudPurchaseMaterial")]
        public ActionResult<Purchased_Material> DeleteCloudPurchaseMaterial(Int32 Id)
        {

            var purchased_material = _Localdb.Purchased_Material.FirstOrDefault(x => x.Purchased_ID == Id);
            if (purchased_material == null)
            {
                return NotFound();
            }
            _Localdb.Remove(purchased_material);
            _Localdb.SaveChanges();

            return NoContent();
        }

        // --------------------------- PURCHASE MATERIAL ---------------------------

        // --------------------------- MATERIALS REQUESITION SLIP ---------------------------

        [HttpGet("GetLocalMaterialRequesitionSlips")]
        public List<MaterialRequesition_Slip> GetAllLocalMaterialRequesitionSlips()
        {
            return _Localdb.MaterialRequisition_Slip.ToList();
        }

        [HttpGet("GetLocalMaterialRequesitionSlipsByID")]
        public ActionResult<MaterialRequesition_Slip> GetLocalMaterialRequesition_SlipByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var materialRequesitionSlip = _Localdb.MaterialRequisition_Slip.FirstOrDefault(x => x.MR_ID == id);
            if (materialRequesitionSlip == null)
            {
                return NotFound();
            }
            return Ok(materialRequesitionSlip);
        }

        [HttpPost("AddLocalMaterialRequesitionSlips")]
        public ActionResult<MaterialRequesition_Slip> AddLocalMaterialRequesitionSlips(MaterialRequesition_Slip materialRequesitionSlip)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if ((materialRequesitionSlip.Quantity ?? 0) <= 0)
                return BadRequest("Quantity must be greater than zero.");
            var quantity = materialRequesitionSlip.Quantity.GetValueOrDefault();

            var directionError = InventoryRules.CheckDirection(
                materialRequesitionSlip.FromWho,
                materialRequesitionSlip.ToWho);
            if (directionError != null) return BadRequest(directionError);

            var inventory = _Localdb.Inventory
                .FirstOrDefault(x => x.Material_ID == materialRequesitionSlip.Material_ID);
            if (inventory == null) return BadRequest("This material has no inventory record.");

            if (InventoryRules.IsWarehouse(materialRequesitionSlip.FromWho))
            {
                var plan = InventoryRules.PlanIssue(inventory, quantity);
                if (plan.Error != null) return BadRequest(plan.Error);
            }

            inventory.Request = GetPendingDemand(inventory.Material_ID)
                + quantity;
            materialRequesitionSlip.Status = "Pending";
            materialRequesitionSlip.Total_Cost =
                materialRequesitionSlip.Quantity * materialRequesitionSlip.Unit_Cost;
            _Localdb.Add(materialRequesitionSlip);
            _Localdb.SaveChanges();
            return Ok(materialRequesitionSlip);
        }

        [HttpPost("UpdateLocalMaterialRequesition_Slip")]
        public ActionResult<MaterialRequesition_Slip> UpdateLocalMaterialRequesition_Slip(Int32 Id, [FromBody] MaterialRequesition_Slip materialRequesitionSlip)
        {
            if (materialRequesitionSlip == null)
            {
                return BadRequest(materialRequesitionSlip);
            }
            var updateMaterialRequesitionSlip = _Localdb.MaterialRequisition_Slip.FirstOrDefault(x => x.MR_ID == Id);
            if (updateMaterialRequesitionSlip == null)
            {
                return NotFound();
            }

            if (!IsPending(updateMaterialRequesitionSlip.Status))
                return Conflict("Only pending requisitions can be edited.");

            var directionError = InventoryRules.CheckDirection(
                materialRequesitionSlip.FromWho,
                materialRequesitionSlip.ToWho);
            if (directionError != null) return BadRequest(directionError);

            if ((materialRequesitionSlip.Quantity ?? 0) <= 0)
                return BadRequest("Quantity must be greater than zero.");
            var quantity = materialRequesitionSlip.Quantity.GetValueOrDefault();

            var updatedInventory = _Localdb.Inventory
                .FirstOrDefault(x => x.Material_ID == materialRequesitionSlip.Material_ID);
            if (updatedInventory == null) return BadRequest("This material has no inventory record.");

            if (InventoryRules.IsWarehouse(materialRequesitionSlip.FromWho))
            {
                var plan = InventoryRules.PlanIssue(updatedInventory, quantity);
                if (plan.Error != null) return BadRequest(plan.Error);
            }

            var currentMaterialId = updateMaterialRequesitionSlip.Material_ID;
            var currentQuantity = updateMaterialRequesitionSlip.Quantity ?? 0;
            if (currentMaterialId == materialRequesitionSlip.Material_ID)
            {
                updatedInventory.Request = Math.Max(
                    GetPendingDemand(currentMaterialId) - currentQuantity
                        + quantity,
                    0);
            }
            else
            {
                var currentInventory = _Localdb.Inventory
                    .FirstOrDefault(x => x.Material_ID == currentMaterialId);
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
            updateMaterialRequesitionSlip.Approved_By = materialRequesitionSlip.Approved_By;
            updateMaterialRequesitionSlip.Submmited_By = materialRequesitionSlip.Submmited_By;
            updateMaterialRequesitionSlip.Received_By = materialRequesitionSlip.Received_By;
            updateMaterialRequesitionSlip.Status = "Pending";

            _Localdb.SaveChanges();
            return Ok(updateMaterialRequesitionSlip);
        }

        [HttpPut("DeleteLocalMaterialRequesitionSlip")]
        public ActionResult<MaterialRequesition_Slip> DeleteLocalMaterialRequesitionSlip(int id)
        {
            var materialRequesitionSlip = _Localdb.MaterialRequisition_Slip.FirstOrDefault(x => x.MR_ID == id);
            if (materialRequesitionSlip == null)
            {
                return NotFound();
            }

            if (string.Equals(materialRequesitionSlip.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                var error = ReverseApprovedRequisitionEffect(materialRequesitionSlip);
                if (error != null) return Conflict(error);
            }
            else if (IsPending(materialRequesitionSlip.Status))
            {
                var inventory = _Localdb.Inventory
                    .FirstOrDefault(x => x.Material_ID == materialRequesitionSlip.Material_ID);
                if (inventory != null)
                {
                    inventory.Request = Math.Max(
                        GetPendingDemand(materialRequesitionSlip.Material_ID)
                            - (materialRequesitionSlip.Quantity ?? 0),
                        0);
                }
            }

            _Localdb.Remove(materialRequesitionSlip);
            _Localdb.SaveChanges();
            return NoContent();
        }

        [HttpGet("GetCloudMaterialRequesitionSlips")]
        public List<MaterialRequesition_Slip> GetAllCloudMaterialRequesitionSlips()
        {
            return _CloudDb.MaterailsRequisition_Slip.ToList();
        }

        [HttpGet("GetCloudMaterialRequesitionSlipsByID")]
        public ActionResult<MaterialRequesition_Slip> GetCloudMaterialRequesition_SlipByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var materialRequesitionSlip = _CloudDb.MaterailsRequisition_Slip.FirstOrDefault(x => x.MR_ID == id);
            if (materialRequesitionSlip == null)
            {
                return NotFound();
            }
            return Ok(materialRequesitionSlip);
        }

        [HttpPost("AddCloudMaterialRequesitionSlips")]
        public ActionResult<MaterialRequesition_Slip> AddCloudMaterialRequesitionSlips(MaterialRequesition_Slip materialRequesitionSlip)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(materialRequesitionSlip);
            _CloudDb.SaveChanges();
            return Ok(materialRequesitionSlip);
        }

        [HttpPost("UpdateCloudMaterialRequesition_Slip")]
        public ActionResult<MaterialRequesition_Slip> UpdateCloudMaterialRequesition_Slip(Int32 Id, [FromBody] MaterialRequesition_Slip materialRequesitionSlip)
        {
            if (materialRequesitionSlip == null)
            {
                return BadRequest(materialRequesitionSlip);
            }
            var updateMaterialRequesitionSlip = _CloudDb.MaterailsRequisition_Slip.FirstOrDefault(x => x.MR_ID == Id);
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
            updateMaterialRequesitionSlip.Approved_By = materialRequesitionSlip.Approved_By;
            updateMaterialRequesitionSlip.Submmited_By = materialRequesitionSlip.Submmited_By;
            updateMaterialRequesitionSlip.Received_By = materialRequesitionSlip.Received_By;

            _CloudDb.SaveChanges();
            return Ok(updateMaterialRequesitionSlip);
        }

        [HttpPut("DeleteCloudMaterialRequesitionSlip")]
        public ActionResult<MaterialRequesition_Slip> DeleteCloudMaterialRequesitionSlip(int id)
        {
            var materialRequesitionSlip = _CloudDb.MaterailsRequisition_Slip.FirstOrDefault(x => x.MR_ID == id);
            if (materialRequesitionSlip == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(materialRequesitionSlip);
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // --------------------------- MATERIALS REQUESITION SLIP ---------------------------

        // --------------------------- STOCK TRANSFER SLIP ---------------------------

        [HttpGet("GetLocalStockTransferSlips")]
        public List<StockTransfer_Slip> GetAllLocalStockTransferSlips()
        {
            return _Localdb.StockTransfer_Slip.ToList();
        }

        [HttpGet("GetLocalStockTransferSlipByID")]
        public ActionResult<StockTransfer_Slip> GetLocalStockTransferSlipByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var stockTransferSlip = _Localdb.StockTransfer_Slip.FirstOrDefault(x => x.Control_Number == id);
            if (stockTransferSlip == null)
            {
                return NotFound();
            }
            return Ok(stockTransferSlip);
        }

        [HttpPost("AddLocalStockTransfer_Slip")]
        public ActionResult<StockTransfer_Slip> AddLocalStockTransferSlip(StockTransfer_Slip stockTransferSlip)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(stockTransferSlip);
            _Localdb.SaveChanges();
            return Ok(stockTransferSlip);
        }

        [HttpPost("UpdateLocalStockTransfer_Slip")]
        public ActionResult<StockTransfer_Slip> UpdateLocalStockTransferSlip(Int32 Id, [FromBody] StockTransfer_Slip stockTransferSlip)
        {
            if (stockTransferSlip == null)
            {
                return BadRequest(stockTransferSlip);
            }
            var updateStockTransferSlip = _Localdb.StockTransfer_Slip.FirstOrDefault(x => x.Control_Number == Id);
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

            _Localdb.SaveChanges();
            return Ok(updateStockTransferSlip);
        }

        [HttpPut("DeleteLocalStockTransfer_Slip")]
        public ActionResult<StockTransfer_Slip> DeleteLocalStockTransferSlip(int id)
        {
            var stockTransferSlip = _Localdb.StockTransfer_Slip.FirstOrDefault(x => x.Control_Number == id);
            if (stockTransferSlip == null)
            {
                return NotFound();
            }
            _Localdb.Remove(stockTransferSlip);
            _Localdb.SaveChanges();
            return NoContent();
        }



        [HttpGet("GetCloudStockTransferSlips")]
        public List<StockTransfer_Slip> GetAllCloudStockTransferSlips()
        {
            return _CloudDb.StockTransfer_Slip.ToList();
        }

        [HttpGet("GetCloudStockTransferSlipByID")]
        public ActionResult<StockTransfer_Slip> GetCloudStockTransferSlipByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var stockTransferSlip = _CloudDb.StockTransfer_Slip.FirstOrDefault(x => x.Control_Number == id);
            if (stockTransferSlip == null)
            {
                return NotFound();
            }
            return Ok(stockTransferSlip);
        }

        [HttpPost("AddCloudStockTransfer_Slip")]
        public ActionResult<StockTransfer_Slip> AddCloudStockTransferSlip(StockTransfer_Slip stockTransferSlip)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(stockTransferSlip);
            _CloudDb.SaveChanges();
            return Ok(stockTransferSlip);
        }

        [HttpPost("UpdateCloudStockTransfer_Slip")]
        public ActionResult<StockTransfer_Slip> UpdateCloudStockTransferSlip(Int32 Id, [FromBody] StockTransfer_Slip stockTransferSlip)
        {
            if (stockTransferSlip == null)
            {
                return BadRequest(stockTransferSlip);
            }
            var updateStockTransferSlip = _CloudDb.StockTransfer_Slip.FirstOrDefault(x => x.Control_Number == Id);
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

            _CloudDb.SaveChanges();
            return Ok(updateStockTransferSlip);
        }

        [HttpPut("DeleteCloudStockTransfer_Slip")]
        public ActionResult<StockTransfer_Slip> DeleteCloudStockTransferSlip(int id)
        {
            var stockTransferSlip = _CloudDb.StockTransfer_Slip.FirstOrDefault(x => x.Control_Number == id);
            if (stockTransferSlip == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(stockTransferSlip);
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // --------------------------- STOCK TRANSFER SLIP ---------------------------

        // --------------------------- TRANSMITTAL SLIP ---------------------------

        [HttpGet("GetLocalTransmittalSlips")]
        public List<Transmittal_Slip> GetAllTransmittalSlips()
        {
            return _Localdb.Transmittal_Slip.ToList();
        }

        [HttpGet("GetLocalTransmittalSlipsByID")]
        public ActionResult<Transmittal_Slip> GetLocalTransmittalSlipByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var transmittalSlip = _Localdb.Transmittal_Slip.FirstOrDefault(x => x.Control_Number == id);
            if (transmittalSlip == null)
            {
                return NotFound();
            }
            return Ok(transmittalSlip);
        }

        [HttpPost("AddLocalTransmittal_Slip")]
        public ActionResult<Transmittal_Slip> AddLocalTransmittalSlip(Transmittal_Slip transmittalSlip)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(transmittalSlip);
            _Localdb.SaveChanges();
            return Ok(transmittalSlip);
        }

        [HttpPost("UpdateLocalTransmittal_Slip")]
        public ActionResult<Transmittal_Slip> UpdateLocalTransmittalSlip(Int32 Id, [FromBody] Transmittal_Slip transmittalSlip)
        {
            if (transmittalSlip == null)
            {
                return BadRequest(transmittalSlip);
            }
            var updateTransmittalSlip = _Localdb.Transmittal_Slip.FirstOrDefault(x => x.Control_Number == Id);
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

            _Localdb.SaveChanges();
            return Ok(updateTransmittalSlip);
        }

        [HttpPut("DeleteLocalTransmittal_Slip")]
        public ActionResult<Transmittal_Slip> DeleteLocalTransmittalSlip(int id)
        {
            var transmittalSlip = _Localdb.Transmittal_Slip.FirstOrDefault(x => x.Control_Number == id);
            if (transmittalSlip == null)
            {
                return NotFound();
            }
            _Localdb.Remove(transmittalSlip);
            _Localdb.SaveChanges();
            return NoContent();
        }



        [HttpGet("GetCloudTransmittalSlips")]
        public List<Transmittal_Slip> GetAllCloudTransmittalSlips()
        {
            return _CloudDb.Transmittal_Slip.ToList();
        }

        [HttpGet("GetCloudTransmittalSlipsByID")]
        public ActionResult<Transmittal_Slip> GetCloudTransmittalSlipByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var transmittalSlip = _CloudDb.Transmittal_Slip.FirstOrDefault(x => x.Control_Number == id);
            if (transmittalSlip == null)
            {
                return NotFound();
            }
            return Ok(transmittalSlip);
        }

        [HttpPost("AddCloudTransmittal_Slip")]
        public ActionResult<Transmittal_Slip> AddCloudTransmittalSlip(Transmittal_Slip transmittalSlip)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(transmittalSlip);
            _CloudDb.SaveChanges();
            return Ok(transmittalSlip);
        }

        [HttpPost("UpdateCloudTransmittal_Slip")]
        public ActionResult<Transmittal_Slip> UpdateCloudTransmittalSlip(Int32 Id, [FromBody] Transmittal_Slip transmittalSlip)
        {
            if (transmittalSlip == null)
            {
                return BadRequest(transmittalSlip);
            }
            var updateTransmittalSlip = _CloudDb.Transmittal_Slip.FirstOrDefault(x => x.Control_Number == Id);
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

            _CloudDb.SaveChanges();
            return Ok(updateTransmittalSlip);
        }

        [HttpPut("DeleteCloudTransmittal_Slip")]
        public ActionResult<Transmittal_Slip> DeleteCloudTransmittalSlip(int id)
        {
            var transmittalSlip = _CloudDb.Transmittal_Slip.FirstOrDefault(x => x.Control_Number == id);
            if (transmittalSlip == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(transmittalSlip);
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // --------------------------- TRANSMITTAL SLIP ---------------------------

    }
}
