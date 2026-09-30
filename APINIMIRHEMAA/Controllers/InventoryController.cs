using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
            return _Localdb.Inventory.ToList();
        }

        [HttpPost("AddLocalInventory")]
        public ActionResult<Inventory> AddLocalInventory([FromBody] Inventory inventory)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Inventory.Add(inventory);
            _Localdb.SaveChanges();
            return Ok(inventory);
        }

        [HttpPost("UpdateLocalInventory")]
        public ActionResult<Inventory> UpdateLocalInventory([FromBody] Inventory inventory)
        {
            if (inventory == null)
            {
                return BadRequest(inventory);
            }

            var _inventory = _Localdb.Inventory.FirstOrDefault(x => x.Material_ID == inventory.Material_ID);
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
            _inventory.Status = inventory.Status;
            _inventory.Material_ID = inventory.Material_ID;
            _Localdb.SaveChanges();

            return Ok(_inventory);
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
            return _CloudDb.Inventory.ToList();
        }

        [HttpPost("AddCloudInventory")]
        public ActionResult<Inventory> AddCloudInventory([FromBody] Inventory inventory)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
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
            _inventory.Status = inventory.Status;
            _inventory.Material_ID = inventory.Material_ID;
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
        public ActionResult<Purchased_Material> AddLocalPurchaseMaterial([FromBody] Purchased_Material purchased_material)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Purchased_Material.Add(purchased_material);
            _Localdb.SaveChanges();
            return Ok(purchased_material);
        }

        [HttpPost("UpdateLocalPurchaseMaterial")]
        public ActionResult<Purchased_Material> UpdateLocalPurchaseMaterial([FromBody] Purchased_Material purchased_material)
        {
            if (purchased_material == null)
            {
                return BadRequest(purchased_material);
            }

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
            _purchased_material.Amount = purchased_material.Amount;
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
            _purchased_material.Amount = purchased_material.Amount;
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
            var materialRequesitionSlip = _Localdb.MaterialRequisition_Slip.FirstOrDefault(x => x.Control_Number == id);
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
            var updateMaterialRequesitionSlip = _Localdb.MaterialRequisition_Slip.FirstOrDefault(x => x.Control_Number == Id);
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
            updateMaterialRequesitionSlip.Status = materialRequesitionSlip.Status;

            _Localdb.SaveChanges();
            return Ok(updateMaterialRequesitionSlip);
        }

        [HttpPut("DeleteLocalMaterialRequesitionSlip")]
        public ActionResult<MaterialRequesition_Slip> DeleteLocalMaterialRequesitionSlip(int id)
        {
            var materialRequesitionSlip = _Localdb.MaterialRequisition_Slip.FirstOrDefault(x => x.Control_Number == id);
            if (materialRequesitionSlip == null)
            {
                return NotFound();
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
            var materialRequesitionSlip = _CloudDb.MaterailsRequisition_Slip.FirstOrDefault(x => x.Control_Number == id);
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
            var updateMaterialRequesitionSlip = _CloudDb.MaterailsRequisition_Slip.FirstOrDefault(x => x.Control_Number == Id);
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
            var materialRequesitionSlip = _CloudDb.MaterailsRequisition_Slip.FirstOrDefault(x => x.Control_Number == id);
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
