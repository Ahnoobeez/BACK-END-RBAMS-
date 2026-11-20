using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO.JobOrders;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize(Policy = "technical")]
    [Route("api/[controller]")]
    [ApiController]
    public class TechnicalController : ControllerBase
    {
        private LocalDbContext _Localdb;
        private CloudDbContext _CloudDb;
        private readonly EncryptionService _encryptionService;
        private readonly NotificationService _notificationService;

        public TechnicalController(
            LocalDbContext Localcontext,
            CloudDbContext Cloudcontext,
            EncryptionService encryptionService,
            NotificationService notificationService)
        {
            _Localdb = Localcontext;
            _CloudDb = Cloudcontext;
            _encryptionService = encryptionService;
            _notificationService = notificationService;
        }

        private IQueryable<JobOrder> GetTechnicalVisibleJobOrders()
        {
            return _Localdb.JobOrder.IgnoreQueryFilters();
        }

        //  ----------------------------------- JOB ORDER -----------------------------------

        [HttpGet("GetLocalJobOrder")]
        public async Task<List<JobOrder>> GetAllJobOrder()
        {
            var jobOrders = await GetTechnicalVisibleJobOrders()
                .AsNoTracking()
                .ToListAsync();
            await JobOrderClientQueries.PopulateAsync(_Localdb, jobOrders, HttpContext.RequestAborted);
            return jobOrders;
        }

        [HttpPost("StartLocalJobOrder")]
        public async Task<ActionResult<JobOrder>> StartLocalJobOrder(int jobOrderId)
        {
            if (jobOrderId <= 0)
            {
                return BadRequest("A valid Job Order ID is required.");
            }

            if (!await GetTechnicalVisibleJobOrders().AnyAsync(record => record.JobOrder_ID == jobOrderId))
            {
                return NotFound("Job Order not found.");
            }

            var updatedRows = await _Localdb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [dbo].[JobOrder] SET [Status] = CASE WHEN [Production_Completed] = 1 OR [Status] = N'Completed' THEN N'Completed' WHEN [Technical_Completed] = 1 THEN N'In Progress (1/2)' ELSE N'In Progress (0/2)' END WHERE [JobOrder_ID] = {jobOrderId}");
            if (updatedRows == 0)
            {
                return NotFound("Job Order not found.");
            }

            var jobOrder = await GetTechnicalVisibleJobOrders()
                .AsNoTracking()
                .FirstAsync(record => record.JobOrder_ID == jobOrderId);
            return Ok(jobOrder);
        }

        [HttpGet("GetLocalJobOrderItemsByJobOrderID")]
        public async Task<ActionResult<List<JobOrderItems>>> GetLocalJobOrderItemsByJobOrderID(int jobOrderId)
        {
            if (jobOrderId <= 0)
            {
                return BadRequest("Invalid Job Order ID.");
            }

            if (!await GetTechnicalVisibleJobOrders().AnyAsync(jobOrder => jobOrder.JobOrder_ID == jobOrderId))
            {
                return NotFound("Job Order not found.");
            }

            var items = await _Localdb.JobOrderItems
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(item => item.JobOrder_ID == jobOrderId)
                .OrderBy(item => item.JobOrderItems_ID)
                .ToListAsync();
            return Ok(items);
        }

        [HttpGet("GetLocalJobOrderByID")]
        public async Task<ActionResult<JobOrder>> GetLocalJobOrderByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var jobOrder = await GetTechnicalVisibleJobOrders()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.JobOrder_ID == id, HttpContext.RequestAborted);
            if (jobOrder == null)
            {
                return NotFound();
            }
            await JobOrderClientQueries.PopulateAsync(
                _Localdb,
                new List<JobOrder> { jobOrder },
                HttpContext.RequestAborted);
            return Ok(jobOrder);
        }

        [HttpGet("GetLocalJobOrderComments")]
        public async Task<ActionResult<JobOrderCommentPage>> GetLocalJobOrderComments(
            int jobOrderId,
            int? beforeCommentId,
            int? afterCommentId,
            int take = 20,
            CancellationToken cancellationToken = default)
        {
            if (jobOrderId <= 0 ||
                (beforeCommentId.HasValue && beforeCommentId.Value <= 0) ||
                (afterCommentId.HasValue && afterCommentId.Value <= 0) ||
                (beforeCommentId.HasValue && afterCommentId.HasValue) ||
                take < 1 || take > 50)
            {
                return BadRequest("Invalid Job Order comment paging parameters.");
            }
            if (!await GetTechnicalVisibleJobOrders()
                .AnyAsync(jobOrder => jobOrder.JobOrder_ID == jobOrderId, cancellationToken))
            {
                return NotFound("Job Order not found.");
            }

            var page = await JobOrderCommentQueries.GetPageAsync(
                _Localdb,
                jobOrderId,
                beforeCommentId,
                afterCommentId,
                take,
                cancellationToken);
            return Ok(page);
        }

        [HttpPost("AddLocalJobOrderComment")]
        public async Task<ActionResult<JobOrderComment>> AddLocalJobOrderComment(
            [FromBody] AddJobOrderCommentRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var commentText = request.Comment.Trim();
            if (string.IsNullOrWhiteSpace(commentText))
            {
                return BadRequest("A comment is required.");
            }

            if (!await GetTechnicalVisibleJobOrders().AnyAsync(jobOrder => jobOrder.JobOrder_ID == request.JobOrder_ID))
            {
                return NotFound("Job Order not found.");
            }

            var employeeName = _Localdb.GetCurrentEmployeeName().Trim();
            if (string.IsNullOrWhiteSpace(employeeName))
            {
                return BadRequest("The signed-in employee name is unavailable.");
            }

            var comment = new JobOrderComment
            {
                JobOrder_ID = request.JobOrder_ID,
                Employee_Name = employeeName,
                Comment = commentText,
                CreatedAt = DateTime.UtcNow
            };
            _Localdb.JobOrderComments.Add(comment);
            await _Localdb.SaveChangesAsync();
            await _notificationService.NotifyJobOrderCommentAsync(comment);
            return Ok(comment);
        }

        [HttpPost("AddLocalJobOrder")]
        public async Task<ActionResult<JobOrder>> AddLocalJobOrder(JobOrder jobOrder)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var targetDeliveryError = WorkflowRules.ValidateTargetDelivery(jobOrder.Target_Delivery);
            if (targetDeliveryError != null)
            {
                return BadRequest(targetDeliveryError);
            }
            jobOrder.Comments = new List<JobOrderComment>();
            _Localdb.Add(jobOrder);
            foreach (var purchaseOrder in _Localdb.PurchaseOrder.Where(
                record => record.Conforme_FileID == jobOrder.Conforme_FileID))
            {
                purchaseOrder.Status = "Completed";
            }
            await _Localdb.SaveChangesAsync();
            await _notificationService.NotifyJobOrderReadersAsync(jobOrder.JobOrder_ID);
            return Ok(jobOrder);
        }

        [HttpPost("CompleteLocalJobOrder")]
        public async Task<ActionResult<JobOrder>> CompleteLocalJobOrder(int jobOrderId)
        {
            if (jobOrderId <= 0)
            {
                return BadRequest("A valid Job Order ID is required.");
            }

            if (!await GetTechnicalVisibleJobOrders().AnyAsync(record => record.JobOrder_ID == jobOrderId))
            {
                return NotFound("Job Order not found.");
            }

            var updatedRows = await _Localdb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [dbo].[JobOrder] SET [Technical_Completed] = 1, [Status] = CASE WHEN [Production_Completed] = 1 THEN N'Completed' ELSE N'In Progress (1/2)' END WHERE [JobOrder_ID] = {jobOrderId} AND ([Technical_Completed] = 1 OR [Status] = N'In Progress (0/2)')");
            if (updatedRows == 0)
            {
                if (!await GetTechnicalVisibleJobOrders().AnyAsync(record => record.JobOrder_ID == jobOrderId))
                {
                    return NotFound("Job Order not found.");
                }
                return Conflict("Open the Job Order before marking Technical work complete.");
            }

            var jobOrder = await GetTechnicalVisibleJobOrders()
                .AsNoTracking()
                .FirstAsync(record => record.JobOrder_ID == jobOrderId);
            return Ok(jobOrder);
        }

        [HttpPost("UpdateLocalJobOrder")]
        public ActionResult<JobOrder> UpdateLocalJobOrder(Int32 Id, [FromBody] JobOrder _jobOrder)
        {
            if (_jobOrder == null)
            {
                return BadRequest(_jobOrder);
            }
            var targetDeliveryError = WorkflowRules.ValidateTargetDelivery(_jobOrder.Target_Delivery);
            if (targetDeliveryError != null)
            {
                return BadRequest(targetDeliveryError);
            }
            var updateJobOrder = _Localdb.JobOrder.FirstOrDefault(x => x.JobOrder_ID == Id);
            if (updateJobOrder == null)
            {
                return NotFound();
            }
            updateJobOrder.Target_Delivery = _jobOrder.Target_Delivery;


            _Localdb.SaveChanges();
            return Ok(updateJobOrder);
        }

        [HttpPut("DeleteLocalJobOrder")]
        public ActionResult<JobOrder> DeleteLocalJobOrder(int id)
        {
            var jobOrder = _Localdb.JobOrder.FirstOrDefault(x => x.JobOrder_ID == id);
            if (jobOrder == null)
            {
                return NotFound();
            }
            _Localdb.Remove(jobOrder);
            _Localdb.SaveChanges();
            return NoContent();
        }


        [HttpGet("GetCloudJobOrder")]
        public List<JobOrder> GetAllCloudJobOrder()
        {
            return _Localdb.JobOrder.ToList();
        }

        [HttpGet("GetCloudJobOrderByID")]
        public ActionResult<JobOrder> GetCloudJobOrderByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var jobOrder = _CloudDb.JobOrder.FirstOrDefault(x => x.JobOrder_ID == id);
            if (jobOrder == null)
            {
                return NotFound();
            }
            return Ok(jobOrder);
        }

        [HttpPost("AddCloudJobOrder")]
        public ActionResult<JobOrder> AddCloudJobOrder(JobOrder jobOrder)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var targetDeliveryError = WorkflowRules.ValidateTargetDelivery(jobOrder.Target_Delivery);
            if (targetDeliveryError != null)
            {
                return BadRequest(targetDeliveryError);
            }
            _CloudDb.Add(jobOrder);
            _CloudDb.SaveChanges();
            return Ok(jobOrder);
        }

        [HttpPost("UpdateCloudJobOrder")]
        public ActionResult<JobOrder> UpdateCloudJobOrder(Int32 Id, [FromBody] JobOrder _jobOrder)
        {
            if (_jobOrder == null)
            {
                return BadRequest(_jobOrder);
            }
            var targetDeliveryError = WorkflowRules.ValidateTargetDelivery(_jobOrder.Target_Delivery);
            if (targetDeliveryError != null)
            {
                return BadRequest(targetDeliveryError);
            }
            var updateJobOrder = _CloudDb.JobOrder.FirstOrDefault(x => x.JobOrder_ID == Id);
            if (updateJobOrder == null)
            {
                return NotFound();
            }
            updateJobOrder.Installation_Date = _jobOrder.Installation_Date;
            updateJobOrder.Target_Delivery = _jobOrder.Target_Delivery;
            updateJobOrder.Date_Delivered = _jobOrder.Date_Delivered;

            _CloudDb.SaveChanges();
            return Ok(updateJobOrder);
        }

        [HttpPut("DeleteCloudJobOrder")]
        public ActionResult<JobOrder> DeleteCloudJobOrder(int id)
        {
            var jobOrder = _CloudDb.JobOrder.FirstOrDefault(x => x.JobOrder_ID == id);
            if (jobOrder == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(jobOrder);
            _CloudDb.SaveChanges();
            return NoContent();
        }

        //  ----------------------------------- JOB ORDER -----------------------------------

        //  ----------------------------------- GRAPHICS -----------------------------------

        [HttpGet("GetLocalGraphics")]
        public List<Graphics> GetAllGraphics()
        {
            return _Localdb.Graphics.ToList();
        }

        [HttpGet("GetLocalJobOrderGraphics")]
        public async Task<ActionResult<List<Graphics>>> GetLocalJobOrderGraphics(int jobOrderId)
        {
            if (jobOrderId <= 0)
            {
                return BadRequest("A valid Job Order ID is required.");
            }

            var jobOrder = await GetTechnicalVisibleJobOrders()
                .AsNoTracking()
                .FirstOrDefaultAsync(record => record.JobOrder_ID == jobOrderId);
            if (jobOrder == null)
            {
                return NotFound("Job Order not found.");
            }

            var quotationId = await _Localdb.Conforme
                .IgnoreQueryFilters()
                .Where(conforme => conforme.FileID == jobOrder.Conforme_FileID)
                .Select(conforme => (int?)conforme.Quotation_ID)
                .FirstOrDefaultAsync();
            if (!quotationId.HasValue)
            {
                return Ok(new List<Graphics>());
            }

            var graphics = await _Localdb.Graphics
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(graphic => graphic.Quotation_ID == quotationId.Value)
                .OrderBy(graphic => graphic.FileID)
                .ToListAsync();
            foreach (var graphic in graphics)
            {
                graphic.FileData = _encryptionService.TryDecrypt(graphic.FileData);
            }

            return Ok(graphics);
        }

        [HttpGet("GetLocalGraphicsByID")]
        public ActionResult<Graphics> GetLocalGraphicsByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var graphics = _Localdb.Graphics.FirstOrDefault(x => x.FileID == id);
            if (graphics == null)
            {
                return NotFound();
            }
            return Ok(graphics);
        }

        [HttpPost("AddLocalGraphics")]
        public ActionResult<Graphics> AddLocalGraphics(Graphics graphics)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(graphics);
            _Localdb.SaveChanges();
            return Ok(graphics);
        }

        [HttpPost("UpdateLocalGraphics")]
        public ActionResult<Graphics> UpdateLocalGraphics(Int32 Id, [FromBody] Graphics _graphics)
        {
            if (_graphics == null)
            {
                return BadRequest(_graphics);
            }
            var updateGraphics = _Localdb.Graphics.FirstOrDefault(x => x.FileID == Id);
            if (updateGraphics == null)
            {
                return NotFound();
            }
            updateGraphics.Quotation_ID = _graphics.FileID;
            updateGraphics.FileName = _graphics.FileName;
            updateGraphics.FileExtension = _graphics.FileExtension;
            updateGraphics.FileData = _graphics.FileData;
            updateGraphics.Quotation_ID = _graphics.Quotation_ID;
            _Localdb.SaveChanges();
            return Ok(updateGraphics);
        }

        [HttpPut("DeleteLocalGraphics")]
        public ActionResult<Graphics> DeleteLocalGraphics(int id)
        {
            var graphics = _Localdb.Graphics.FirstOrDefault(x => x.FileID == id);
            if (graphics == null)
            {
                return NotFound();
            }
            _Localdb.Remove(graphics);
            _Localdb.SaveChanges();
            return NoContent();
        }

        [HttpGet("GetCloudGraphics")]
        public List<Graphics> GetAllCloudGraphics()
        {
            return _CloudDb.Graphics.ToList();
        }

        [HttpGet("GetCloudGraphicsByID")]
        public ActionResult<Graphics> GetCloudGraphicsByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var graphics = _CloudDb.Graphics.FirstOrDefault(x => x.FileID == id);
            if (graphics == null)
            {
                return NotFound();
            }
            return Ok(graphics);
        }

        [HttpPost("AddCloudGraphics")]
        public ActionResult<Graphics> AddCloudGraphics(Graphics graphics)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(graphics);
            _CloudDb.SaveChanges();
            return Ok(graphics);
        }

        [HttpPost("UpdateCloudGraphics")]
        public ActionResult<Graphics> UpdateCloudGraphics(Int32 Id, [FromBody] Graphics _graphics)
        {
            if (_graphics == null)
            {
                return BadRequest(_graphics);
            }
            var updateGraphics = _CloudDb.Graphics.FirstOrDefault(x => x.FileID == Id);
            if (updateGraphics == null)
            {
                return NotFound();
            }
            updateGraphics.Quotation_ID = _graphics.FileID;
            updateGraphics.FileName = _graphics.FileName;
            updateGraphics.FileExtension = _graphics.FileExtension;
            updateGraphics.FileData = _graphics.FileData;
            updateGraphics.Quotation_ID = _graphics.Quotation_ID;
            _CloudDb.SaveChanges();
            return Ok(updateGraphics);
        }

        [HttpPut("DeleteCloudGraphics")]
        public ActionResult<Graphics> DeleteCloudGraphics(int id)
        {
            var graphics = _CloudDb.Graphics.FirstOrDefault(x => x.FileID == id);
            if (graphics == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(graphics);
            _CloudDb.SaveChanges();
            return NoContent();
        }

        //  ----------------------------------- GRAPHICS -----------------------------------

        // --------------------------- MATERIALS REQUESITION SLIP ---------------------------

        [HttpGet("GetLocalMaterialRequesitionSlips")]
        public List<MaterialRequesition_Slip> GetAllLocalMaterialRequesitionSlips()
        {
            return _Localdb.MaterialRequisition_Slip
                .AsNoTracking()
                .Where(slip => slip.FromWho != null && slip.FromWho.Trim().ToLower() == "technical")
                .ToList();
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
        public async Task<ActionResult<MaterialRequesition_Slip>> AddLocalMaterialRequesitionSlips(
            MaterialRequesition_Slip materialRequesitionSlip,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var departmentError = InventoryRules.CheckSameDepartment(
                materialRequesitionSlip.FromWho,
                materialRequesitionSlip.ToWho);
            if (departmentError != null) return BadRequest(departmentError);
            await using var transaction = await _Localdb.Database.BeginTransactionAsync(cancellationToken);
            materialRequesitionSlip.Status = "Pending";
            materialRequesitionSlip.Approved_By = null;
            _Localdb.Add(materialRequesitionSlip);
            await _Localdb.SaveChangesAsync(cancellationToken);
            await _notificationService.NotifyMaterialRequisitionSubmittedAsync(
                new[] { materialRequesitionSlip },
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(materialRequesitionSlip);
        }

        [HttpPost("UpdateLocalMaterialRequesition_Slip")]
        public ActionResult<MaterialRequesition_Slip> UpdateLocalMaterialRequesition_Slip(Int32 Id, [FromBody] MaterialRequesition_Slip materialRequesitionSlip)
        {
            if (materialRequesitionSlip == null)
            {
                return BadRequest(materialRequesitionSlip);
            }
            var departmentError = InventoryRules.CheckSameDepartment(
                materialRequesitionSlip.FromWho,
                materialRequesitionSlip.ToWho);
            if (departmentError != null) return BadRequest(departmentError);
            var updateMaterialRequesitionSlip = _Localdb.MaterialRequisition_Slip.FirstOrDefault(x => x.MR_ID == Id);
            if (updateMaterialRequesitionSlip == null)
            {
                return NotFound();
            }
            if (string.Equals(updateMaterialRequesitionSlip.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict("An approved Material Requisition cannot be edited.");
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
            var departmentError = InventoryRules.CheckSameDepartment(
                materialRequesitionSlip.FromWho,
                materialRequesitionSlip.ToWho);
            if (departmentError != null) return BadRequest(departmentError);
            materialRequesitionSlip.Approved_By = null;
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
            var departmentError = InventoryRules.CheckSameDepartment(
                materialRequesitionSlip.FromWho,
                materialRequesitionSlip.ToWho);
            if (departmentError != null) return BadRequest(departmentError);
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

    }
}
