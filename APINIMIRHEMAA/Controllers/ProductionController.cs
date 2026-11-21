using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO;
using APINIMIRHEMAA.DTO.AuditTrail;
using APINIMIRHEMAA.DTO.InstallationReport;
using APINIMIRHEMAA.DTO.InstallationSchedule;
using APINIMIRHEMAA.DTO.JobOrders;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;
using System.Text.Json;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize(Policy = "production")]
    [Route("api/[controller]")]
    [ApiController]

    public class ProductionController : ControllerBase
    {

        private LocalDbContext _Localdb;

        private CloudDbContext _CloudDb;

        private readonly EncryptionService _encryptionService;

        private readonly NotificationService _notificationService;


        public ProductionController(
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


        private int? GetProductionDepartmentId()
        {
            return _Localdb.Departments
                .AsNoTracking()

                .Where(department =>
                    department.Department_Name.ToLower().Contains("production") ||
                    department.Department_Name.ToLower().Contains("joborder") ||
                    department.Department_Name.ToLower().Contains("job order"))
                .Select(department => (int?)department.Department_ID)
                .FirstOrDefault();
        }


        private IQueryable<JobOrder> GetProductionVisibleJobOrders()
        {
            return _Localdb.JobOrder.IgnoreQueryFilters();
        }


        private static bool IsCompletedJobOrder(JobOrder jobOrder)
        {
            return jobOrder.Production_Completed ||
                string.Equals(jobOrder.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(jobOrder.Status, "Complete", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(jobOrder.Status, "Complited", StringComparison.OrdinalIgnoreCase);
        }


        private bool IsProductionApprover()
        {
            return _Localdb.HasGlobalRecordAccess ||
                (_Localdb.IsDepartmentManager &&
                    ModuleAccess.GetModuleForDepartment(_Localdb.CurrentDepartmentName) == "production");
        }


        [HttpGet("GetInstallationReportJobOrders")]
        public async Task<ActionResult<IReadOnlyList<InstallationReportJobOrderOption>>> GetInstallationReportJobOrders(
            CancellationToken cancellationToken)
        {
            var jobOrders = await GetProductionVisibleJobOrders()
                .AsNoTracking()

                .Where(jobOrder =>
                    jobOrder.Production_Completed ||
                    jobOrder.Status == "Completed" ||
                    jobOrder.Status == "Complete" ||
                    jobOrder.Status == "complited")

                .OrderByDescending(jobOrder => jobOrder.JobOrder_ID)
                .ToListAsync(cancellationToken);
            var existingReportJobOrderIds = await _Localdb.InstallationReport
                .AsNoTracking()
                .Select(report => report.JobOrder_ID)
                .Distinct()
                .ToListAsync(cancellationToken);
            jobOrders = jobOrders

                .Where(jobOrder => !existingReportJobOrderIds.Contains(jobOrder.JobOrder_ID))
                .ToList();


            if (jobOrders.Count == 0)
            {
                return Ok(Array.Empty<InstallationReportJobOrderOption>());
            }

            var conformeIds = jobOrders.Select(jobOrder => jobOrder.Conforme_FileID).Distinct().ToArray();
            var conformes = await _Localdb.Conforme
                .AsNoTracking()

                .Where(conforme => conformeIds.Contains(conforme.FileID))
                .Select(conforme => new { conforme.FileID, conforme.Quotation_ID })
                .ToDictionaryAsync(conforme => conforme.FileID, cancellationToken);

            var quotationIds = conformes.Values.Select(conforme => conforme.Quotation_ID).Distinct().ToArray();
            var quotations = await _Localdb.Quotation
                .AsNoTracking()

                .Where(quotation => quotationIds.Contains(quotation.Quotation_ID))
                .Select(quotation => new { quotation.Quotation_ID, quotation.Client_ID, quotation.Title })
                .ToDictionaryAsync(quotation => quotation.Quotation_ID, cancellationToken);

            var clientIds = quotations.Values.Select(quotation => quotation.Client_ID).Distinct().ToArray();
            var clients = await _Localdb.Clients
                .AsNoTracking()

                .Where(client => clientIds.Contains(client.Client_ID))
                .Select(client => new { client.Client_ID, client.Client_Name, client.Company_Address })
                .ToDictionaryAsync(client => client.Client_ID, cancellationToken);

            var options = jobOrders.Select(jobOrder =>
            {

                string? clientName = null;
                string? location = null;
                string? quotationTitle = null;


                if (conformes.TryGetValue(jobOrder.Conforme_FileID, out var conforme)
                    && quotations.TryGetValue(conforme.Quotation_ID, out var quotation))
                {
                    quotationTitle = quotation.Title;

                    if (clients.TryGetValue(quotation.Client_ID, out var client))
                    {
                        clientName = client.Client_Name;
                        location = client.Company_Address;
                    }
                }

                return new InstallationReportJobOrderOption
                {
                    JobOrderId = jobOrder.JobOrder_ID,
                    ClientName = clientName,
                    ProjectName = string.IsNullOrWhiteSpace(jobOrder.Title) ? quotationTitle : jobOrder.Title,
                    Location = location,
                    InstallationDate = jobOrder.Installation_Date
                };
            }).ToArray();

            return Ok(options);
        }


        [HttpPost("AddLocalInstallationReport")]
        [RequestSizeLimit(10_000_000)]
        public async Task<ActionResult> AddLocalInstallationReport(
            [FromBody] CreateInstallationReportRequest request,
            CancellationToken cancellationToken)
        {

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var jobOrderExists = await _Localdb.JobOrder
                .AsNoTracking()
                .AnyAsync(jobOrder => jobOrder.JobOrder_ID == request.JobOrderId, cancellationToken);

            if (!jobOrderExists)
            {
                return NotFound("The selected Job Order was not found.");
            }

            if (await _Localdb.InstallationReport
                .AsNoTracking()
                .AnyAsync(report => report.JobOrder_ID == request.JobOrderId, cancellationToken))
            {
                return Conflict("An Installation Report already exists for the selected Job Order.");
            }


            if (request.Signatures == null || request.Signatures.Count == 0 || request.Signatures.Count > 2)
            {
                return BadRequest("The prepared-by user signature is required, and no more than two sign-off images may be submitted.");
            }

            var signatureRoles = new HashSet<string>(StringComparer.Ordinal);
            var signatureFiles = new List<InstallationReportFile>();

            foreach (var signature in request.Signatures)
            {

                if (signature == null)
                {
                    return BadRequest("An Installation Report signature entry is missing.");
                }

                if (!signatureRoles.Add(signature.FileRole))
                {
                    return BadRequest($"Only one {signature.FileRole} signature image may be submitted.");
                }

                var extension = signature.FileExtension.Trim().ToLowerInvariant();

                if (extension is not ".png" and not ".jpg" and not ".jpeg")
                {
                    return BadRequest("Installation Report sign-off files must be JPG or PNG images.");
                }

                var validationError = UploadValidator.ValidateImage(signature.FileData);

                if (validationError != null)
                {
                    return BadRequest(validationError);
                }

                var fileData = signature.FileData;

                if (fileData.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var separator = fileData.IndexOf(',');
                    fileData = separator < 0 ? string.Empty : fileData[(separator + 1)..];
                }

                byte[] imageBytes;
                try
                {
                    imageBytes = Convert.FromBase64String(fileData);
                }

                catch (FormatException)
                {
                    return BadRequest("An Installation Report signature image contains invalid file data.");
                }

                var fileName = Path.GetFileName(signature.FileName.Trim());

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    return BadRequest("Every Installation Report signature image must have a file name.");
                }

                signatureFiles.Add(new InstallationReportFile
                {
                    File_Role = signature.FileRole,
                    File_Name = fileName,
                    File_Extension = extension,
                    File_Data = _encryptionService.Encrypt(imageBytes)
                });
            }


            if (!signatureRoles.Contains("PreparedBy"))
            {
                return BadRequest("The prepared-by user signature is required.");
            }

            var report = new InstallationReport
            {
                JobOrder_ID = request.JobOrderId,
                ClientName = request.ClientName,
                ProjectName = request.ProjectName,
                Location = request.Location,
                ReportDate = request.ReportDate,
                TimeStarted = request.TimeStarted,
                TimeEnded = request.TimeEnded,
                Installers = request.Installers,
                OnTime = request.OnTime,
                UniformAndId = request.UniformAndId,
                GraphicsProperlyInstalled = request.GraphicsProperlyInstalled,
                ProblemsOccurred = request.ProblemsOccurred,
                ProblemsNote = request.ProblemsNote,
                AreaClean = request.AreaClean,
                InstallersMannered = request.InstallersMannered,
                ClientRemarks = request.ClientRemarks,
                Obstruction = request.Obstruction,
                ObstructionDetails = request.ObstructionDetails,
                GraphicsDamaged = request.GraphicsDamaged,
                DamageDetails = request.DamageDetails,
                InstallerRemarks = request.InstallerRemarks,
                ClientSignature = request.ClientSignature,
                PreparedBy = request.PreparedBy,
                ApprovedBy = null
            };

            await using var transaction = await _Localdb.Database.BeginTransactionAsync(cancellationToken);
            _Localdb.InstallationReport.Add(report);

            await _Localdb.SaveChangesAsync(cancellationToken);

            foreach (var signatureFile in signatureFiles)
            {
                signatureFile.InstallationReport_ID = report.InstallationReport_ID;
            }
            await _Localdb.InstallationReportFiles.AddRangeAsync(signatureFiles, cancellationToken);

            await _Localdb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new
            {
                installationReportId = report.InstallationReport_ID,
                jobOrderId = report.JobOrder_ID
            });
        }


        [HttpPost("ApproveInstallationReport/{id:int}")]
        public async Task<ActionResult> ApproveInstallationReport(
            int id,
            CancellationToken cancellationToken)
        {

            if (!IsProductionApprover())
            {
                return Forbid();
            }


            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var approverUserId))
            {
                return Unauthorized("Your session is invalid. Please sign in again.");
            }

            await using var transaction = await _Localdb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var report = await _Localdb.InstallationReport
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(record => record.InstallationReport_ID == id, cancellationToken);

            if (report == null)
            {
                return NotFound("The Installation Report was not found.");
            }

            var preparedByUserId = await _Localdb.InstallationReportFiles
                .IgnoreQueryFilters()

                .Where(file =>
                    file.InstallationReport_ID == id &&
                    file.File_Role == "PreparedBy")

                .OrderBy(file => file.InstallationReportFile_ID)
                .Select(file => file.CreatedByUserId)
                .FirstOrDefaultAsync(cancellationToken);

            if (!preparedByUserId.HasValue)
            {
                return BadRequest("This Installation Report must have a preparer's signature before approval.");
            }

            if (preparedByUserId.Value == approverUserId)
            {
                return BadRequest("A different authorized user must approve this Installation Report.");
            }

            var existingApproval = await _Localdb.InstallationReportFiles
                .IgnoreQueryFilters()
                .AnyAsync(file =>
                    file.InstallationReport_ID == id &&
                    file.File_Role == "ApproverSigned",
                    cancellationToken);

            if (existingApproval)
            {
                return Conflict("This Installation Report has already been approved.");
            }

            var approverSignature = await _Localdb.UserSignatures
                .AsNoTracking()
                .SingleOrDefaultAsync(signature => signature.User_ID == approverUserId, cancellationToken);

            if (approverSignature == null || approverSignature.SignatureData.Length == 0)
            {
                return BadRequest("Add a signature in Profile Settings before approving an Installation Report.");
            }

            report.ApprovedBy = User.FindFirstValue("EmployeeName") ??
                User.FindFirstValue(ClaimTypes.Name);
            _Localdb.InstallationReportFiles.Add(new InstallationReportFile
            {
                InstallationReport_ID = id,
                File_Role = "ApproverSigned",
                File_Name = "approved-by-signature.png",
                File_Extension = ".png",
                File_Data = _encryptionService.Encrypt(approverSignature.SignatureData)
            });

            await _Localdb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(new { message = "Installation Report approved and signed." });
        }


        [HttpGet("GetDeliveryReceiptApprovalQueue")]
        public async Task<ActionResult<List<DeliveryReceipt>>> GetDeliveryReceiptApprovalQueue(
            CancellationToken cancellationToken)
        {
            var receipts = await _Localdb.DeliveryReceipt
                .IgnoreQueryFilters()
                .AsNoTracking()

                .OrderByDescending(receipt => receipt.Delivery_ID)
                .ToListAsync(cancellationToken);
            var deliveryIds = receipts.Select(receipt => receipt.Delivery_ID).ToArray();
            var files = deliveryIds.Length == 0
                ? new List<DeliveryFiles>()
                : await _Localdb.DeliveryFiles
                    .IgnoreQueryFilters()
                    .AsNoTracking()

                    .Where(file => deliveryIds.Contains(file.Delivery_ID))
                    .ToListAsync(cancellationToken);

            foreach (var file in files)
            {
                file.File_Data = _encryptionService.TryDecrypt(file.File_Data);
            }

            foreach (var receipt in receipts)
            {
                receipt.ApprovalStatus = WorkflowRules.GetDeliveryReceiptApprovalStatus(
                    receipt.Status,
                    files,
                    receipt.Delivery_ID);
                receipt.Status = WorkflowRules.GetDeliveryReceiptStatus(
                    WorkflowRules.HasReceivedByPhoto(files, receipt.Delivery_ID));
            }
            return Ok(receipts);
        }


        [HttpGet("GetDeliveryReceiptApprovalItems")]
        public async Task<ActionResult<List<DeliveryReceiptItems>>> GetDeliveryReceiptApprovalItems(
            CancellationToken cancellationToken)
        {
            var items = await _Localdb.DeliveryReceiptItems
                .IgnoreQueryFilters()
                .AsNoTracking()

                .OrderBy(item => item.Delivery_ID)

                .ThenBy(item => item.DeliveryReceiptItems_ID)
                .ToListAsync(cancellationToken);
            return Ok(items);
        }


        [HttpGet("GetDeliveryReceiptApprovalSignatures")]
        public async Task<ActionResult<List<DeliveryFiles>>> GetDeliveryReceiptApprovalSignatures(
            CancellationToken cancellationToken)
        {
            var files = await _Localdb.DeliveryFiles
                .IgnoreQueryFilters()
                .AsNoTracking()

                .Where(file => file.File_Role == "ApproverSigned" || file.File_Role == "PreparedBy")
                .ToListAsync(cancellationToken);
            var preparedByUsers = files

                .Where(file => string.Equals(file.File_Role, "PreparedBy", StringComparison.OrdinalIgnoreCase))
                .GroupBy(file => file.Delivery_ID)
                .ToDictionary(
                    group => group.Key,

                    group => group.OrderBy(file => file.File_ID)
                        .Select(file => file.CreatedByUserId)
                        .FirstOrDefault());
            var approvals = files

                .Where(file =>
                    string.Equals(file.File_Role, "ApproverSigned", StringComparison.OrdinalIgnoreCase) &&
                    preparedByUsers.TryGetValue(file.Delivery_ID, out var preparedByUserId) &&
                    preparedByUserId.HasValue &&
                    file.CreatedByUserId.HasValue &&
                    preparedByUserId.Value != file.CreatedByUserId.Value)
                .ToList();

            foreach (var file in approvals)
            {
                file.File_Data = _encryptionService.TryDecrypt(file.File_Data);
            }
            return Ok(approvals);
        }


        [HttpPost("ApproveDeliveryReceipt/{id:int}")]
        public Task<ActionResult> ApproveDeliveryReceipt(int id, CancellationToken cancellationToken) =>
            SetDeliveryReceiptApprovalStatus(id, "Approved", cancellationToken);


        [HttpPost("RejectDeliveryReceipt/{id:int}")]
        public Task<ActionResult> RejectDeliveryReceipt(int id, CancellationToken cancellationToken) =>
            SetDeliveryReceiptApprovalStatus(id, "Rejected", cancellationToken);


        private async Task<ActionResult> SetDeliveryReceiptApprovalStatus(
            int id,
            string status,
            CancellationToken cancellationToken)
        {

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var approverUserId))
            {
                return Unauthorized("Your session is invalid. Please sign in again.");
            }

            await using var transaction = await _Localdb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var receipt = await _Localdb.DeliveryReceipt
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(record => record.Delivery_ID == id, cancellationToken);

            if (receipt == null)
            {
                return NotFound("The Delivery Receipt was not found.");
            }

            var files = await _Localdb.DeliveryFiles
                .IgnoreQueryFilters()

                .Where(file => file.Delivery_ID == id)
                .ToListAsync(cancellationToken);
            var currentStatus = WorkflowRules.GetDeliveryReceiptApprovalStatus(
                receipt.Status,
                files,
                id);

            if (currentStatus != "Request")
            {
                return Conflict($"This Delivery Receipt has already been {currentStatus.ToLowerInvariant()}.");
            }

            var preparedByUserId = files

                .Where(file => string.Equals(file.File_Role, "PreparedBy", StringComparison.OrdinalIgnoreCase))

                .OrderBy(file => file.File_ID)
                .Select(file => file.CreatedByUserId)
                .FirstOrDefault();

            if (!preparedByUserId.HasValue)
            {
                return BadRequest("This Delivery Receipt must have a preparer's signature before review.");
            }

            if (preparedByUserId.Value == approverUserId)
            {
                return BadRequest("A different authorized user must review this Delivery Receipt.");
            }


            if (status == "Approved")
            {
                var approverSignature = await _Localdb.UserSignatures
                    .AsNoTracking()
                    .SingleOrDefaultAsync(signature => signature.User_ID == approverUserId, cancellationToken);

                if (approverSignature == null || approverSignature.SignatureData.Length == 0)
                {
                    return BadRequest("Add a signature in Profile Settings before approving a Delivery Receipt.");
                }

                var legacyApprovals = await _Localdb.DeliveryFiles
                    .IgnoreQueryFilters()

                    .Where(file => file.Delivery_ID == id && file.File_Role == "ApprovedBy")
                    .ToListAsync(cancellationToken);
                var priorApprovals = files

                    .Where(file => string.Equals(file.File_Role, "ApproverSigned", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                _Localdb.DeliveryFiles.RemoveRange(legacyApprovals);
                _Localdb.DeliveryFiles.RemoveRange(priorApprovals);
                _Localdb.DeliveryFiles.Add(new DeliveryFiles
                {
                    Delivery_ID = id,
                    File_Role = "ApproverSigned",
                    File_Name = "approved-by-signature.png",
                    File_Extension = ".png",
                    File_Data = _encryptionService.Encrypt(approverSignature.SignatureData)
                });
            }

            receipt.Status = status;

            await _Localdb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(new { message = $"Delivery Receipt {status.ToLowerInvariant()}." });
        }


        [HttpGet("GetLocalJobOrder")]

        public async Task<List<JobOrder>> GetAllJobOrder()
        {
            var jobOrders = await GetProductionVisibleJobOrders()
                .AsNoTracking()
                .ToListAsync(HttpContext.RequestAborted);
            await JobOrderClientQueries.PopulateAsync(_Localdb, jobOrders, HttpContext.RequestAborted);
            return jobOrders;
        }


        [HttpGet("GetLocalJobOrderItemsByJobOrderID")]
        public async Task<ActionResult<List<JobOrderItems>>> GetLocalJobOrderItemsByJobOrderID(int jobOrderId)
        {

            if (jobOrderId <= 0)
            {
                return BadRequest("Invalid Job Order ID.");
            }


            if (!await GetProductionVisibleJobOrders()
                .AnyAsync(jobOrder => jobOrder.JobOrder_ID == jobOrderId))
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
            var jobOrder = await GetProductionVisibleJobOrders()
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

            if (!await GetProductionVisibleJobOrders()
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


            if (!await GetProductionVisibleJobOrders()
                .AnyAsync(jobOrder => jobOrder.JobOrder_ID == request.JobOrder_ID))
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


            if (!await GetProductionVisibleJobOrders()
                .AnyAsync(record => record.JobOrder_ID == jobOrderId))
            {
                return NotFound("Job Order not found.");
            }

            var updatedRows = await _Localdb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [dbo].[JobOrder] SET [Production_Completed] = 1, [Status] = N'Completed' WHERE [JobOrder_ID] = {jobOrderId} AND [Technical_Completed] = 1 AND ([Production_Completed] = 1 OR [Status] = N'In Progress (1/2)' OR [Status] = N'Completed')");

            if (updatedRows == 0)
            {

                if (!await GetProductionVisibleJobOrders()
                    .AnyAsync(record => record.JobOrder_ID == jobOrderId))
                {
                    return NotFound("Job Order not found.");
                }
                return Conflict("This Job Order is available to Production after Technical completes it.");
            }

            var jobOrder = await GetProductionVisibleJobOrders()
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

            var jobOrder = await GetProductionVisibleJobOrders()
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





        [HttpGet("GetLocalInstallationSchedule")]

        public List<Installation_Schedule> GetAllLocalInstallationSchedule()
        {
            return _Localdb.Installation_Schedule.ToList();
        }


        [HttpGet("GetLocalInstallationScheduleByID")]
        public ActionResult<Installation_Schedule> GetLocalInstallationScheduleID(int id)
        {

            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var installationSchedule = _Localdb.Installation_Schedule.FirstOrDefault(x => x.Installation_ID == id);

            if (installationSchedule == null)
            {
                return NotFound();
            }
            return Ok(installationSchedule);
        }


        [HttpPost("AddLocalInstallation_Schedule")]
        public ActionResult<Installation_Schedule> AddLocalInstallationSchedule([FromBody] Installation_Schedule installationSchedule)
        {

            if (!ModelState.IsValid || installationSchedule == null ||
                installationSchedule.JobOrder_ID <= 0 ||
                installationSchedule.Date == default ||
                string.IsNullOrWhiteSpace(installationSchedule.Mall_Hours))
            {
                return BadRequest("A valid job order, date, and mall-hours group are required.");
            }
            var jobOrder = GetProductionVisibleJobOrders()
                .FirstOrDefault(record => record.JobOrder_ID == installationSchedule.JobOrder_ID);

            if (jobOrder == null)
            {
                return BadRequest("The selected Job Order was not found.");
            }

            if (!IsCompletedJobOrder(jobOrder))
            {
                return BadRequest("Only completed Job Orders can be added to the Installation Schedule.");
            }

            installationSchedule.Installation_ID = 0;
            _Localdb.Installation_Schedule.Add(installationSchedule);
            jobOrder.Installation_Date = installationSchedule.Date;

            _Localdb.SaveChanges();
            return Ok(installationSchedule);
        }


        [HttpPost("UpdateLocalInstallation_Schedule")]
        public ActionResult<Installation_Schedule> UpdateLocalInstallationSchedule(int Id, [FromBody] Installation_Schedule installationSchedule)
        {

            if (Id <= 0 || installationSchedule == null ||
                installationSchedule.JobOrder_ID <= 0 ||
                installationSchedule.Date == default ||
                string.IsNullOrWhiteSpace(installationSchedule.Mall_Hours))
            {
                return BadRequest("A valid schedule row, job order, date, and mall-hours group are required.");
            }
            var existingSchedule = _Localdb.Installation_Schedule.FirstOrDefault(schedule => schedule.Installation_ID == Id);

            if (existingSchedule == null)
            {
                return NotFound();
            }

            var jobOrder = GetProductionVisibleJobOrders()
                .FirstOrDefault(record => record.JobOrder_ID == installationSchedule.JobOrder_ID);

            if (jobOrder == null)
            {
                return BadRequest("The selected Job Order was not found.");
            }

            if (!IsCompletedJobOrder(jobOrder))
            {
                return BadRequest("Only completed Job Orders can be added to the Installation Schedule.");
            }

            var previousJobOrderId = existingSchedule.JobOrder_ID;
            existingSchedule.Time = installationSchedule.Time;
            existingSchedule.Remarks = installationSchedule.Remarks;
            existingSchedule.Mall_Hours = installationSchedule.Mall_Hours;
            existingSchedule.Date = installationSchedule.Date;
            existingSchedule.JobOrder_ID = installationSchedule.JobOrder_ID;
            jobOrder.Installation_Date = installationSchedule.Date;


            if (previousJobOrderId != installationSchedule.JobOrder_ID)
            {
                var previousJobOrder = GetProductionVisibleJobOrders()
                    .FirstOrDefault(record => record.JobOrder_ID == previousJobOrderId);

                if (previousJobOrder != null)
                {
                    previousJobOrder.Installation_Date = _Localdb.Installation_Schedule

                        .Where(schedule => schedule.JobOrder_ID == previousJobOrderId && schedule.Installation_ID != Id)

                        .OrderBy(schedule => schedule.Installation_ID)
                        .Select(schedule => (DateOnly?)schedule.Date)
                        .FirstOrDefault();
                }
            }


            _Localdb.SaveChanges();
            return Ok(existingSchedule);
        }


        [HttpPut("DeleteLocalInstallationSchedule")]
        public async Task<IActionResult> DeleteLocalInstallationSchedule(int id)
        {

            if (id <= 0)
            {
                return BadRequest("A valid schedule row is required.");
            }

            var requestedBatchId = Request.Headers["X-Audit-Batch-Id"].FirstOrDefault();
            var batchId = Guid.TryParse(requestedBatchId, out var parsedBatchId)
                ? parsedBatchId.ToString("N")
                : Guid.NewGuid().ToString("N");
            Request.Headers["X-Audit-Batch-Id"] = batchId;

            var scheduleToDelete = await _Localdb.Installation_Schedule
                .AsNoTracking()
                .FirstOrDefaultAsync(schedule => schedule.Installation_ID == id);
            var associatedJobOrder = scheduleToDelete == null
                ? null
                : await GetProductionVisibleJobOrders()
                    .FirstOrDefaultAsync(record => record.JobOrder_ID == scheduleToDelete.JobOrder_ID);
            var conforme = associatedJobOrder == null
                ? null
                : await _Localdb.Conforme
                    .AsNoTracking()
                    .FirstOrDefaultAsync(record => record.FileID == associatedJobOrder.Conforme_FileID);
            var quotation = conforme == null
                ? null
                : await _Localdb.Quotation
                    .AsNoTracking()
                    .FirstOrDefaultAsync(record => record.Quotation_ID == conforme.Quotation_ID);
            var clientName = quotation == null
                ? null
                : await _Localdb.Clients
                    .AsNoTracking()

                    .Where(client => client.Client_ID == quotation.Client_ID)
                    .Select(client => client.Client_Name)
                    .FirstOrDefaultAsync();
            await using var transaction = await _Localdb.Database.BeginTransactionAsync();
            await _Localdb.Installation_Schedule_Employee

                .Where(assignment => assignment.Installation_ID == id)

                .ExecuteDeleteAsync();

            var deletedSchedules = await _Localdb.Installation_Schedule

                .Where(schedule => schedule.Installation_ID == id)

                .ExecuteDeleteAsync();

            if (deletedSchedules == 0)
            {
                await transaction.CommitAsync();
                return NoContent();
            }


            if (scheduleToDelete != null)
            {

                if (associatedJobOrder != null)
                {
                    associatedJobOrder.Installation_Date = await _Localdb.Installation_Schedule

                        .Where(schedule => schedule.JobOrder_ID == scheduleToDelete.JobOrder_ID)

                        .OrderBy(schedule => schedule.Installation_ID)
                        .Select(schedule => (DateOnly?)schedule.Date)
                        .FirstOrDefaultAsync();
                }
            }


            var now = DateTime.Now;
            var documentLabel = string.Join(" - ", new[]
                {
                    clientName,
                    quotation?.Client_Subject,
                    associatedJobOrder?.Title ?? quotation?.Title
                }

                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));

            if (string.IsNullOrWhiteSpace(documentLabel))
            {
                documentLabel = "Installation Schedule";
            }
            var auditDetails = new AuditTrailDetailsDTO
            {
                Entity = nameof(Installation_Schedule),
                RecordId = id.ToString(),
                Operation = "Deleted",
                BatchId = batchId,
                DocumentLabel = documentLabel,
                ActionDescription = "has been deleted",
                PreviousValues = scheduleToDelete == null
                    ? null
                    : new Dictionary<string, string?>
                    {
                        ["Date"] = scheduleToDelete.Date.ToString("yyyy-MM-dd"),
                        ["Time"] = scheduleToDelete.Time.ToString("HH:mm:ss"),
                        ["Remarks"] = scheduleToDelete.Remarks,
                        ["Mall_Hours"] = scheduleToDelete.Mall_Hours,
                        ["Project Title"] = associatedJobOrder?.Title ?? quotation?.Title
                    }
            };
            _Localdb.AuditTrail.Add(new AuditTrail
            {
                Name = _Localdb.GetCurrentEmployeeName(),
                Date = DateOnly.FromDateTime(now),
                Time = TimeOnly.FromDateTime(now),
                Action = "AUDIT_DETAILS_V1:" + JsonSerializer.Serialize(auditDetails),
                Department = "JobOrder"
            });

            await _Localdb.SaveChangesAsync();
            await transaction.CommitAsync();
            return NoContent();
        }


        [HttpGet("GetLocalInstallationScheduleEmployees")]
        public ActionResult<List<InstallationScheduleEmployeeOptionDto>> GetLocalInstallationScheduleEmployees()
        {
            var productionDepartmentId = GetProductionDepartmentId();

            if (!productionDepartmentId.HasValue)
            {
                return BadRequest("The Production department is not configured.");
            }

            var employees = _Localdb.Employee
                .AsNoTracking()

                .Where(employee =>
                    employee.Date_Resigned == null &&
                    employee.Department_ID == productionDepartmentId.Value)
                .Select(employee => new InstallationScheduleEmployeeOptionDto
                {
                    Employee_Id = employee.Employee_Id,
                    FirstName = employee.FirstName,
                    LastName = employee.LastName
                })
                .ToList()

                .OrderBy(employee => employee.LastName, StringComparer.OrdinalIgnoreCase)

                .ThenBy(employee => employee.FirstName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Ok(employees);
        }


        [HttpGet("GetLocalInstallationScheduleEmployeeAssignments")]
        public ActionResult<List<InstallationScheduleEmployeeAssignmentDto>> GetLocalInstallationScheduleEmployeeAssignments()
        {
            var assignments = (
                from assignment in _Localdb.Installation_Schedule_Employee.AsNoTracking()
                join employee in _Localdb.Employee.AsNoTracking()
                    on assignment.Employee_Id equals employee.Employee_Id
                select new InstallationScheduleEmployeeAssignmentDto
                {
                    Installation_ID = assignment.Installation_ID,
                    Employee_Id = employee.Employee_Id,
                    FirstName = employee.FirstName,
                    LastName = employee.LastName
                })
                .ToList()

                .OrderBy(assignment => assignment.Installation_ID)

                .ThenBy(assignment => assignment.LastName, StringComparer.OrdinalIgnoreCase)

                .ThenBy(assignment => assignment.FirstName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Ok(assignments);
        }


        [HttpPost("AddLocalInstallationScheduleEmployee")]
        public ActionResult<InstallationScheduleEmployeeAssignmentDto> AddLocalInstallationScheduleEmployee(
            [FromBody] AddInstallationScheduleEmployeeRequest request)
        {

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }


            if (!_Localdb.Installation_Schedule.Any(schedule => schedule.Installation_ID == request.Installation_ID))
            {
                return NotFound("The selected installation schedule row was not found.");
            }

            var productionDepartmentId = GetProductionDepartmentId();

            if (!productionDepartmentId.HasValue)
            {
                return BadRequest("The Production department is not configured.");
            }

            var employee = _Localdb.Employee.FirstOrDefault(candidate =>
                candidate.Employee_Id == request.Employee_Id &&
                candidate.Date_Resigned == null &&
                candidate.Department_ID == productionDepartmentId.Value);

            if (employee == null)
            {
                return NotFound("The selected active Production employee was not found.");
            }


            if (_Localdb.Installation_Schedule_Employee.Any(assignment =>
                    assignment.Installation_ID == request.Installation_ID &&
                    assignment.Employee_Id == request.Employee_Id))
            {
                return Conflict("This employee is already assigned to this schedule row.");
            }

            _Localdb.Installation_Schedule_Employee.Add(new InstallationScheduleEmployee
            {
                Installation_ID = request.Installation_ID,
                Employee_Id = employee.Employee_Id
            });

            _Localdb.SaveChanges();

            return Ok(new InstallationScheduleEmployeeAssignmentDto
            {
                Installation_ID = request.Installation_ID,
                Employee_Id = employee.Employee_Id,
                FirstName = employee.FirstName,
                LastName = employee.LastName
            });
        }


        [HttpDelete("DeleteLocalInstallationScheduleEmployee")]
        public IActionResult DeleteLocalInstallationScheduleEmployee(int installationId, int employeeId)
        {

            if (installationId <= 0 || employeeId <= 0)
            {
                return BadRequest("A valid schedule row and employee are required.");
            }

            var assignment = _Localdb.Installation_Schedule_Employee.FirstOrDefault(candidate =>
                candidate.Installation_ID == installationId &&
                candidate.Employee_Id == employeeId);

            if (assignment == null)
            {
                return NotFound("The employee is not assigned to this schedule row.");
            }

            _Localdb.Installation_Schedule_Employee.Remove(assignment);

            _Localdb.SaveChanges();
            return NoContent();
        }


        [HttpGet("GetCloudInstallationSchedule")]
        public List<Installation_Schedule> GetAllCloudInstallationSchedule()
        {
            return _CloudDb.Installation_Schedule.ToList();
        }


        [HttpGet("GetCloudInstallationScheduleByID")]
        public ActionResult<Installation_Schedule> GetCloudInstallationScheduleID(int id)
        {

            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var installationSchedule = _CloudDb.Installation_Schedule.FirstOrDefault(x => x.Installation_ID == id);

            if (installationSchedule == null)
            {
                return NotFound();
            }
            return Ok(installationSchedule);
        }


        [HttpPost("AddCloudInstallation_Schedule")]
        public ActionResult<Installation_Schedule> AddCloudInstallationSchedule([FromBody] Installation_Schedule installationSchedule)
        {

            if (!ModelState.IsValid || installationSchedule == null ||
                installationSchedule.JobOrder_ID <= 0 ||
                installationSchedule.Date == default ||
                string.IsNullOrWhiteSpace(installationSchedule.Mall_Hours))
            {
                return BadRequest("A valid job order, date, and mall-hours group are required.");
            }

            if (!_CloudDb.JobOrder.IgnoreQueryFilters().Any(jobOrder => jobOrder.JobOrder_ID == installationSchedule.JobOrder_ID))
            {
                return BadRequest("The selected Job Order was not found.");
            }

            installationSchedule.Installation_ID = 0;
            _CloudDb.Installation_Schedule.Add(installationSchedule);

            _CloudDb.SaveChanges();
            return Ok(installationSchedule);
        }


        [HttpPost("UpdateCloudInstallation_Schedule")]
        public ActionResult<Installation_Schedule> UpdateCloudInstallationSchedule(int Id, [FromBody] Installation_Schedule installationSchedule)
        {

            if (Id <= 0 || installationSchedule == null ||
                installationSchedule.JobOrder_ID <= 0 ||
                installationSchedule.Date == default ||
                string.IsNullOrWhiteSpace(installationSchedule.Mall_Hours))
            {
                return BadRequest("A valid schedule row, job order, date, and mall-hours group are required.");
            }

            if (!_CloudDb.JobOrder.IgnoreQueryFilters().Any(jobOrder => jobOrder.JobOrder_ID == installationSchedule.JobOrder_ID))
            {
                return BadRequest("The selected Job Order was not found.");
            }

            var existingSchedule = _CloudDb.Installation_Schedule.FirstOrDefault(schedule => schedule.Installation_ID == Id);

            if (existingSchedule == null)
            {
                return NotFound();
            }

            existingSchedule.Time = installationSchedule.Time;
            existingSchedule.Remarks = installationSchedule.Remarks;
            existingSchedule.Mall_Hours = installationSchedule.Mall_Hours;
            existingSchedule.Date = installationSchedule.Date;
            existingSchedule.JobOrder_ID = installationSchedule.JobOrder_ID;

            _CloudDb.SaveChanges();
            return Ok(existingSchedule);
        }


        [HttpPut("DeleteCloudInstallationSchedule")]
        public IActionResult DeleteCloudInstallationSchedule(int id)
        {

            if (id <= 0)
            {
                return BadRequest("A valid schedule row is required.");
            }

            var installationSchedule = _CloudDb.Installation_Schedule.FirstOrDefault(schedule => schedule.Installation_ID == id);

            if (installationSchedule == null)
            {
                return NotFound();
            }

            _CloudDb.Installation_Schedule.Remove(installationSchedule);

            _CloudDb.SaveChanges();
            return NoContent();
        }





        [HttpGet("GetMaterialRequisitionInventory")]

        public async Task<IActionResult> GetMaterialRequisitionInventory(
            CancellationToken cancellationToken)
        {
            var inventory = await MaterialRequisitionLookupQueries.GetInventoryOptionsAsync(
                _Localdb,
                cancellationToken);
            return Ok(inventory);
        }


        [HttpGet("GetMaterialRequisitionPurchaseReceipts")]
        public async Task<IActionResult> GetMaterialRequisitionPurchaseReceipts(
            int materialId,
            CancellationToken cancellationToken)
        {

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


        [HttpPost("PreviewMaterialRequisition")]
        public ActionResult PreviewMaterialRequisition([FromBody] RequisitionPreviewDto dto)
        {
            var result = MaterialRequisitionWorkflowService.Preview(_Localdb, dto, "Production");
            return result.Error != null ? BadRequest(result.Error) : Ok(result.Response);
        }


        [HttpPost("AddMaterialRequisitionBatch")]
        public async Task<ActionResult> AddMaterialRequisitionBatch(
            [FromBody] List<MaterialRequesition_Slip> slips,
            CancellationToken cancellationToken)
        {

            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var submitterUserId))
            {
                return Unauthorized("Your session is invalid. Please sign in again.");
            }

            var result = await MaterialRequisitionWorkflowService.SubmitBatchAsync(
                _Localdb,
                _notificationService,
                slips,
                submitterUserId,
                "Production",
                cancellationToken);
            return result.Error != null
                ? BadRequest(result.Error)
                : Ok(new { saved = result.Saved });
        }


        [HttpGet("GetLocalMaterialRequesitionSlips")]
        public List<MaterialRequesition_Slip> GetAllLocalMaterialRequesitionSlips()
        {
            return _Localdb.MaterialRequisition_Slip
                .AsNoTracking()

                .Where(slip => slip.ToWho != null && slip.ToWho.Trim().ToLower() == "production")
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



        [HttpGet("GetLocalQuotations")]

        public List<Quotation> GetAllLocalQuotations()
        {
            return _Localdb.Quotation.ToList();
        }


        [HttpGet("GetLocalConforme")]
        public List<Conforme> GetAllLocalConforme()
        {
            var conforme = _Localdb.Conforme.ToList();


            foreach (var _conforme in conforme)
            {
                _conforme.FIleData = _encryptionService.Decrypt(_conforme.FIleData);

            }

            return conforme;


        }


        [HttpGet("GetAllLocalClients")]
        public List<ClientEntity> GetAllLocalClients()
        {
            return _Localdb.Clients.ToList();
        }



    }
}
