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
    // Coordinates the production controller API endpoints, including request handling and response generation.
    public class ProductionController : ControllerBase
    {
        // Injected or cached localdb dependency/state used by this class.
        private LocalDbContext _Localdb;
        // Injected or cached cloud db dependency/state used by this class.
        private CloudDbContext _CloudDb;
        // Encryption service used to protect sensitive persisted values.
        private readonly EncryptionService _encryptionService;
        // Injected or cached notification service dependency/state used by this class.
        private readonly NotificationService _notificationService;

        // Initializes a new production controller instance.
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

        // Retrieves the production department id.
        private int? GetProductionDepartmentId()
        {
            return _Localdb.Departments
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(department =>
                    department.Department_Name.ToLower().Contains("production") ||
                    department.Department_Name.ToLower().Contains("joborder") ||
                    department.Department_Name.ToLower().Contains("job order"))
                .Select(department => (int?)department.Department_ID)
                .FirstOrDefault();
        }

        // The Production policy protects these shared workflow records from creator-based scoping.
        private IQueryable<JobOrder> GetProductionVisibleJobOrders()
        {
            return _Localdb.JobOrder.IgnoreQueryFilters();
        }

        // Checks whether the job order is in a completed workflow state.
        private static bool IsCompletedJobOrder(JobOrder jobOrder)
        {
            return jobOrder.Production_Completed ||
                string.Equals(jobOrder.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(jobOrder.Status, "Complete", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(jobOrder.Status, "Complited", StringComparison.OrdinalIgnoreCase);
        }

        // Checks whether the current user has the role required to approve production work.
        private bool IsProductionApprover()
        {
            return _Localdb.HasGlobalRecordAccess ||
                (_Localdb.IsDepartmentManager &&
                    ModuleAccess.GetModuleForDepartment(_Localdb.CurrentDepartmentName) == "production");
        }

        /// <summary>Retrieves the installation report job orders.</summary>
        [HttpGet("GetInstallationReportJobOrders")]
        public async Task<ActionResult<IReadOnlyList<InstallationReportJobOrderOption>>> GetInstallationReportJobOrders(
            CancellationToken cancellationToken)
        {
            var jobOrders = await GetProductionVisibleJobOrders()
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(jobOrder =>
                    jobOrder.Production_Completed ||
                    jobOrder.Status == "Completed" ||
                    jobOrder.Status == "Complete" ||
                    jobOrder.Status == "complited")
                // Apply the ordering expression here before returning or materializing the results.
                .OrderByDescending(jobOrder => jobOrder.JobOrder_ID)
                .ToListAsync(cancellationToken);
            var existingReportJobOrderIds = await _Localdb.InstallationReport
                .AsNoTracking()
                .Select(report => report.JobOrder_ID)
                .Distinct()
                .ToListAsync(cancellationToken);
            jobOrders = jobOrders
                // Apply this predicate in the database query before materializing results.
                .Where(jobOrder => !existingReportJobOrderIds.Contains(jobOrder.JobOrder_ID))
                .ToList();

            // Exit the current operation when `jobOrders.Count == 0` is true.
            if (jobOrders.Count == 0)
            {
                return Ok(Array.Empty<InstallationReportJobOrderOption>());
            }

            var conformeIds = jobOrders.Select(jobOrder => jobOrder.Conforme_FileID).Distinct().ToArray();
            var conformes = await _Localdb.Conforme
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(conforme => conformeIds.Contains(conforme.FileID))
                .Select(conforme => new { conforme.FileID, conforme.Quotation_ID })
                .ToDictionaryAsync(conforme => conforme.FileID, cancellationToken);

            var quotationIds = conformes.Values.Select(conforme => conforme.Quotation_ID).Distinct().ToArray();
            var quotations = await _Localdb.Quotation
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(quotation => quotationIds.Contains(quotation.Quotation_ID))
                .Select(quotation => new { quotation.Quotation_ID, quotation.Client_ID, quotation.Title })
                .ToDictionaryAsync(quotation => quotation.Quotation_ID, cancellationToken);

            var clientIds = quotations.Values.Select(quotation => quotation.Client_ID).Distinct().ToArray();
            var clients = await _Localdb.Clients
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(client => clientIds.Contains(client.Client_ID))
                .Select(client => new { client.Client_ID, client.Client_Name, client.Company_Address })
                .ToDictionaryAsync(client => client.Client_ID, cancellationToken);

            var options = jobOrders.Select(jobOrder =>
            {
                // Combines each job order with its client and quotation details for the response.
                string? clientName = null;
                string? location = null;
                string? quotationTitle = null;

                // Enter this branch when `conformes.TryGetValue(jobOrder.Conforme_FileID, out var conforme) && quotations.TryGetValue(conforme.Quotation_ID, out var quotation)` is true.
                if (conformes.TryGetValue(jobOrder.Conforme_FileID, out var conforme)
                    && quotations.TryGetValue(conforme.Quotation_ID, out var quotation))
                {
                    quotationTitle = quotation.Title;
                    // Enter this branch when `clients.TryGetValue(quotation.Client_ID, out var client)` is true.
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

        /// <summary>Adds the local installation report.</summary>
        [HttpPost("AddLocalInstallationReport")]
        [RequestSizeLimit(10_000_000)]
        public async Task<ActionResult> AddLocalInstallationReport(
            [FromBody] CreateInstallationReportRequest request,
            CancellationToken cancellationToken)
        {
            // Exit the current operation when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var jobOrderExists = await _Localdb.JobOrder
                .AsNoTracking()
                .AnyAsync(jobOrder => jobOrder.JobOrder_ID == request.JobOrderId, cancellationToken);
            // Return HTTP 404 Not Found when `!jobOrderExists` is true.
            if (!jobOrderExists)
            {
                return NotFound("The selected Job Order was not found.");
            }
            // Return HTTP 409 Conflict when `await _Localdb.InstallationReport .AsNoTracking() .AnyAsync(report => report.JobOrder_ID == request.JobOrderId, cancellationToken)` is true.
            if (await _Localdb.InstallationReport
                .AsNoTracking()
                .AnyAsync(report => report.JobOrder_ID == request.JobOrderId, cancellationToken))
            {
                return Conflict("An Installation Report already exists for the selected Job Order.");
            }

            // Return HTTP 400 Bad Request when `request.Signatures == null || request.Signatures.Count == 0 || request.Signatures.Count > 2` is true.
            if (request.Signatures == null || request.Signatures.Count == 0 || request.Signatures.Count > 2)
            {
                return BadRequest("The prepared-by user signature is required, and no more than two sign-off images may be submitted.");
            }

            var signatureRoles = new HashSet<string>(StringComparer.Ordinal);
            var signatureFiles = new List<InstallationReportFile>();
            // Process each `signature` returned by the collection expression in the loop header.
            foreach (var signature in request.Signatures)
            {
                // Return HTTP 400 Bad Request when `signature == null` is true.
                if (signature == null)
                {
                    return BadRequest("An Installation Report signature entry is missing.");
                }
                // Return HTTP 400 Bad Request when `!signatureRoles.Add(signature.FileRole)` is true.
                if (!signatureRoles.Add(signature.FileRole))
                {
                    return BadRequest($"Only one {signature.FileRole} signature image may be submitted.");
                }

                var extension = signature.FileExtension.Trim().ToLowerInvariant();
                // Return HTTP 400 Bad Request when `extension is not and not and not` is true.
                if (extension is not ".png" and not ".jpg" and not ".jpeg")
                {
                    return BadRequest("Installation Report sign-off files must be JPG or PNG images.");
                }

                var validationError = UploadValidator.ValidateImage(signature.FileData);
                // Return HTTP 400 Bad Request when `validationError != null` is true.
                if (validationError != null)
                {
                    return BadRequest(validationError);
                }

                var fileData = signature.FileData;
                // Enter this branch when `fileData.StartsWith(, StringComparison.OrdinalIgnoreCase)` is true.
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
                // Handle `FormatException` raised by the preceding operation.
                catch (FormatException)
                {
                    return BadRequest("An Installation Report signature image contains invalid file data.");
                }

                var fileName = Path.GetFileName(signature.FileName.Trim());
                // Return HTTP 400 Bad Request when `string.IsNullOrWhiteSpace(fileName)` is true.
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

            // Return HTTP 400 Bad Request when `!signatureRoles.Contains()` is true.
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
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync(cancellationToken);
            // Process each `signatureFile` returned by the collection expression in the loop header.
            foreach (var signatureFile in signatureFiles)
            {
                signatureFile.InstallationReport_ID = report.InstallationReport_ID;
            }
            await _Localdb.InstallationReportFiles.AddRangeAsync(signatureFiles, cancellationToken);
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new
            {
                installationReportId = report.InstallationReport_ID,
                jobOrderId = report.JobOrder_ID
            });
        }

        /// <summary>Approves the installation report after validating the required workflow state.</summary>
        [HttpPost("ApproveInstallationReport/{id:int}")]
        public async Task<ActionResult> ApproveInstallationReport(
            int id,
            CancellationToken cancellationToken)
        {
            // Return HTTP 403 Forbidden when `!IsProductionApprover()` is true.
            if (!IsProductionApprover())
            {
                return Forbid();
            }

            // Return HTTP 401 Unauthorized when `!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var approverUserId)` is true.
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
            // Return HTTP 404 Not Found when `report == null` is true.
            if (report == null)
            {
                return NotFound("The Installation Report was not found.");
            }

            var preparedByUserId = await _Localdb.InstallationReportFiles
                .IgnoreQueryFilters()
                // Apply this predicate in the database query before materializing results.
                .Where(file =>
                    file.InstallationReport_ID == id &&
                    file.File_Role == "PreparedBy")
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(file => file.InstallationReportFile_ID)
                .Select(file => file.CreatedByUserId)
                .FirstOrDefaultAsync(cancellationToken);
            // Return HTTP 400 Bad Request when `!preparedByUserId.HasValue` is true.
            if (!preparedByUserId.HasValue)
            {
                return BadRequest("This Installation Report must have a preparer's signature before approval.");
            }
            // Return HTTP 400 Bad Request when `preparedByUserId.Value == approverUserId` is true.
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
            // Return HTTP 409 Conflict when `existingApproval` is true.
            if (existingApproval)
            {
                return Conflict("This Installation Report has already been approved.");
            }

            var approverSignature = await _Localdb.UserSignatures
                .AsNoTracking()
                .SingleOrDefaultAsync(signature => signature.User_ID == approverUserId, cancellationToken);
            // Return HTTP 400 Bad Request when `approverSignature == null || approverSignature.SignatureData.Length == 0` is true.
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
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(new { message = "Installation Report approved and signed." });
        }

        /// <summary>Retrieves the delivery receipt approval queue.</summary>
        [HttpGet("GetDeliveryReceiptApprovalQueue")]
        public async Task<ActionResult<List<DeliveryReceipt>>> GetDeliveryReceiptApprovalQueue(
            CancellationToken cancellationToken)
        {
            var receipts = await _Localdb.DeliveryReceipt
                .IgnoreQueryFilters()
                .AsNoTracking()
                // Apply the ordering expression here before returning or materializing the results.
                .OrderByDescending(receipt => receipt.Delivery_ID)
                .ToListAsync(cancellationToken);
            var deliveryIds = receipts.Select(receipt => receipt.Delivery_ID).ToArray();
            var files = deliveryIds.Length == 0
                ? new List<DeliveryFiles>()
                : await _Localdb.DeliveryFiles
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(file => deliveryIds.Contains(file.Delivery_ID))
                    .ToListAsync(cancellationToken);
            // Process each `file` returned by the collection expression in the loop header.
            foreach (var file in files)
            {
                file.File_Data = _encryptionService.TryDecrypt(file.File_Data);
            }
            // Process each `receipt` returned by the collection expression in the loop header.
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

        /// <summary>Retrieves the delivery receipt approval items.</summary>
        [HttpGet("GetDeliveryReceiptApprovalItems")]
        public async Task<ActionResult<List<DeliveryReceiptItems>>> GetDeliveryReceiptApprovalItems(
            CancellationToken cancellationToken)
        {
            var items = await _Localdb.DeliveryReceiptItems
                .IgnoreQueryFilters()
                .AsNoTracking()
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(item => item.Delivery_ID)
                // Apply the ordering expression here before returning or materializing the results.
                .ThenBy(item => item.DeliveryReceiptItems_ID)
                .ToListAsync(cancellationToken);
            return Ok(items);
        }

        /// <summary>Retrieves the delivery receipt approval signatures.</summary>
        [HttpGet("GetDeliveryReceiptApprovalSignatures")]
        public async Task<ActionResult<List<DeliveryFiles>>> GetDeliveryReceiptApprovalSignatures(
            CancellationToken cancellationToken)
        {
            var files = await _Localdb.DeliveryFiles
                .IgnoreQueryFilters()
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(file => file.File_Role == "ApproverSigned" || file.File_Role == "PreparedBy")
                .ToListAsync(cancellationToken);
            var preparedByUsers = files
                // Apply this predicate in the database query before materializing results.
                .Where(file => string.Equals(file.File_Role, "PreparedBy", StringComparison.OrdinalIgnoreCase))
                .GroupBy(file => file.Delivery_ID)
                .ToDictionary(
                    group => group.Key,
                    // Apply the ordering expression here before returning or materializing the results.
                    group => group.OrderBy(file => file.File_ID)
                        .Select(file => file.CreatedByUserId)
                        .FirstOrDefault());
            var approvals = files
                // Apply this predicate in the database query before materializing results.
                .Where(file =>
                    string.Equals(file.File_Role, "ApproverSigned", StringComparison.OrdinalIgnoreCase) &&
                    preparedByUsers.TryGetValue(file.Delivery_ID, out var preparedByUserId) &&
                    preparedByUserId.HasValue &&
                    file.CreatedByUserId.HasValue &&
                    preparedByUserId.Value != file.CreatedByUserId.Value)
                .ToList();
            // Process each `file` returned by the collection expression in the loop header.
            foreach (var file in approvals)
            {
                file.File_Data = _encryptionService.TryDecrypt(file.File_Data);
            }
            return Ok(approvals);
        }

        /// <summary>Approves the specified delivery receipt and advances its workflow.</summary>
        [HttpPost("ApproveDeliveryReceipt/{id:int}")]
        public Task<ActionResult> ApproveDeliveryReceipt(int id, CancellationToken cancellationToken) =>
            SetDeliveryReceiptApprovalStatus(id, "Approved", cancellationToken);

        /// <summary>Rejects the specified delivery receipt with the supplied reason.</summary>
        [HttpPost("RejectDeliveryReceipt/{id:int}")]
        public Task<ActionResult> RejectDeliveryReceipt(int id, CancellationToken cancellationToken) =>
            SetDeliveryReceiptApprovalStatus(id, "Rejected", cancellationToken);

        // Sets the delivery receipt approval status.
        private async Task<ActionResult> SetDeliveryReceiptApprovalStatus(
            int id,
            string status,
            CancellationToken cancellationToken)
        {
            // Return HTTP 401 Unauthorized when `!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var approverUserId)` is true.
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
            // Return HTTP 404 Not Found when `receipt == null` is true.
            if (receipt == null)
            {
                return NotFound("The Delivery Receipt was not found.");
            }

            var files = await _Localdb.DeliveryFiles
                .IgnoreQueryFilters()
                // Apply this predicate in the database query before materializing results.
                .Where(file => file.Delivery_ID == id)
                .ToListAsync(cancellationToken);
            var currentStatus = WorkflowRules.GetDeliveryReceiptApprovalStatus(
                receipt.Status,
                files,
                id);
            // Return HTTP 409 Conflict when `currentStatus !=` is true.
            if (currentStatus != "Request")
            {
                return Conflict($"This Delivery Receipt has already been {currentStatus.ToLowerInvariant()}.");
            }

            var preparedByUserId = files
                // Apply this predicate in the database query before materializing results.
                .Where(file => string.Equals(file.File_Role, "PreparedBy", StringComparison.OrdinalIgnoreCase))
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(file => file.File_ID)
                .Select(file => file.CreatedByUserId)
                .FirstOrDefault();
            // Return HTTP 400 Bad Request when `!preparedByUserId.HasValue` is true.
            if (!preparedByUserId.HasValue)
            {
                return BadRequest("This Delivery Receipt must have a preparer's signature before review.");
            }
            // Return HTTP 400 Bad Request when `preparedByUserId.Value == approverUserId` is true.
            if (preparedByUserId.Value == approverUserId)
            {
                return BadRequest("A different authorized user must review this Delivery Receipt.");
            }

            // Enter this branch when `status ==` is true.
            if (status == "Approved")
            {
                var approverSignature = await _Localdb.UserSignatures
                    .AsNoTracking()
                    .SingleOrDefaultAsync(signature => signature.User_ID == approverUserId, cancellationToken);
                // Return HTTP 400 Bad Request when `approverSignature == null || approverSignature.SignatureData.Length == 0` is true.
                if (approverSignature == null || approverSignature.SignatureData.Length == 0)
                {
                    return BadRequest("Add a signature in Profile Settings before approving a Delivery Receipt.");
                }

                var legacyApprovals = await _Localdb.DeliveryFiles
                    .IgnoreQueryFilters()
                    // Apply this predicate in the database query before materializing results.
                    .Where(file => file.Delivery_ID == id && file.File_Role == "ApprovedBy")
                    .ToListAsync(cancellationToken);
                var priorApprovals = files
                    // Apply this predicate in the database query before materializing results.
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
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(new { message = $"Delivery Receipt {status.ToLowerInvariant()}." });
        }

        // ----------------------------------- JOB ORDER -----------------------------------
        [HttpGet("GetLocalJobOrder")]
        // Retrieves all job order. Returns List<JobOrder> asynchronously.
        public async Task<List<JobOrder>> GetAllJobOrder()
        {
            var jobOrders = await GetProductionVisibleJobOrders()
                .AsNoTracking()
                .ToListAsync(HttpContext.RequestAborted);
            await JobOrderClientQueries.PopulateAsync(_Localdb, jobOrders, HttpContext.RequestAborted);
            return jobOrders;
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

            // Return HTTP 404 Not Found when `!await GetProductionVisibleJobOrders() .AnyAsync(jobOrder => jobOrder.JobOrder_ID == jobOrderId)` is true.
            if (!await GetProductionVisibleJobOrders()
                .AnyAsync(jobOrder => jobOrder.JobOrder_ID == jobOrderId))
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
            var jobOrder = await GetProductionVisibleJobOrders()
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
            // Return HTTP 404 Not Found when `!await GetProductionVisibleJobOrders() .AnyAsync(jobOrder => jobOrder.JobOrder_ID == jobOrderId, cancellationToken)` is true.
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

            // Return HTTP 404 Not Found when `!await GetProductionVisibleJobOrders() .AnyAsync(jobOrder => jobOrder.JobOrder_ID == request.JobOrder_ID)` is true.
            if (!await GetProductionVisibleJobOrders()
                .AnyAsync(jobOrder => jobOrder.JobOrder_ID == request.JobOrder_ID))
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

            // Return HTTP 404 Not Found when `!await GetProductionVisibleJobOrders() .AnyAsync(record => record.JobOrder_ID == jobOrderId)` is true.
            if (!await GetProductionVisibleJobOrders()
                .AnyAsync(record => record.JobOrder_ID == jobOrderId))
            {
                return NotFound("Job Order not found.");
            }

            var updatedRows = await _Localdb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [dbo].[JobOrder] SET [Production_Completed] = 1, [Status] = N'Completed' WHERE [JobOrder_ID] = {jobOrderId} AND [Technical_Completed] = 1 AND ([Production_Completed] = 1 OR [Status] = N'In Progress (1/2)' OR [Status] = N'Completed')");
            // Enter this branch when `updatedRows == 0` is true.
            if (updatedRows == 0)
            {
                // Return HTTP 404 Not Found when `!await GetProductionVisibleJobOrders() .AnyAsync(record => record.JobOrder_ID == jobOrderId)` is true.
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

        // ----------------------------------- JOB ORDER -----------------------------------

        // ----------------------------------- GRAPHICS -----------------------------------

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

            var jobOrder = await GetProductionVisibleJobOrders()
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

        // ----------------------------------- GRAPHICS -----------------------------------

        // ----------------------------------- INSTALLATION SCHEDULE -----------------------------------

        [HttpGet("GetLocalInstallationSchedule")]
        // Retrieves all local installation schedule. Returns List<Installation_Schedule>.
        public List<Installation_Schedule> GetAllLocalInstallationSchedule()
        {
            return _Localdb.Installation_Schedule.ToList();
        }

        /// <summary>Retrieves the local installation schedule id.</summary>
        [HttpGet("GetLocalInstallationScheduleByID")]
        public ActionResult<Installation_Schedule> GetLocalInstallationScheduleID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var installationSchedule = _Localdb.Installation_Schedule.FirstOrDefault(x => x.Installation_ID == id);
            // Return HTTP 404 Not Found when `installationSchedule == null` is true.
            if (installationSchedule == null)
            {
                return NotFound();
            }
            return Ok(installationSchedule);
        }

        /// <summary>Adds the local installation schedule.</summary>
        [HttpPost("AddLocalInstallation_Schedule")]
        public ActionResult<Installation_Schedule> AddLocalInstallationSchedule([FromBody] Installation_Schedule installationSchedule)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid || installationSchedule == null || installationSchedule.JobOrder_ID <= 0 || installationSchedule.Date == default || string.IsNullOrWhiteSpace(installationSchedule.Mall_Hours)` is true.
            if (!ModelState.IsValid || installationSchedule == null ||
                installationSchedule.JobOrder_ID <= 0 ||
                installationSchedule.Date == default ||
                string.IsNullOrWhiteSpace(installationSchedule.Mall_Hours))
            {
                return BadRequest("A valid job order, date, and mall-hours group are required.");
            }
            var jobOrder = GetProductionVisibleJobOrders()
                .FirstOrDefault(record => record.JobOrder_ID == installationSchedule.JobOrder_ID);
            // Return HTTP 400 Bad Request when `jobOrder == null` is true.
            if (jobOrder == null)
            {
                return BadRequest("The selected Job Order was not found.");
            }
            // Return HTTP 400 Bad Request when `!IsCompletedJobOrder(jobOrder)` is true.
            if (!IsCompletedJobOrder(jobOrder))
            {
                return BadRequest("Only completed Job Orders can be added to the Installation Schedule.");
            }

            installationSchedule.Installation_ID = 0;
            _Localdb.Installation_Schedule.Add(installationSchedule);
            jobOrder.Installation_Date = installationSchedule.Date;
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(installationSchedule);
        }

        /// <summary>Updates the local installation schedule.</summary>
        [HttpPost("UpdateLocalInstallation_Schedule")]
        public ActionResult<Installation_Schedule> UpdateLocalInstallationSchedule(int Id, [FromBody] Installation_Schedule installationSchedule)
        {
            // Return HTTP 400 Bad Request when `Id <= 0 || installationSchedule == null || installationSchedule.JobOrder_ID <= 0 || installationSchedule.Date == default || string.IsNullOrWhiteSpace(installationSchedule.Mall_Hours)` is true.
            if (Id <= 0 || installationSchedule == null ||
                installationSchedule.JobOrder_ID <= 0 ||
                installationSchedule.Date == default ||
                string.IsNullOrWhiteSpace(installationSchedule.Mall_Hours))
            {
                return BadRequest("A valid schedule row, job order, date, and mall-hours group are required.");
            }
            var existingSchedule = _Localdb.Installation_Schedule.FirstOrDefault(schedule => schedule.Installation_ID == Id);
            // Return HTTP 404 Not Found when `existingSchedule == null` is true.
            if (existingSchedule == null)
            {
                return NotFound();
            }

            var jobOrder = GetProductionVisibleJobOrders()
                .FirstOrDefault(record => record.JobOrder_ID == installationSchedule.JobOrder_ID);
            // Return HTTP 400 Bad Request when `jobOrder == null` is true.
            if (jobOrder == null)
            {
                return BadRequest("The selected Job Order was not found.");
            }
            // Return HTTP 400 Bad Request when `!IsCompletedJobOrder(jobOrder)` is true.
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

            // Enter this branch when `previousJobOrderId != installationSchedule.JobOrder_ID` is true.
            if (previousJobOrderId != installationSchedule.JobOrder_ID)
            {
                var previousJobOrder = GetProductionVisibleJobOrders()
                    .FirstOrDefault(record => record.JobOrder_ID == previousJobOrderId);
                // Enter this branch when `previousJobOrder != null` is true.
                if (previousJobOrder != null)
                {
                    previousJobOrder.Installation_Date = _Localdb.Installation_Schedule
                        // Apply this predicate in the database query before materializing results.
                        .Where(schedule => schedule.JobOrder_ID == previousJobOrderId && schedule.Installation_ID != Id)
                        // Apply the ordering expression here before returning or materializing the results.
                        .OrderBy(schedule => schedule.Installation_ID)
                        .Select(schedule => (DateOnly?)schedule.Date)
                        .FirstOrDefault();
                }
            }

            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(existingSchedule);
        }

        /// <summary>Deletes the local installation schedule.</summary>
        [HttpPut("DeleteLocalInstallationSchedule")]
        public async Task<IActionResult> DeleteLocalInstallationSchedule(int id)
        {
            // Return HTTP 400 Bad Request when `id <= 0` is true.
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
                    // Apply this predicate in the database query before materializing results.
                    .Where(client => client.Client_ID == quotation.Client_ID)
                    .Select(client => client.Client_Name)
                    .FirstOrDefaultAsync();
            await using var transaction = await _Localdb.Database.BeginTransactionAsync();
            await _Localdb.Installation_Schedule_Employee
                // Apply this predicate in the database query before materializing results.
                .Where(assignment => assignment.Installation_ID == id)
                // Delete matching rows in the database without loading them into application memory.
                .ExecuteDeleteAsync();

            var deletedSchedules = await _Localdb.Installation_Schedule
                // Apply this predicate in the database query before materializing results.
                .Where(schedule => schedule.Installation_ID == id)
                // Delete matching rows in the database without loading them into application memory.
                .ExecuteDeleteAsync();
            // Enter this branch when `deletedSchedules == 0` is true.
            if (deletedSchedules == 0)
            {
                await transaction.CommitAsync();
                return NoContent();
            }

            // Enter this branch when `scheduleToDelete != null` is true.
            if (scheduleToDelete != null)
            {
                // Enter this branch when `associatedJobOrder != null` is true.
                if (associatedJobOrder != null)
                {
                    associatedJobOrder.Installation_Date = await _Localdb.Installation_Schedule
                        // Apply this predicate in the database query before materializing results.
                        .Where(schedule => schedule.JobOrder_ID == scheduleToDelete.JobOrder_ID)
                        // Apply the ordering expression here before returning or materializing the results.
                        .OrderBy(schedule => schedule.Installation_ID)
                        .Select(schedule => (DateOnly?)schedule.Date)
                        .FirstOrDefaultAsync();
                }
            }

            // Capture one UTC instant so the timestamps written by this operation remain consistent.
            var now = DateTime.Now;
            var documentLabel = string.Join(" - ", new[]
                {
                    clientName,
                    quotation?.Client_Subject,
                    associatedJobOrder?.Title ?? quotation?.Title
                }
                // Filter the query to rows satisfying `.Where(value => !string.IsNullOrWhiteSpace(value)) .Select(value => value!.Trim())` before materializing the results.
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
            // Enter this branch when `string.IsNullOrWhiteSpace(documentLabel)` is true.
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
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            await transaction.CommitAsync();
            return NoContent();
        }

        /// <summary>Retrieves the local installation schedule employees.</summary>
        [HttpGet("GetLocalInstallationScheduleEmployees")]
        public ActionResult<List<InstallationScheduleEmployeeOptionDto>> GetLocalInstallationScheduleEmployees()
        {
            var productionDepartmentId = GetProductionDepartmentId();
            // Return HTTP 400 Bad Request when `!productionDepartmentId.HasValue` is true.
            if (!productionDepartmentId.HasValue)
            {
                return BadRequest("The Production department is not configured.");
            }

            var employees = _Localdb.Employee
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
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
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(employee => employee.LastName, StringComparer.OrdinalIgnoreCase)
                // Apply the ordering expression here before returning or materializing the results.
                .ThenBy(employee => employee.FirstName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Ok(employees);
        }

        /// <summary>Retrieves the local installation schedule employee assignments.</summary>
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
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(assignment => assignment.Installation_ID)
                // Apply the ordering expression here before returning or materializing the results.
                .ThenBy(assignment => assignment.LastName, StringComparer.OrdinalIgnoreCase)
                // Apply the ordering expression here before returning or materializing the results.
                .ThenBy(assignment => assignment.FirstName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Ok(assignments);
        }

        /// <summary>Adds the local installation schedule employee.</summary>
        [HttpPost("AddLocalInstallationScheduleEmployee")]
        public ActionResult<InstallationScheduleEmployeeAssignmentDto> AddLocalInstallationScheduleEmployee(
            [FromBody] AddInstallationScheduleEmployeeRequest request)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Return HTTP 404 Not Found when `!_Localdb.Installation_Schedule.Any(schedule => schedule.Installation_ID == request.Installation_ID)` is true.
            if (!_Localdb.Installation_Schedule.Any(schedule => schedule.Installation_ID == request.Installation_ID))
            {
                return NotFound("The selected installation schedule row was not found.");
            }

            var productionDepartmentId = GetProductionDepartmentId();
            // Return HTTP 400 Bad Request when `!productionDepartmentId.HasValue` is true.
            if (!productionDepartmentId.HasValue)
            {
                return BadRequest("The Production department is not configured.");
            }

            var employee = _Localdb.Employee.FirstOrDefault(candidate =>
                candidate.Employee_Id == request.Employee_Id &&
                candidate.Date_Resigned == null &&
                candidate.Department_ID == productionDepartmentId.Value);
            // Return HTTP 404 Not Found when `employee == null` is true.
            if (employee == null)
            {
                return NotFound("The selected active Production employee was not found.");
            }

            // Return HTTP 409 Conflict when `_Localdb.Installation_Schedule_Employee.Any(assignment => assignment.Installation_ID == request.Installation_ID && assignment.Employee_Id == request.Employee_Id)` is true.
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
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();

            return Ok(new InstallationScheduleEmployeeAssignmentDto
            {
                Installation_ID = request.Installation_ID,
                Employee_Id = employee.Employee_Id,
                FirstName = employee.FirstName,
                LastName = employee.LastName
            });
        }

        /// <summary>Deletes the local installation schedule employee.</summary>
        [HttpDelete("DeleteLocalInstallationScheduleEmployee")]
        public IActionResult DeleteLocalInstallationScheduleEmployee(int installationId, int employeeId)
        {
            // Return HTTP 400 Bad Request when `installationId <= 0 || employeeId <= 0` is true.
            if (installationId <= 0 || employeeId <= 0)
            {
                return BadRequest("A valid schedule row and employee are required.");
            }

            var assignment = _Localdb.Installation_Schedule_Employee.FirstOrDefault(candidate =>
                candidate.Installation_ID == installationId &&
                candidate.Employee_Id == employeeId);
            // Return HTTP 404 Not Found when `assignment == null` is true.
            if (assignment == null)
            {
                return NotFound("The employee is not assigned to this schedule row.");
            }

            _Localdb.Installation_Schedule_Employee.Remove(assignment);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }

        /// <summary>Retrieves all installation schedules from the cloud database.</summary>
        [HttpGet("GetCloudInstallationSchedule")]
        public List<Installation_Schedule> GetAllCloudInstallationSchedule()
        {
            return _CloudDb.Installation_Schedule.ToList();
        }

        /// <summary>Retrieves the cloud installation schedule id.</summary>
        [HttpGet("GetCloudInstallationScheduleByID")]
        public ActionResult<Installation_Schedule> GetCloudInstallationScheduleID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var installationSchedule = _CloudDb.Installation_Schedule.FirstOrDefault(x => x.Installation_ID == id);
            // Return HTTP 404 Not Found when `installationSchedule == null` is true.
            if (installationSchedule == null)
            {
                return NotFound();
            }
            return Ok(installationSchedule);
        }

        /// <summary>Adds the cloud installation schedule.</summary>
        [HttpPost("AddCloudInstallation_Schedule")]
        public ActionResult<Installation_Schedule> AddCloudInstallationSchedule([FromBody] Installation_Schedule installationSchedule)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid || installationSchedule == null || installationSchedule.JobOrder_ID <= 0 || installationSchedule.Date == default || string.IsNullOrWhiteSpace(installationSchedule.Mall_Hours)` is true.
            if (!ModelState.IsValid || installationSchedule == null ||
                installationSchedule.JobOrder_ID <= 0 ||
                installationSchedule.Date == default ||
                string.IsNullOrWhiteSpace(installationSchedule.Mall_Hours))
            {
                return BadRequest("A valid job order, date, and mall-hours group are required.");
            }
            // Return HTTP 400 Bad Request when `!_CloudDb.JobOrder.IgnoreQueryFilters().Any(jobOrder => jobOrder.JobOrder_ID == installationSchedule.JobOrder_ID)` is true.
            if (!_CloudDb.JobOrder.IgnoreQueryFilters().Any(jobOrder => jobOrder.JobOrder_ID == installationSchedule.JobOrder_ID))
            {
                return BadRequest("The selected Job Order was not found.");
            }

            installationSchedule.Installation_ID = 0;
            _CloudDb.Installation_Schedule.Add(installationSchedule);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(installationSchedule);
        }

        /// <summary>Updates the cloud installation schedule.</summary>
        [HttpPost("UpdateCloudInstallation_Schedule")]
        public ActionResult<Installation_Schedule> UpdateCloudInstallationSchedule(int Id, [FromBody] Installation_Schedule installationSchedule)
        {
            // Return HTTP 400 Bad Request when `Id <= 0 || installationSchedule == null || installationSchedule.JobOrder_ID <= 0 || installationSchedule.Date == default || string.IsNullOrWhiteSpace(installationSchedule.Mall_Hours)` is true.
            if (Id <= 0 || installationSchedule == null ||
                installationSchedule.JobOrder_ID <= 0 ||
                installationSchedule.Date == default ||
                string.IsNullOrWhiteSpace(installationSchedule.Mall_Hours))
            {
                return BadRequest("A valid schedule row, job order, date, and mall-hours group are required.");
            }
            // Return HTTP 400 Bad Request when `!_CloudDb.JobOrder.IgnoreQueryFilters().Any(jobOrder => jobOrder.JobOrder_ID == installationSchedule.JobOrder_ID)` is true.
            if (!_CloudDb.JobOrder.IgnoreQueryFilters().Any(jobOrder => jobOrder.JobOrder_ID == installationSchedule.JobOrder_ID))
            {
                return BadRequest("The selected Job Order was not found.");
            }

            var existingSchedule = _CloudDb.Installation_Schedule.FirstOrDefault(schedule => schedule.Installation_ID == Id);
            // Return HTTP 404 Not Found when `existingSchedule == null` is true.
            if (existingSchedule == null)
            {
                return NotFound();
            }

            existingSchedule.Time = installationSchedule.Time;
            existingSchedule.Remarks = installationSchedule.Remarks;
            existingSchedule.Mall_Hours = installationSchedule.Mall_Hours;
            existingSchedule.Date = installationSchedule.Date;
            existingSchedule.JobOrder_ID = installationSchedule.JobOrder_ID;
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(existingSchedule);
        }

        /// <summary>Deletes the cloud installation schedule.</summary>
        [HttpPut("DeleteCloudInstallationSchedule")]
        public IActionResult DeleteCloudInstallationSchedule(int id)
        {
            // Return HTTP 400 Bad Request when `id <= 0` is true.
            if (id <= 0)
            {
                return BadRequest("A valid schedule row is required.");
            }

            var installationSchedule = _CloudDb.Installation_Schedule.FirstOrDefault(schedule => schedule.Installation_ID == id);
            // Return HTTP 404 Not Found when `installationSchedule == null` is true.
            if (installationSchedule == null)
            {
                return NotFound();
            }

            _CloudDb.Installation_Schedule.Remove(installationSchedule);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // ----------------------------------- INSTALLATION SCHEDULE -----------------------------------

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
            var result = MaterialRequisitionWorkflowService.Preview(_Localdb, dto, "Production");
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
                "Production",
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
                .Where(slip => slip.FromWho != null && slip.FromWho.Trim().ToLower() == "production")
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

        [HttpGet("GetLocalQuotations")]
        // Retrieves all local quotations. Returns List<Quotation>.
        public List<Quotation> GetAllLocalQuotations()
        {
            return _Localdb.Quotation.ToList();
        }

        /// <summary>Retrieves all conforme records from the local database.</summary>
        [HttpGet("GetLocalConforme")]
        public List<Conforme> GetAllLocalConforme()
        {
            var conforme = _Localdb.Conforme.ToList();

            // Process each `_conforme` returned by the collection expression in the loop header.
            foreach (var _conforme in conforme)
            {
                _conforme.FIleData = _encryptionService.Decrypt(_conforme.FIleData);

            }

            return conforme;


        }

        /// <summary>Retrieves all clients from the local database.</summary>
        [HttpGet("GetAllLocalClients")]
        public List<ClientEntity> GetAllLocalClients()
        {
            return _Localdb.Clients.ToList();
        }



    }
}
