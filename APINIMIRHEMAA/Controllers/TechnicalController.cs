using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO;
using APINIMIRHEMAA.DTO.JobOrders;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize(Policy = "technical")]
    [Route("api/[controller]")]
    [ApiController]
    // Coordinates the technical controller API endpoints, including request handling and response generation.
    public class TechnicalController : ControllerBase
    {
        // Injected or cached localdb dependency/state used by this class.
        private LocalDbContext _Localdb;
        // Injected or cached cloud db dependency/state used by this class.
        private CloudDbContext _CloudDb;
        // Encryption service used to protect sensitive persisted values.
        private readonly EncryptionService _encryptionService;
        // Injected or cached notification service dependency/state used by this class.
        private readonly NotificationService _notificationService;

        // Initializes a new technical controller instance.
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

        // Retrieves the technical visible job orders.
        private IQueryable<JobOrder> GetTechnicalVisibleJobOrders()
        {
            return _Localdb.JobOrder.IgnoreQueryFilters();
        }

        //  ----------------------------------- JOB ORDER -----------------------------------

        [HttpGet("GetLocalJobOrder")]
        // Retrieves all job order. Returns List<JobOrder> asynchronously.
        public async Task<List<JobOrder>> GetAllJobOrder()
        {
            var jobOrders = await GetTechnicalVisibleJobOrders()
                .AsNoTracking()
                .ToListAsync();
            await JobOrderClientQueries.PopulateAsync(_Localdb, jobOrders, HttpContext.RequestAborted);
            return jobOrders;
        }

        /// <summary>Creates a local job order from the supplied data and begins its workflow.</summary>
        [HttpPost("StartLocalJobOrder")]
        public async Task<ActionResult<JobOrder>> StartLocalJobOrder(int jobOrderId)
        {
            // Return HTTP 400 Bad Request when `jobOrderId <= 0` is true.
            if (jobOrderId <= 0)
            {
                return BadRequest("A valid Job Order ID is required.");
            }

            // Return HTTP 404 Not Found when `!await GetTechnicalVisibleJobOrders().AnyAsync(record => record.JobOrder_ID == jobOrderId)` is true.
            if (!await GetTechnicalVisibleJobOrders().AnyAsync(record => record.JobOrder_ID == jobOrderId))
            {
                return NotFound("Job Order not found.");
            }

            var updatedRows = await _Localdb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [dbo].[JobOrder] SET [Status] = CASE WHEN [Production_Completed] = 1 OR [Status] = N'Completed' THEN N'Completed' WHEN [Technical_Completed] = 1 THEN N'In Progress (1/2)' ELSE N'In Progress (0/2)' END WHERE [JobOrder_ID] = {jobOrderId}");
            // Return HTTP 404 Not Found when `updatedRows == 0` is true.
            if (updatedRows == 0)
            {
                return NotFound("Job Order not found.");
            }

            var jobOrder = await GetTechnicalVisibleJobOrders()
                .AsNoTracking()
                .FirstAsync(record => record.JobOrder_ID == jobOrderId);
            return Ok(jobOrder);
        }

        /// <summary>Retrieves the local job order items by job order id.</summary>
        [HttpGet("GetLocalJobOrderItemsByJobOrderID")]
        public async Task<ActionResult<List<JobOrderItems>>> GetLocalJobOrderItemsByJobOrderID(int jobOrderId)
        {
            // Return HTTP 400 Bad Request when `jobOrderId <= 0` is true.
            if (jobOrderId <= 0)
            {
                return BadRequest("Invalid Job Order ID.");
            }

            // Return HTTP 404 Not Found when `!await GetTechnicalVisibleJobOrders().AnyAsync(jobOrder => jobOrder.JobOrder_ID == jobOrderId)` is true.
            if (!await GetTechnicalVisibleJobOrders().AnyAsync(jobOrder => jobOrder.JobOrder_ID == jobOrderId))
            {
                return NotFound("Job Order not found.");
            }

            var items = await _Localdb.JobOrderItems
                .IgnoreQueryFilters()
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(item => item.JobOrder_ID == jobOrderId)
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(item => item.JobOrderItems_ID)
                .ToListAsync();
            return Ok(items);
        }

        /// <summary>Retrieves the local job order by id.</summary>
        [HttpGet("GetLocalJobOrderByID")]
        public async Task<ActionResult<JobOrder>> GetLocalJobOrderByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var jobOrder = await GetTechnicalVisibleJobOrders()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.JobOrder_ID == id, HttpContext.RequestAborted);
            // Return HTTP 404 Not Found when `jobOrder == null` is true.
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

        /// <summary>Retrieves the local job order comments.</summary>
        [HttpGet("GetLocalJobOrderComments")]
        public async Task<ActionResult<JobOrderCommentPage>> GetLocalJobOrderComments(
            int jobOrderId,
            int? beforeCommentId,
            int? afterCommentId,
            int take = 20,
            CancellationToken cancellationToken = default)
        {
            // Return HTTP 400 Bad Request when `jobOrderId <= 0 || (beforeCommentId.HasValue && beforeCommentId.Value <= 0) || (afterCommentId.HasValue && afterCommentId.Value <= 0) || (beforeCommentId.HasValue && afterCommentId.HasValue) || take < 1 || take > 50` is true.
            if (jobOrderId <= 0 ||
                (beforeCommentId.HasValue && beforeCommentId.Value <= 0) ||
                (afterCommentId.HasValue && afterCommentId.Value <= 0) ||
                (beforeCommentId.HasValue && afterCommentId.HasValue) ||
                take < 1 || take > 50)
            {
                return BadRequest("Invalid Job Order comment paging parameters.");
            }
            // Return HTTP 404 Not Found when `!await GetTechnicalVisibleJobOrders() .AnyAsync(jobOrder => jobOrder.JobOrder_ID == jobOrderId, cancellationToken)` is true.
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

        /// <summary>Adds the local job order comment.</summary>
        [HttpPost("AddLocalJobOrderComment")]
        public async Task<ActionResult<JobOrderComment>> AddLocalJobOrderComment(
            [FromBody] AddJobOrderCommentRequest request)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var commentText = request.Comment.Trim();
            // Return HTTP 400 Bad Request when `string.IsNullOrWhiteSpace(commentText)` is true.
            if (string.IsNullOrWhiteSpace(commentText))
            {
                return BadRequest("A comment is required.");
            }

            // Return HTTP 404 Not Found when `!await GetTechnicalVisibleJobOrders().AnyAsync(jobOrder => jobOrder.JobOrder_ID == request.JobOrder_ID)` is true.
            if (!await GetTechnicalVisibleJobOrders().AnyAsync(jobOrder => jobOrder.JobOrder_ID == request.JobOrder_ID))
            {
                return NotFound("Job Order not found.");
            }

            var employeeName = _Localdb.GetCurrentEmployeeName().Trim();
            // Return HTTP 400 Bad Request when `string.IsNullOrWhiteSpace(employeeName)` is true.
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
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            await _notificationService.NotifyJobOrderCommentAsync(comment);
            return Ok(comment);
        }

        /// <summary>Adds the local job order.</summary>
        [HttpPost("AddLocalJobOrder")]
        public async Task<ActionResult<JobOrder>> AddLocalJobOrder(JobOrder jobOrder)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var targetDeliveryError = WorkflowRules.ValidateTargetDelivery(jobOrder.Target_Delivery);
            // Return HTTP 400 Bad Request when `targetDeliveryError != null` is true.
            if (targetDeliveryError != null)
            {
                return BadRequest(targetDeliveryError);
            }
            jobOrder.Comments = new List<JobOrderComment>();
            _Localdb.Add(jobOrder);
            // Process each item in the collection using the loop body.
            // Apply this predicate before materializing the query so unrelated rows are excluded.
            foreach (var purchaseOrder in _Localdb.PurchaseOrder.Where(
                record => record.Conforme_FileID == jobOrder.Conforme_FileID))
            {
                purchaseOrder.Status = "Completed";
            }
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            await _notificationService.NotifyJobOrderReadersAsync(jobOrder.JobOrder_ID);
            return Ok(jobOrder);
        }

        /// <summary>Completes the specified local job order and updates its workflow state.</summary>
        [HttpPost("CompleteLocalJobOrder")]
        public async Task<ActionResult<JobOrder>> CompleteLocalJobOrder(int jobOrderId)
        {
            // Return HTTP 400 Bad Request when `jobOrderId <= 0` is true.
            if (jobOrderId <= 0)
            {
                return BadRequest("A valid Job Order ID is required.");
            }

            // Return HTTP 404 Not Found when `!await GetTechnicalVisibleJobOrders().AnyAsync(record => record.JobOrder_ID == jobOrderId)` is true.
            if (!await GetTechnicalVisibleJobOrders().AnyAsync(record => record.JobOrder_ID == jobOrderId))
            {
                return NotFound("Job Order not found.");
            }

            var updatedRows = await _Localdb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [dbo].[JobOrder] SET [Technical_Completed] = 1, [Status] = CASE WHEN [Production_Completed] = 1 THEN N'Completed' ELSE N'In Progress (1/2)' END WHERE [JobOrder_ID] = {jobOrderId} AND ([Technical_Completed] = 1 OR [Status] = N'In Progress (0/2)')");
            // Enter this branch when `updatedRows == 0` is true.
            if (updatedRows == 0)
            {
                // Return HTTP 404 Not Found when `!await GetTechnicalVisibleJobOrders().AnyAsync(record => record.JobOrder_ID == jobOrderId)` is true.
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

        /// <summary>Updates the local job order.</summary>
        [HttpPost("UpdateLocalJobOrder")]
        public ActionResult<JobOrder> UpdateLocalJobOrder(Int32 Id, [FromBody] JobOrder _jobOrder)
        {
            // Return HTTP 400 Bad Request when `_jobOrder == null` is true.
            if (_jobOrder == null)
            {
                return BadRequest(_jobOrder);
            }
            var targetDeliveryError = WorkflowRules.ValidateTargetDelivery(_jobOrder.Target_Delivery);
            // Return HTTP 400 Bad Request when `targetDeliveryError != null` is true.
            if (targetDeliveryError != null)
            {
                return BadRequest(targetDeliveryError);
            }
            var updateJobOrder = _Localdb.JobOrder.FirstOrDefault(x => x.JobOrder_ID == Id);
            // Return HTTP 404 Not Found when `updateJobOrder == null` is true.
            if (updateJobOrder == null)
            {
                return NotFound();
            }
            updateJobOrder.Target_Delivery = _jobOrder.Target_Delivery;


            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(updateJobOrder);
        }

        /// <summary>Deletes the local job order.</summary>
        [HttpPut("DeleteLocalJobOrder")]
        public ActionResult<JobOrder> DeleteLocalJobOrder(int id)
        {
            var jobOrder = _Localdb.JobOrder.FirstOrDefault(x => x.JobOrder_ID == id);
            // Return HTTP 404 Not Found when `jobOrder == null` is true.
            if (jobOrder == null)
            {
                return NotFound();
            }
            _Localdb.Remove(jobOrder);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }


        /// <summary>Retrieves all job orders from the cloud database.</summary>
        [HttpGet("GetCloudJobOrder")]
        public List<JobOrder> GetAllCloudJobOrder()
        {
            return _Localdb.JobOrder.ToList();
        }

        /// <summary>Retrieves the cloud job order by id.</summary>
        [HttpGet("GetCloudJobOrderByID")]
        public ActionResult<JobOrder> GetCloudJobOrderByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var jobOrder = _CloudDb.JobOrder.FirstOrDefault(x => x.JobOrder_ID == id);
            // Return HTTP 404 Not Found when `jobOrder == null` is true.
            if (jobOrder == null)
            {
                return NotFound();
            }
            return Ok(jobOrder);
        }

        /// <summary>Adds the cloud job order.</summary>
        [HttpPost("AddCloudJobOrder")]
        public ActionResult<JobOrder> AddCloudJobOrder(JobOrder jobOrder)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var targetDeliveryError = WorkflowRules.ValidateTargetDelivery(jobOrder.Target_Delivery);
            // Return HTTP 400 Bad Request when `targetDeliveryError != null` is true.
            if (targetDeliveryError != null)
            {
                return BadRequest(targetDeliveryError);
            }
            _CloudDb.Add(jobOrder);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(jobOrder);
        }

        /// <summary>Updates the cloud job order.</summary>
        [HttpPost("UpdateCloudJobOrder")]
        public ActionResult<JobOrder> UpdateCloudJobOrder(Int32 Id, [FromBody] JobOrder _jobOrder)
        {
            // Return HTTP 400 Bad Request when `_jobOrder == null` is true.
            if (_jobOrder == null)
            {
                return BadRequest(_jobOrder);
            }
            var targetDeliveryError = WorkflowRules.ValidateTargetDelivery(_jobOrder.Target_Delivery);
            // Return HTTP 400 Bad Request when `targetDeliveryError != null` is true.
            if (targetDeliveryError != null)
            {
                return BadRequest(targetDeliveryError);
            }
            var updateJobOrder = _CloudDb.JobOrder.FirstOrDefault(x => x.JobOrder_ID == Id);
            // Return HTTP 404 Not Found when `updateJobOrder == null` is true.
            if (updateJobOrder == null)
            {
                return NotFound();
            }
            updateJobOrder.Installation_Date = _jobOrder.Installation_Date;
            updateJobOrder.Target_Delivery = _jobOrder.Target_Delivery;
            updateJobOrder.Date_Delivered = _jobOrder.Date_Delivered;

            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(updateJobOrder);
        }

        /// <summary>Deletes the cloud job order.</summary>
        [HttpPut("DeleteCloudJobOrder")]
        public ActionResult<JobOrder> DeleteCloudJobOrder(int id)
        {
            var jobOrder = _CloudDb.JobOrder.FirstOrDefault(x => x.JobOrder_ID == id);
            // Return HTTP 404 Not Found when `jobOrder == null` is true.
            if (jobOrder == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(jobOrder);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return NoContent();
        }

        //  ----------------------------------- JOB ORDER -----------------------------------

        //  ----------------------------------- GRAPHICS -----------------------------------

        [HttpGet("GetLocalGraphics")]
        // Retrieves all graphics. Returns List<Graphics>.
        public List<Graphics> GetAllGraphics()
        {
            return _Localdb.Graphics.ToList();
        }

        /// <summary>Retrieves the local job order graphics.</summary>
        [HttpGet("GetLocalJobOrderGraphics")]
        public async Task<ActionResult<List<Graphics>>> GetLocalJobOrderGraphics(int jobOrderId)
        {
            // Return HTTP 400 Bad Request when `jobOrderId <= 0` is true.
            if (jobOrderId <= 0)
            {
                return BadRequest("A valid Job Order ID is required.");
            }

            var jobOrder = await GetTechnicalVisibleJobOrders()
                .AsNoTracking()
                .FirstOrDefaultAsync(record => record.JobOrder_ID == jobOrderId);
            // Return HTTP 404 Not Found when `jobOrder == null` is true.
            if (jobOrder == null)
            {
                return NotFound("Job Order not found.");
            }

            var quotationId = await _Localdb.Conforme
                .IgnoreQueryFilters()
                // Apply this predicate in the database query before materializing results.
                .Where(conforme => conforme.FileID == jobOrder.Conforme_FileID)
                .Select(conforme => (int?)conforme.Quotation_ID)
                .FirstOrDefaultAsync();
            // Exit the current operation when `!quotationId.HasValue` is true.
            if (!quotationId.HasValue)
            {
                return Ok(new List<Graphics>());
            }

            var graphics = await _Localdb.Graphics
                .IgnoreQueryFilters()
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(graphic => graphic.Quotation_ID == quotationId.Value)
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(graphic => graphic.FileID)
                .ToListAsync();
            // Process each `graphic` returned by the collection expression in the loop header.
            foreach (var graphic in graphics)
            {
                graphic.FileData = _encryptionService.TryDecrypt(graphic.FileData);
            }

            return Ok(graphics);
        }

        /// <summary>Retrieves the local graphics by id.</summary>
        [HttpGet("GetLocalGraphicsByID")]
        public ActionResult<Graphics> GetLocalGraphicsByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var graphics = _Localdb.Graphics.FirstOrDefault(x => x.FileID == id);
            // Return HTTP 404 Not Found when `graphics == null` is true.
            if (graphics == null)
            {
                return NotFound();
            }
            return Ok(graphics);
        }

        /// <summary>Adds the local graphics.</summary>
        [HttpPost("AddLocalGraphics")]
        public ActionResult<Graphics> AddLocalGraphics(Graphics graphics)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(graphics);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(graphics);
        }

        /// <summary>Updates the local graphics.</summary>
        [HttpPost("UpdateLocalGraphics")]
        public ActionResult<Graphics> UpdateLocalGraphics(Int32 Id, [FromBody] Graphics _graphics)
        {
            // Return HTTP 400 Bad Request when `_graphics == null` is true.
            if (_graphics == null)
            {
                return BadRequest(_graphics);
            }
            var updateGraphics = _Localdb.Graphics.FirstOrDefault(x => x.FileID == Id);
            // Return HTTP 404 Not Found when `updateGraphics == null` is true.
            if (updateGraphics == null)
            {
                return NotFound();
            }
            updateGraphics.Quotation_ID = _graphics.FileID;
            updateGraphics.FileName = _graphics.FileName;
            updateGraphics.FileExtension = _graphics.FileExtension;
            updateGraphics.FileData = _graphics.FileData;
            updateGraphics.Quotation_ID = _graphics.Quotation_ID;
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(updateGraphics);
        }

        /// <summary>Deletes the local graphics.</summary>
        [HttpPut("DeleteLocalGraphics")]
        public ActionResult<Graphics> DeleteLocalGraphics(int id)
        {
            var graphics = _Localdb.Graphics.FirstOrDefault(x => x.FileID == id);
            // Return HTTP 404 Not Found when `graphics == null` is true.
            if (graphics == null)
            {
                return NotFound();
            }
            _Localdb.Remove(graphics);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }

        /// <summary>Retrieves all graphics from the cloud database.</summary>
        [HttpGet("GetCloudGraphics")]
        public List<Graphics> GetAllCloudGraphics()
        {
            return _CloudDb.Graphics.ToList();
        }

        /// <summary>Retrieves the cloud graphics by id.</summary>
        [HttpGet("GetCloudGraphicsByID")]
        public ActionResult<Graphics> GetCloudGraphicsByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var graphics = _CloudDb.Graphics.FirstOrDefault(x => x.FileID == id);
            // Return HTTP 404 Not Found when `graphics == null` is true.
            if (graphics == null)
            {
                return NotFound();
            }
            return Ok(graphics);
        }

        /// <summary>Adds the cloud graphics.</summary>
        [HttpPost("AddCloudGraphics")]
        public ActionResult<Graphics> AddCloudGraphics(Graphics graphics)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(graphics);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(graphics);
        }

        /// <summary>Updates the cloud graphics.</summary>
        [HttpPost("UpdateCloudGraphics")]
        public ActionResult<Graphics> UpdateCloudGraphics(Int32 Id, [FromBody] Graphics _graphics)
        {
            // Return HTTP 400 Bad Request when `_graphics == null` is true.
            if (_graphics == null)
            {
                return BadRequest(_graphics);
            }
            var updateGraphics = _CloudDb.Graphics.FirstOrDefault(x => x.FileID == Id);
            // Return HTTP 404 Not Found when `updateGraphics == null` is true.
            if (updateGraphics == null)
            {
                return NotFound();
            }
            updateGraphics.Quotation_ID = _graphics.FileID;
            updateGraphics.FileName = _graphics.FileName;
            updateGraphics.FileExtension = _graphics.FileExtension;
            updateGraphics.FileData = _graphics.FileData;
            updateGraphics.Quotation_ID = _graphics.Quotation_ID;
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(updateGraphics);
        }

        /// <summary>Deletes the cloud graphics.</summary>
        [HttpPut("DeleteCloudGraphics")]
        public ActionResult<Graphics> DeleteCloudGraphics(int id)
        {
            var graphics = _CloudDb.Graphics.FirstOrDefault(x => x.FileID == id);
            // Return HTTP 404 Not Found when `graphics == null` is true.
            if (graphics == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(graphics);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return NoContent();
        }

        //  ----------------------------------- GRAPHICS -----------------------------------

        // --------------------------- MATERIALS REQUESITION SLIP ---------------------------

        [HttpGet("GetMaterialRequisitionInventory")]
        // Retrieves material requisition inventory. Inputs: cancellationToken (cancellation token for the asynchronous operation). Returns the HTTP response for the requested operation.
        public async Task<IActionResult> GetMaterialRequisitionInventory(
            CancellationToken cancellationToken)
        {
            var inventory = await MaterialRequisitionLookupQueries.GetInventoryOptionsAsync(
                _Localdb,
                cancellationToken);
            return Ok(inventory);
        }

        /// <summary>Retrieves the material requisition purchase receipts.</summary>
        [HttpGet("GetMaterialRequisitionPurchaseReceipts")]
        public async Task<IActionResult> GetMaterialRequisitionPurchaseReceipts(
            int materialId,
            CancellationToken cancellationToken)
        {
            // Return HTTP 400 Bad Request when `materialId <= 0` is true.
            if (materialId <= 0)
            {
                return BadRequest("A valid material ID is required.");
            }

            var receipts = await MaterialRequisitionLookupQueries.GetPurchaseReceiptOptionsAsync(
                _Localdb,
                materialId,
                cancellationToken);
            return Ok(receipts);
        }

        /// <summary>Validates a material requisition and returns a preview of its stock effects without submitting it.</summary>
        [HttpPost("PreviewMaterialRequisition")]
        public ActionResult PreviewMaterialRequisition([FromBody] RequisitionPreviewDto dto)
        {
            var result = MaterialRequisitionWorkflowService.Preview(_Localdb, dto, "Technical");
            return result.Error != null ? BadRequest(result.Error) : Ok(result.Response);
        }

        /// <summary>Adds the material requisition batch.</summary>
        [HttpPost("AddMaterialRequisitionBatch")]
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
                "Technical",
                cancellationToken);
            return result.Error != null
                ? BadRequest(result.Error)
                : Ok(new { saved = result.Saved });
        }

        /// <summary>Retrieves all material requisition slips from the local database.</summary>
        [HttpGet("GetLocalMaterialRequesitionSlips")]
        public List<MaterialRequesition_Slip> GetAllLocalMaterialRequesitionSlips()
        {
            return _Localdb.MaterialRequisition_Slip
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(slip => slip.FromWho != null && slip.FromWho.Trim().ToLower() == "technical")
                .ToList();
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
            var departmentError = InventoryRules.CheckSameDepartment(
                materialRequesitionSlip.FromWho,
                materialRequesitionSlip.ToWho);
            // Return HTTP 400 Bad Request when `departmentError != null` is true.
            if (departmentError != null) return BadRequest(departmentError);
            await using var transaction = await _Localdb.Database.BeginTransactionAsync(cancellationToken);
            materialRequesitionSlip.Status = "Pending";
            materialRequesitionSlip.Approved_By = null;
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
            var departmentError = InventoryRules.CheckSameDepartment(
                materialRequesitionSlip.FromWho,
                materialRequesitionSlip.ToWho);
            // Return HTTP 400 Bad Request when `departmentError != null` is true.
            if (departmentError != null) return BadRequest(departmentError);
            var updateMaterialRequesitionSlip = _Localdb.MaterialRequisition_Slip.FirstOrDefault(x => x.MR_ID == Id);
            // Return HTTP 404 Not Found when `updateMaterialRequesitionSlip == null` is true.
            if (updateMaterialRequesitionSlip == null)
            {
                return NotFound();
            }
            // Return HTTP 409 Conflict when `string.Equals(updateMaterialRequesitionSlip.Status, , StringComparison.OrdinalIgnoreCase)` is true.
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

    }
}
