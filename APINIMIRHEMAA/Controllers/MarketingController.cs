using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO.InstallationReport;
using APINIMIRHEMAA.DTO.InstallationSchedule;
using APINIMIRHEMAA.DTO.JobOrders;
using APINIMIRHEMAA.DTO;
using APINIMIRHEMAA.DTO.QuotationItems;
using APINIMIRHEMAA.DTO.ServiceInvoice;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize(Policy = "marketing")]
    [Route("api/[controller]")]
    [ApiController]
    // Coordinates the marketing controller API endpoints, including request handling and response generation.
    public class MarketingController : ControllerBase
    {
        // Injected or cached localdb dependency/state used by this class.
        private LocalDbContext _Localdb;
        // Injected or cached cloud db dependency/state used by this class.
        private CloudDbContext _CloudDb;
        // Encryption service used to protect sensitive persisted values.
        private readonly EncryptionService _encryptionService;
        // Injected or cached notification service dependency/state used by this class.
        private readonly NotificationService _notificationService;
        // Initializes a new marketing controller instance.
        public MarketingController(
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

        // Retrieves the marketing visible job orders.
        private IQueryable<JobOrder> GetMarketingVisibleJobOrders()
        {
            return _Localdb.JobOrder.IgnoreQueryFilters();
        }

        // Checks whether the current role may approve the delivery receipt.
        private static bool IsApprovedByRole(string? role) =>
            string.Equals(role?.Trim(), "ApprovedBy", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role?.Trim(), "ApproverSigned", StringComparison.OrdinalIgnoreCase);

        // Checks whether the delivery receipt has already reached a reviewed status.
        private static bool IsReviewedDeliveryReceiptStatus(string? status) =>
            string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase);

        // ------------------------------- QUOTATION -------------------------------

        [HttpGet("GetLocalQuotationMaterials")]
        // Retrieves local quotation materials. Returns the HTTP response for the requested operation.
        public async Task<IActionResult> GetLocalQuotationMaterials()
        {
            var materials = await _Localdb.Materials
                .AsNoTracking()
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(material => material.Item_Name)
                .Select(material => new
                {
                    material.Material_ID,
                    material.Code_Name,
                    material.Item_Name,
                    material.Category,
                    material.Unit
                })
                .ToListAsync();

            return Ok(materials);
        }

        /// <summary>Retrieves the local quotation inventory.</summary>
        [HttpGet("GetLocalQuotationInventory")]
        public async Task<IActionResult> GetLocalQuotationInventory()
        {
            var inventoryRecords = await _Localdb.Inventory
                .AsNoTracking()
                .ToListAsync();
            var pendingRequests = await _Localdb.MaterialRequisition_Slip
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(request => request.Status == null || request.Status.Trim() == "" ||
                    request.Status.Trim().ToUpper() == "PENDING")
                .GroupBy(request => request.Material_ID)
                .Select(group => new
                {
                    MaterialId = group.Key,
                    Quantity = group.Sum(request => request.Quantity ?? 0)
                })
                .ToDictionaryAsync(group => group.MaterialId, group => group.Quantity);

            // Process each `inventory` returned by the collection expression in the loop header.
            foreach (var inventory in inventoryRecords)
            {
                inventory.Request = pendingRequests.TryGetValue(inventory.Material_ID, out var quantity)
                    ? quantity
                    : 0;
                InventoryRules.Recalculate(inventory);
            }

            return Ok(inventoryRecords.Select(inventory => new
            {
                inventory.Material_ID,
                inventory.Balance
            }));
        }

        /// <summary>Retrieves all quotations from the local database.</summary>
        [HttpGet("GetLocalQuotations")]
        public async Task<ActionResult<List<Quotation>>> GetAllLocalQuotations()
        {
            var quotations = await _Localdb.Quotation.ToListAsync();
            var subjectsForResponse = new Dictionary<int, string>();
            var hasLegacyPlaintextSubjects = false;
            // Process each `quotation` returned by the collection expression in the loop header.
            foreach (var quotation in quotations)
            {
                // Skip this item and continue when `string.IsNullOrWhiteSpace(quotation.Client_Subject)` is true.
                if (string.IsNullOrWhiteSpace(quotation.Client_Subject))
                {
                    continue;
                }

                // Enter this branch when `quotation.Client_Subject.StartsWith(, StringComparison.Ordinal)` is true.
                if (quotation.Client_Subject.StartsWith("CfDJ8", StringComparison.Ordinal))
                {
                    var readableSubject = _encryptionService.TryDecrypt(quotation.Client_Subject);
                    // Exit the current operation when `string.IsNullOrWhiteSpace(readableSubject)` is true.
                    if (string.IsNullOrWhiteSpace(readableSubject))
                    {
                        return StatusCode(
                            StatusCodes.Status500InternalServerError,
                            $"The Client Subject for quotation {quotation.Quotation_ID} could not be decrypted. Verify the ASP.NET Data Protection keys.");
                    }
                    subjectsForResponse[quotation.Quotation_ID] = readableSubject;
                }
                // Use this fallback path when the preceding condition is false.
                else
                {
                    subjectsForResponse[quotation.Quotation_ID] = quotation.Client_Subject;
                    quotation.Client_Subject = _encryptionService.Encrypt(quotation.Client_Subject);
                    hasLegacyPlaintextSubjects = true;
                }
            }

            // Enter this branch when `hasLegacyPlaintextSubjects` is true.
            if (hasLegacyPlaintextSubjects)
            {
                // Persist the pending entity changes to the database at this point.
                await _Localdb.SaveChangesAsync();
            }

            // Process each `quotation` returned by the collection expression in the loop header.
            foreach (var quotation in quotations)
            {
                _Localdb.Entry(quotation).State = EntityState.Detached;
                // Enter this branch when `subjectsForResponse.TryGetValue(quotation.Quotation_ID, out var subject)` is true.
                if (subjectsForResponse.TryGetValue(quotation.Quotation_ID, out var subject))
                {
                    quotation.Client_Subject = subject;
                }
            }

            return Ok(quotations);
        }

        /// <summary>Retrieves the local quotation details.</summary>
        [HttpGet("GetLocalQuotationsByID")]
        public ActionResult<Quotation> GetLocalQuotationDetails(Int32 Id)
        {
            // Return HTTP 400 Bad Request when `Id == 0` is true.
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var QuotationId = _Localdb.Quotation.FirstOrDefault(x => x.Client_ID == Id);
            // Return HTTP 404 Not Found when `QuotationId == null` is true.
            if (QuotationId == null)
            {
                return NotFound("Client not found.");
            }
            return QuotationId;
        }

        /// <summary>Generates a preview of the quotation subject using the supplied client and quotation details.</summary>
        [HttpGet("PreviewLocalClientQuotationSubject")]
        public async Task<ActionResult> PreviewLocalClientQuotationSubject(
            [FromQuery] int clientId,
            [FromQuery] int? sourceQuotationId)
        {
            // Return HTTP 400 Bad Request when `clientId <= 0` is true.
            if (clientId <= 0)
            {
                return BadRequest("A valid client ID is required.");
            }
            // Return HTTP 400 Bad Request when `!await _Localdb.Clients.AsNoTracking().AnyAsync(record => record.Client_ID == clientId)` is true.
            if (!await _Localdb.Clients.AsNoTracking().AnyAsync(record => record.Client_ID == clientId))
            {
                return BadRequest($"Client {clientId} was not found.");
            }

            // Enter this branch when `sourceQuotationId.HasValue` is true.
            if (sourceQuotationId.HasValue)
            {
                // Return HTTP 400 Bad Request when `sourceQuotationId.Value <= 0` is true.
                if (sourceQuotationId.Value <= 0)
                {
                    return BadRequest("A valid source quotation ID is required.");
                }

                var sourceQuotation = await _Localdb.Quotation
                    .AsNoTracking()
                    .FirstOrDefaultAsync(record =>
                        record.Quotation_ID == sourceQuotationId.Value &&
                        record.Client_ID == clientId);
                // Return HTTP 404 Not Found when `sourceQuotation == null` is true.
                if (sourceQuotation == null)
                {
                    return NotFound("The source quotation was not found for the selected client.");
                }

                var sourceSubject = ReadClientSubject(sourceQuotation.Client_Subject);
                // Exit the current operation when `string.IsNullOrWhiteSpace(sourceSubject)` is true.
                if (string.IsNullOrWhiteSpace(sourceSubject))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "The source quotation Client Subject could not be read.");
                }

                var (baseSubject, _) = SplitQuotationSubjectVersion(sourceSubject);
                var nextVersion = await GetNextQuotationRevisionNumberAsync(clientId, baseSubject);
                return Ok(new { clientSubject = $"{baseSubject}.{nextVersion}" });
            }

            var employeeName = _Localdb.GetCurrentEmployeeName().Trim();
            var initial = employeeName.FirstOrDefault(char.IsLetter);
            // Return HTTP 400 Bad Request when `initial == default` is true.
            if (initial == default)
            {
                return BadRequest("The signed-in employee name is unavailable, so a Client Subject cannot be generated.");
            }

            var clientSubject = await PreviewClientQuotationSubjectAsync(_Localdb, clientId, initial);
            return Ok(new { clientSubject });
        }

        /// <summary>Reserves the local client quotation subject.</summary>
        [HttpPost("ReserveLocalClientQuotationSubject")]
        public async Task<ActionResult> ReserveLocalClientQuotationSubject([FromQuery] int clientId)
        {
            // Return HTTP 400 Bad Request when `clientId <= 0` is true.
            if (clientId <= 0)
            {
                return BadRequest("A valid client ID is required.");
            }

            var clientExists = await _Localdb.Clients
                .AsNoTracking()
                .AnyAsync(record => record.Client_ID == clientId);
            // Return HTTP 400 Bad Request when `!clientExists` is true.
            if (!clientExists)
            {
                return BadRequest($"Client {clientId} was not found.");
            }

            var employeeName = _Localdb.GetCurrentEmployeeName().Trim();
            var initial = employeeName.FirstOrDefault(char.IsLetter);
            // Return HTTP 400 Bad Request when `initial == default` is true.
            if (initial == default)
            {
                return BadRequest("The signed-in employee name is unavailable, so a Client Subject cannot be generated.");
            }

            var reservation = await ReserveClientQuotationSubjectAsync(
                _Localdb,
                clientId,
                employeeName,
                initial);

            return Ok(new
            {
                reservationId = reservation.Reservation.Reservation_ID,
                clientSubject = reservation.ClientSubject
            });
        }

        /// <summary>Adds the local quotations.</summary>
        [HttpPost("AddLocalQuotations")]
        public async Task<ActionResult<Quotation>> AddLocalQuotations([FromBody] Quotation QuotationDetails)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var clientExists = await _Localdb.Clients
                .AsNoTracking()
                .AnyAsync(record => record.Client_ID == QuotationDetails.Client_ID);
            // Return HTTP 400 Bad Request when `!clientExists` is true.
            if (!clientExists)
            {
                return BadRequest($"Client {QuotationDetails.Client_ID} was not found.");
            }

            var employeeName = _Localdb.GetCurrentEmployeeName().Trim();
            // Capture one UTC instant so the timestamps written by this operation remain consistent.
            var now = DateTime.Now;
            await using var transaction = await _Localdb.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            ClientQuotationReservation? reservation = null;
            Quotation? revisionSource = null;
            string clientSubject;
            // Enter this branch when `QuotationDetails.RevisionOfQuotation_ID.HasValue` is true.
            if (QuotationDetails.RevisionOfQuotation_ID.HasValue)
            {
                // Return HTTP 400 Bad Request when `QuotationDetails.Client_SubjectReservation_ID.HasValue` is true.
                if (QuotationDetails.Client_SubjectReservation_ID.HasValue)
                {
                    return BadRequest("A quotation revision cannot use a new Client Subject reservation.");
                }

                var sourceQuotationId = QuotationDetails.RevisionOfQuotation_ID.Value;
                // Return HTTP 400 Bad Request when `sourceQuotationId <= 0` is true.
                if (sourceQuotationId <= 0)
                {
                    return BadRequest("A valid source quotation ID is required for a revision.");
                }

                revisionSource = await _Localdb.Quotation.FirstOrDefaultAsync(record =>
                    record.Quotation_ID == sourceQuotationId &&
                    record.Client_ID == QuotationDetails.Client_ID);
                // Return HTTP 404 Not Found when `revisionSource == null` is true.
                if (revisionSource == null)
                {
                    return NotFound("The source quotation was not found for the selected client.");
                }

                var sourceSubject = ReadClientSubject(revisionSource.Client_Subject);
                // Exit the current operation when `string.IsNullOrWhiteSpace(sourceSubject)` is true.
                if (string.IsNullOrWhiteSpace(sourceSubject))
                {
                    return StatusCode(
                        StatusCodes.Status500InternalServerError,
                        "The source quotation Client Subject could not be read.");
                }

                var (baseSubject, sourceVersion) = SplitQuotationSubjectVersion(sourceSubject);
                var nextVersion = await GetNextQuotationRevisionNumberAsync(
                    QuotationDetails.Client_ID,
                    baseSubject);
                // Enter this branch when `!sourceVersion.HasValue` is true.
                if (!sourceVersion.HasValue)
                {
                    revisionSource.Client_Subject = _encryptionService.Encrypt($"{baseSubject}.1");
                }
                QuotationDetails.Start_Date = revisionSource.Start_Date;
                clientSubject = $"{baseSubject}.{nextVersion}";
            }
            // Enter this branch when `QuotationDetails.Client_SubjectReservation_ID.HasValue` is true.
            else if (QuotationDetails.Client_SubjectReservation_ID.HasValue)
            {
                reservation = await _Localdb.ClientQuotationReservations.FirstOrDefaultAsync(record =>
                    record.Reservation_ID == QuotationDetails.Client_SubjectReservation_ID.Value &&
                    record.Client_ID == QuotationDetails.Client_ID);
                // Return HTTP 400 Bad Request when `reservation == null` is true.
                if (reservation == null)
                {
                    return BadRequest("The Client Subject reservation is invalid for the selected client.");
                }
                // Return HTTP 409 Conflict when `reservation.IsUsed` is true.
                if (reservation.IsUsed)
                {
                    return Conflict("This Client Subject reservation has already been used.");
                }
                // Return HTTP 403 Forbidden when `!string.Equals(reservation.ReservedBy, employeeName, StringComparison.OrdinalIgnoreCase)` is true.
                if (!string.Equals(reservation.ReservedBy, employeeName, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
                clientSubject = ReadClientSubject(reservation.Client_Subject);
            }
            // Use this fallback path when the preceding condition is false.
            else
            {
                var initial = employeeName.FirstOrDefault(char.IsLetter);
                // Return HTTP 400 Bad Request when `initial == default` is true.
                if (initial == default)
                {
                    return BadRequest("The signed-in employee name is unavailable, so a Client Subject cannot be generated.");
                }

                var generatedSubject = await ReserveClientQuotationSubjectWithinTransactionAsync(
                    _Localdb,
                    QuotationDetails.Client_ID,
                    employeeName,
                    initial);
                reservation = generatedSubject.Reservation;
                clientSubject = generatedSubject.ClientSubject;
                QuotationDetails.Client_SubjectReservation_ID = reservation.Reservation_ID;
            }
            // Exit the current operation when `string.IsNullOrWhiteSpace(clientSubject)` is true.
            if (string.IsNullOrWhiteSpace(clientSubject))
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "The reserved Client Subject could not be decrypted. Verify the ASP.NET Data Protection keys.");
            }

            QuotationDetails.Client_Subject = _encryptionService.Encrypt(clientSubject);
            // Enter this branch when `revisionSource == null` is true.
            if (revisionSource == null)
            {
                QuotationDetails.Start_Date = DateOnly.FromDateTime(now);
            }
            QuotationDetails.Time = TimeOnly.FromDateTime(now);
            // Enter this branch when `reservation != null` is true.
            if (reservation != null)
            {
                reservation.IsUsed = true;
            }
            _Localdb.Quotation.Add(QuotationDetails);
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            await transaction.CommitAsync();
            _Localdb.Entry(QuotationDetails).State = EntityState.Detached;
            QuotationDetails.RevisionOfQuotation_ID = null;
            QuotationDetails.Client_Subject = clientSubject;
            return Ok(QuotationDetails);
        }

        /// <summary>Updates the local quotations.</summary>
        [HttpPost("UpdateLocalQuotations")]
        public async Task<ActionResult<Quotation>> UpdateLocalQuotations(
            [FromQuery] int Id,
            [FromBody] Quotation UpdateQuotation)
        {
            // Return HTTP 400 Bad Request when `UpdateQuotation == null` is true.
            if (UpdateQuotation == null)
            {
                return BadRequest(UpdateQuotation);
            }
            var updateQuotation = _Localdb.Quotation.FirstOrDefault(x => x.Quotation_ID == Id);
            // Return HTTP 404 Not Found when `updateQuotation == null` is true.
            if (updateQuotation == null)
            {
                return NotFound();
            }

            updateQuotation.Title = UpdateQuotation.Title;
            updateQuotation.Delivery_Charge = UpdateQuotation.Delivery_Charge;
            updateQuotation.Installation_Charge = UpdateQuotation.Installation_Charge;
            updateQuotation.Status = UpdateQuotation.Status;
            updateQuotation.Comment = UpdateQuotation.Comment;
            updateQuotation.Terms_Condition = UpdateQuotation.Terms_Condition;
            updateQuotation.Start_Date = UpdateQuotation.Start_Date;
            updateQuotation.Time = UpdateQuotation.Time;

            var storeIds = await _Localdb.Quotation_Store
                // Apply this predicate in the database query before materializing results.
                .Where(store => store.Quotation_ID == Id)
                .Select(store => store.QS_ID)
                .ToListAsync();
            updateQuotation.Sub_Total = decimal.Round(
                await _Localdb.Quotation_Items
                    // Filter the query to rows satisfying `.Where(item => storeIds.Contains(item.QS_ID)) .SumAsync(item => item.Total_Amount ?? 0m), 2, MidpointRounding.AwayFromZero` before materializing the results.
                    .Where(item => storeIds.Contains(item.QS_ID))
                    .SumAsync(item => item.Total_Amount ?? 0m),
                2,
                MidpointRounding.AwayFromZero);
            updateQuotation.VAT = decimal.Round(updateQuotation.Sub_Total * 0.12m, 2, MidpointRounding.AwayFromZero);
            updateQuotation.Overall_Total = decimal.Round(
                updateQuotation.Sub_Total + (updateQuotation.Delivery_Charge ?? 0m) +
                (updateQuotation.Installation_Charge ?? 0m) + (updateQuotation.VAT ?? 0m),
                2,
                MidpointRounding.AwayFromZero);

            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            return Ok(updateQuotation);
        }

        /// <summary>Updates the local quotation status.</summary>
        [HttpPost("UpdateLocalQuotationStatus")]
        public async Task<ActionResult> UpdateLocalQuotationStatus(
            [FromQuery] int Id,
            [FromQuery] string? Status)
        {
            // Return HTTP 400 Bad Request when `Id <= 0` is true.
            if (Id <= 0)
            {
                return BadRequest("A valid quotation ID is required.");
            }
            // Return HTTP 400 Bad Request when `string.IsNullOrWhiteSpace(Status)` is true.
            if (string.IsNullOrWhiteSpace(Status))
            {
                return BadRequest("A quotation status is required.");
            }

            var quotation = await _Localdb.Quotation
                .FirstOrDefaultAsync(record => record.Quotation_ID == Id);
            // Return HTTP 404 Not Found when `quotation == null` is true.
            if (quotation == null)
            {
                return NotFound("Quotation not found.");
            }

            quotation.Status = Status.Trim();
            // Enter this branch when `string.Equals(quotation.Status, , StringComparison.OrdinalIgnoreCase)` is true.
            if (string.Equals(quotation.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                EnsureRejectedQuotationHasVersion(quotation);
            }
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            return Ok(new { quotation.Quotation_ID, quotation.Status });
        }

        /// <summary>Rejects the local quotation and records the reviewer decision.</summary>
        [HttpPost("RejectLocalQuotation")]
        public async Task<ActionResult> RejectLocalQuotation([FromQuery] int Id)
        {
            // Return HTTP 400 Bad Request when `Id <= 0` is true.
            if (Id <= 0)
            {
                return BadRequest("A valid quotation ID is required.");
            }

            var quotation = await _Localdb.Quotation
                .FirstOrDefaultAsync(record => record.Quotation_ID == Id);
            // Return HTTP 404 Not Found when `quotation == null` is true.
            if (quotation == null)
            {
                return NotFound("Quotation not found.");
            }

            var currentStatus = quotation.Status?.Trim();
            // Return HTTP 409 Conflict when `string.Equals(currentStatus, , StringComparison.OrdinalIgnoreCase)` is true.
            if (string.Equals(currentStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict("This quotation has already been rejected.");
            }
            // Return HTTP 409 Conflict when `string.Equals(currentStatus, , StringComparison.OrdinalIgnoreCase) || string.Equals(currentStatus, , StringComparison.OrdinalIgnoreCase)` is true.
            if (string.Equals(currentStatus, "Completed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(currentStatus, "Complete", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict("A completed quotation cannot be rejected.");
            }
            // Return HTTP 409 Conflict when `await _Localdb.Conforme .AsNoTracking() .AnyAsync(record => record.Quotation_ID == Id)` is true.
            if (await _Localdb.Conforme
                .AsNoTracking()
                .AnyAsync(record => record.Quotation_ID == Id))
            {
                return Conflict("A quotation with an attached Conforme cannot be rejected.");
            }

            quotation.Status = "Rejected";
            EnsureRejectedQuotationHasVersion(quotation);
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            return Ok(new { quotation.Quotation_ID, quotation.Status });
        }

        /// <summary>Deletes the local quotations.</summary>
        [HttpPut ("DeleteQuotation")]
        public ActionResult<Quotation> DeleteLocalQuotations(Int32 Id)
        {
            var quotationDetails = _Localdb.Quotation.FirstOrDefault(x => x.Quotation_ID == Id);
            // Return HTTP 404 Not Found when `quotationDetails == null` is true.
            if (quotationDetails == null)
            {
                return NotFound();
            }
            _Localdb.Remove(quotationDetails);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }

        /// <summary>Retrieves all quotations from the cloud database.</summary>
        [HttpGet("GetCloudQuotations")]
        public async Task<ActionResult<List<Quotation>>> GetAllCloudQuotations()
        {
            var quotations = await _CloudDb.Quotation.ToListAsync();
            var subjectsForResponse = new Dictionary<int, string>();
            var hasLegacyPlaintextSubjects = false;
            // Process each `quotation` returned by the collection expression in the loop header.
            foreach (var quotation in quotations)
            {
                // Skip this item and continue when `string.IsNullOrWhiteSpace(quotation.Client_Subject)` is true.
                if (string.IsNullOrWhiteSpace(quotation.Client_Subject))
                {
                    continue;
                }

                // Enter this branch when `quotation.Client_Subject.StartsWith(, StringComparison.Ordinal)` is true.
                if (quotation.Client_Subject.StartsWith("CfDJ8", StringComparison.Ordinal))
                {
                    var readableSubject = _encryptionService.TryDecrypt(quotation.Client_Subject);
                    // Exit the current operation when `string.IsNullOrWhiteSpace(readableSubject)` is true.
                    if (string.IsNullOrWhiteSpace(readableSubject))
                    {
                        return StatusCode(
                            StatusCodes.Status500InternalServerError,
                            $"The Client Subject for quotation {quotation.Quotation_ID} could not be decrypted. Verify the ASP.NET Data Protection keys.");
                    }
                    subjectsForResponse[quotation.Quotation_ID] = readableSubject;
                }
                // Use this fallback path when the preceding condition is false.
                else
                {
                    subjectsForResponse[quotation.Quotation_ID] = quotation.Client_Subject;
                    quotation.Client_Subject = _encryptionService.Encrypt(quotation.Client_Subject);
                    hasLegacyPlaintextSubjects = true;
                }
            }

            // Enter this branch when `hasLegacyPlaintextSubjects` is true.
            if (hasLegacyPlaintextSubjects)
            {
                // Persist the pending entity changes to the database at this point.
                await _CloudDb.SaveChangesAsync();
            }

            // Process each `quotation` returned by the collection expression in the loop header.
            foreach (var quotation in quotations)
            {
                _CloudDb.Entry(quotation).State = EntityState.Detached;
                // Enter this branch when `subjectsForResponse.TryGetValue(quotation.Quotation_ID, out var subject)` is true.
                if (subjectsForResponse.TryGetValue(quotation.Quotation_ID, out var subject))
                {
                    quotation.Client_Subject = subject;
                }
            }

            return Ok(quotations);
        }

        /// <summary>Retrieves the cloud quotation details.</summary>
        [HttpGet("GetCloudQuotationsByID")]
        public ActionResult<Quotation> GetCloudQuotationDetails(Int32 Id)
        {
            // Return HTTP 400 Bad Request when `Id == 0` is true.
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var QuotationId = _CloudDb.Quotation.FirstOrDefault(x => x.Client_ID == Id);
            // Return HTTP 404 Not Found when `QuotationId == null` is true.
            if (QuotationId == null)
            {
                return NotFound("Client not found.");
            }
            return QuotationId;
        }

        /// <summary>Generates a preview of the quotation subject using the supplied client and quotation details.</summary>
        [HttpGet("PreviewCloudClientQuotationSubject")]
        public async Task<ActionResult> PreviewCloudClientQuotationSubject([FromQuery] int clientId)
        {
            // Return HTTP 400 Bad Request when `clientId <= 0` is true.
            if (clientId <= 0)
            {
                return BadRequest("A valid client ID is required.");
            }
            // Return HTTP 400 Bad Request when `!await _CloudDb.Clients.AsNoTracking().AnyAsync(record => record.Client_ID == clientId)` is true.
            if (!await _CloudDb.Clients.AsNoTracking().AnyAsync(record => record.Client_ID == clientId))
            {
                return BadRequest($"Client {clientId} was not found.");
            }

            var employeeName = _Localdb.GetCurrentEmployeeName().Trim();
            var initial = employeeName.FirstOrDefault(char.IsLetter);
            // Return HTTP 400 Bad Request when `initial == default` is true.
            if (initial == default)
            {
                return BadRequest("The signed-in employee name is unavailable, so a Client Subject cannot be generated.");
            }

            var clientSubject = await PreviewClientQuotationSubjectAsync(_CloudDb, clientId, initial);
            return Ok(new { clientSubject });
        }

        /// <summary>Reserves the cloud client quotation subject.</summary>
        [HttpPost("ReserveCloudClientQuotationSubject")]
        public async Task<ActionResult> ReserveCloudClientQuotationSubject([FromQuery] int clientId)
        {
            // Return HTTP 400 Bad Request when `clientId <= 0` is true.
            if (clientId <= 0)
            {
                return BadRequest("A valid client ID is required.");
            }

            var clientExists = await _CloudDb.Clients
                .AsNoTracking()
                .AnyAsync(record => record.Client_ID == clientId);
            // Return HTTP 400 Bad Request when `!clientExists` is true.
            if (!clientExists)
            {
                return BadRequest($"Client {clientId} was not found.");
            }

            var employeeName = _Localdb.GetCurrentEmployeeName().Trim();
            var initial = employeeName.FirstOrDefault(char.IsLetter);
            // Return HTTP 400 Bad Request when `initial == default` is true.
            if (initial == default)
            {
                return BadRequest("The signed-in employee name is unavailable, so a Client Subject cannot be generated.");
            }

            var reservation = await ReserveClientQuotationSubjectAsync(
                _CloudDb,
                clientId,
                employeeName,
                initial);

            return Ok(new
            {
                reservationId = reservation.Reservation.Reservation_ID,
                clientSubject = reservation.ClientSubject
            });
        }

        /// <summary>Adds the cloud quotations.</summary>
        [HttpPost("AddCloudQuotations")]
        public async Task<ActionResult<Quotation>> AddCloudQuotations([FromBody] Quotation QuotationDetails)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var clientExists = await _CloudDb.Clients
                .AsNoTracking()
                .AnyAsync(record => record.Client_ID == QuotationDetails.Client_ID);
            // Return HTTP 400 Bad Request when `!clientExists` is true.
            if (!clientExists)
            {
                return BadRequest($"Client {QuotationDetails.Client_ID} was not found.");
            }

            var employeeName = _Localdb.GetCurrentEmployeeName().Trim();
            // Capture one UTC instant so the timestamps written by this operation remain consistent.
            var now = DateTime.Now;
            await using var transaction = await _CloudDb.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            ClientQuotationReservation reservation;
            string clientSubject;
            // Enter this branch when `QuotationDetails.Client_SubjectReservation_ID.HasValue` is true.
            if (QuotationDetails.Client_SubjectReservation_ID.HasValue)
            {
                reservation = await _CloudDb.ClientQuotationReservations.FirstOrDefaultAsync(record =>
                    record.Reservation_ID == QuotationDetails.Client_SubjectReservation_ID.Value &&
                    record.Client_ID == QuotationDetails.Client_ID);
                // Return HTTP 400 Bad Request when `reservation == null` is true.
                if (reservation == null)
                {
                    return BadRequest("The Client Subject reservation is invalid for the selected client.");
                }
                // Return HTTP 409 Conflict when `reservation.IsUsed` is true.
                if (reservation.IsUsed)
                {
                    return Conflict("This Client Subject reservation has already been used.");
                }
                // Return HTTP 403 Forbidden when `!string.Equals(reservation.ReservedBy, employeeName, StringComparison.OrdinalIgnoreCase)` is true.
                if (!string.Equals(reservation.ReservedBy, employeeName, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
                clientSubject = ReadClientSubject(reservation.Client_Subject);
            }
            // Use this fallback path when the preceding condition is false.
            else
            {
                var initial = employeeName.FirstOrDefault(char.IsLetter);
                // Return HTTP 400 Bad Request when `initial == default` is true.
                if (initial == default)
                {
                    return BadRequest("The signed-in employee name is unavailable, so a Client Subject cannot be generated.");
                }

                var generatedSubject = await ReserveClientQuotationSubjectWithinTransactionAsync(
                    _CloudDb,
                    QuotationDetails.Client_ID,
                    employeeName,
                    initial);
                reservation = generatedSubject.Reservation;
                clientSubject = generatedSubject.ClientSubject;
                QuotationDetails.Client_SubjectReservation_ID = reservation.Reservation_ID;
            }
            // Exit the current operation when `string.IsNullOrWhiteSpace(clientSubject)` is true.
            if (string.IsNullOrWhiteSpace(clientSubject))
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "The reserved Client Subject could not be decrypted. Verify the ASP.NET Data Protection keys.");
            }

            QuotationDetails.Client_Subject = _encryptionService.Encrypt(clientSubject);
            QuotationDetails.Start_Date = DateOnly.FromDateTime(now);
            QuotationDetails.Time = TimeOnly.FromDateTime(now);
            reservation.IsUsed = true;
            _CloudDb.Quotation.Add(QuotationDetails);
            // Persist the pending entity changes to the database at this point.
            await _CloudDb.SaveChangesAsync();
            await transaction.CommitAsync();
            _CloudDb.Entry(QuotationDetails).State = EntityState.Detached;
            QuotationDetails.Client_Subject = clientSubject;
            return Ok(QuotationDetails);
        }

        // Performs the reserve client quotation subject operation. Inputs: db (db); clientId (identifier used to select the target record); employeeName (employee name); initial (initial). Returns (ClientQuotationReservation Reservation, string ClientSubject) asynchronously.
        private async Task<(ClientQuotationReservation Reservation, string ClientSubject)> ReserveClientQuotationSubjectAsync(
            DbContext db,
            int clientId,
            string employeeName,
            char initial)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var result = await ReserveClientQuotationSubjectWithinTransactionAsync(db, clientId, employeeName, initial);
            await transaction.CommitAsync();
            return result;
        }

        // Generates a quotation-subject preview from the supplied client and quotation details.
        private async Task<string> PreviewClientQuotationSubjectAsync(DbContext db, int clientId, char initial)
        {
            // Capture one UTC instant so the timestamps written by this operation remain consistent.
            var now = DateTime.Now;
            var lastSequence = await db.Set<ClientQuotationSequence>()
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(record =>
                    record.Client_ID == clientId &&
                    record.SequenceYear == now.Year &&
                    record.SequenceMonth == now.Month)
                .Select(record => (int?)record.LastSequence)
                .FirstOrDefaultAsync();
            var nextSequence = checked((lastSequence ?? 0) + 1);
            return $"{now.Month:D2}{char.ToUpperInvariant(initial)}{nextSequence:D3}";
        }

        // Performs the reserve client quotation subject within transaction operation. Inputs: db (db); clientId (identifier used to select the target record); employeeName (employee name); initial (initial). Returns (ClientQuotationReservation Reservation, string ClientSubject) asynchronously.
        private async Task<(ClientQuotationReservation Reservation, string ClientSubject)> ReserveClientQuotationSubjectWithinTransactionAsync(
            DbContext db,
            int clientId,
            string employeeName,
            char initial)
        {
            // Capture one UTC instant so the timestamps written by this operation remain consistent.
            var now = DateTime.Now;
            var year = now.Year;
            var month = now.Month;
            var sequence = await db.Set<ClientQuotationSequence>().FirstOrDefaultAsync(record =>
                record.Client_ID == clientId &&
                record.SequenceYear == year &&
                record.SequenceMonth == month);
            // Enter this branch when `sequence == null` is true.
            if (sequence == null)
            {
                sequence = new ClientQuotationSequence
                {
                    Client_ID = clientId,
                    SequenceYear = year,
                    SequenceMonth = checked((byte)month),
                    LastSequence = 1
                };
                db.Set<ClientQuotationSequence>().Add(sequence);
            }
            // Use this fallback path when the preceding condition is false.
            else
            {
                sequence.LastSequence = checked(sequence.LastSequence + 1);
            }

            var clientSubject = $"{month:D2}{char.ToUpperInvariant(initial)}{sequence.LastSequence:D3}";
            var reservation = new ClientQuotationReservation
            {
                Reservation_ID = Guid.NewGuid(),
                Client_ID = clientId,
                SequenceYear = year,
                SequenceMonth = checked((byte)month),
                SequenceNumber = sequence.LastSequence,
                Client_Subject = _encryptionService.Encrypt(clientSubject),
                ReservedBy = employeeName,
                ReservedAt = now,
                IsUsed = false
            };
            db.Set<ClientQuotationReservation>().Add(reservation);
            // Persist the pending entity changes to the database at this point.
            await db.SaveChangesAsync();
            return (reservation, clientSubject);
        }

        // Reads the client subject.
        private string ReadClientSubject(string? storedSubject)
        {
            // Exit the current operation when `string.IsNullOrEmpty(storedSubject)` is true.
            if (string.IsNullOrEmpty(storedSubject))
            {
                return string.Empty;
            }

            return storedSubject.StartsWith("CfDJ8", StringComparison.Ordinal)
                ? _encryptionService.TryDecrypt(storedSubject)
                : storedSubject;
        }

        // Retrieves the next quotation revision number.
        private async Task<int> GetNextQuotationRevisionNumberAsync(int clientId, string baseSubject)
        {
            var storedSubjects = await _Localdb.Quotation
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(record => record.Client_ID == clientId)
                .Select(record => record.Client_Subject)
                .ToListAsync();
            var latestVersion = 1;
            // Process each `storedSubject` returned by the collection expression in the loop header.
            foreach (var storedSubject in storedSubjects)
            {
                var subject = ReadClientSubject(storedSubject);
                var (candidateBaseSubject, version) = SplitQuotationSubjectVersion(subject);
                // Enter this branch when `version.HasValue && string.Equals(candidateBaseSubject, baseSubject, StringComparison.OrdinalIgnoreCase)` is true.
                if (version.HasValue &&
                    string.Equals(candidateBaseSubject, baseSubject, StringComparison.OrdinalIgnoreCase))
                {
                    latestVersion = Math.Max(latestVersion, version.Value);
                }
            }

            return checked(latestVersion + 1);
        }

        // Performs the split quotation subject version operation. Inputs: subject (subject). Returns (string BaseSubject, int? Version).
        private static (string BaseSubject, int? Version) SplitQuotationSubjectVersion(string subject)
        {
            var normalizedSubject = subject.Trim();
            var separator = normalizedSubject.LastIndexOf('.');
            // Exit the current operation when `separator > 0 && int.TryParse(normalizedSubject[(separator + 1)..], out var version) && version > 0` is true.
            if (separator > 0 &&
                int.TryParse(normalizedSubject[(separator + 1)..], out var version) &&
                version > 0)
            {
                return (normalizedSubject[..separator], version);
            }

            return (normalizedSubject, null);
        }

        // Ensures the rejected quotation has version is available or in the required state.
        private void EnsureRejectedQuotationHasVersion(Quotation quotation)
        {
            var subject = ReadClientSubject(quotation.Client_Subject);
            // Exit the current operation when `string.IsNullOrWhiteSpace(subject)` is true.
            if (string.IsNullOrWhiteSpace(subject))
            {
                return;
            }

            var (baseSubject, version) = SplitQuotationSubjectVersion(subject);
            // Enter this branch when `!version.HasValue` is true.
            if (!version.HasValue)
            {
                quotation.Client_Subject = _encryptionService.Encrypt($"{baseSubject}.1");
            }
        }

        /// <summary>Updates the cloud quotations.</summary>
        [HttpPost("UpdateCloudQuotations")]
        public ActionResult<Quotation> UpdateCloudQuotations(Int32 Id, [FromBody] Quotation UpdateQuotation)
        {
            // Return HTTP 400 Bad Request when `UpdateQuotation == null` is true.
            if (UpdateQuotation == null)
            {
                return BadRequest(UpdateQuotation);
            }
            var updateQuotation = _CloudDb.Quotation.FirstOrDefault(x => x.Quotation_ID == Id);
            // Return HTTP 404 Not Found when `updateQuotation == null` is true.
            if (updateQuotation == null)
            {
                return NotFound();
            }
            updateQuotation.Quotation_ID = UpdateQuotation.Quotation_ID;
            updateQuotation.Client_ID = UpdateQuotation.Client_ID;
            updateQuotation.Sub_Total = UpdateQuotation.Sub_Total;
            updateQuotation.Delivery_Charge = UpdateQuotation.Delivery_Charge;
            updateQuotation.Installation_Charge = UpdateQuotation.Installation_Charge;
            updateQuotation.VAT = UpdateQuotation.VAT;
            updateQuotation.Overall_Total = UpdateQuotation.Overall_Total;
            updateQuotation.Status = UpdateQuotation.Status;
            updateQuotation.Comment = UpdateQuotation.Comment;
            updateQuotation.Terms_Condition = UpdateQuotation.Terms_Condition;
            updateQuotation.Start_Date = UpdateQuotation.Start_Date;
            updateQuotation.Time = UpdateQuotation.Time;

            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(UpdateQuotation);
        }

        /// <summary>Deletes the cloud quotations.</summary>
        [HttpPut("DeleteCloudQuotation")]
        public ActionResult<Quotation> DeleteCloudQuotations(Int32 Id)
        {
            var quotationDetails = _CloudDb.Quotation.FirstOrDefault(x => x.Quotation_ID == Id);
            // Return HTTP 404 Not Found when `quotationDetails == null` is true.
            if (quotationDetails == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(quotationDetails);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- QUOTATION -------------------------------

        // ------------------------------- QUOTATION ITEMS -------------------------------


        [HttpGet("GetLocalQuotationItems")]
        // Retrieves all local quotations items. Returns List<Quotation_Items> asynchronously.
        public async Task<List<Quotation_Items>> GetAllLocalQuotationsItems()
        {
            var items = await _Localdb.Quotation_Items
                .AsNoTracking()
                // Load the related `item => item.Materials` data with the parent records for the response or subsequent logic.
                .Include(item => item.Materials)
                .ToListAsync();
            await PopulateQuotationStoreIds(items);
            return items;
        }

        /// <summary>Retrieves the local quotation item details.</summary>
        [HttpGet("GetLocalQuotationItemsByID")]
        public async Task<ActionResult<Quotation_Items>> GetLocalQuotationItemDetails(Int32 Id)
        {
            // Return HTTP 400 Bad Request when `Id == 0` is true.
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var QI_Id = await _Localdb.Quotation_Items
                // Load the related `item => item.Materials` data with the parent records for the response or subsequent logic.
                .Include(item => item.Materials)
                .FirstOrDefaultAsync(x => x.QI_ID == Id);
            // Return HTTP 404 Not Found when `QI_Id == null` is true.
            if (QI_Id == null)
            {
                return NotFound("Client not found.");
            }
            await PopulateQuotationStoreIds(new[] { QI_Id });
            return QI_Id;
        }

        // Adds the related store identifiers to each quotation item.
        private async Task PopulateQuotationStoreIds(IEnumerable<Quotation_Items> items)
        {
            var itemList = items.ToList();
            // Exit the current operation when `itemList.Count == 0` is true.
            if (itemList.Count == 0)
            {
                return;
            }

            var itemIds = itemList.Select(item => item.QI_ID).ToList();
            var storeLinks = await _Localdb.QuotationItemStores
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(link => itemIds.Contains(link.QI_ID))
                .ToListAsync();
            var storeIdsByItem = storeLinks
                .GroupBy(link => link.QI_ID)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(link => link.QS_ID).ToHashSet());

            // Process each `item` returned by the collection expression in the loop header.
            foreach (var item in itemList)
            {
                var storeIds = storeIdsByItem.TryGetValue(item.QI_ID, out var linkedStoreIds)
                    ? linkedStoreIds
                    : new HashSet<int>();
                storeIds.Add(item.QS_ID);
                // Apply the ordering expression here before returning or materializing the results.
                item.QuotationStoreIds = storeIds.OrderBy(storeId => storeId).ToList();
            }
        }

        /// <summary>Retrieves the local quotation item materials.</summary>
        [HttpGet("GetLocalQuotationItemMaterials")]
        public async Task<ActionResult<List<QuotationItemMaterial>>> GetLocalQuotationItemMaterials(int quotationItemId)
        {
            // Return HTTP 400 Bad Request when `quotationItemId <= 0` is true.
            if (quotationItemId <= 0)
            {
                return BadRequest("A valid quotation item ID is required.");
            }
            // Return HTTP 404 Not Found when `!await _Localdb.Quotation_Items.AnyAsync(item => item.QI_ID == quotationItemId)` is true.
            if (!await _Localdb.Quotation_Items.AnyAsync(item => item.QI_ID == quotationItemId))
            {
                return NotFound("Quotation item not found.");
            }

            var materials = await _Localdb.QuotationItemMaterials
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(material => material.QI_ID == quotationItemId)
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(material => material.Code_Name)
                .ToListAsync();
            return Ok(materials);
        }

        /// <summary>Adds the local quotations items.</summary>
        [HttpPost("AddLocalQuotationItems")]
        public async Task<ActionResult<Quotation_Items>> AddLocalQuotationsItems([FromBody] AddQuotationItemDto request)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            // Return HTTP 400 Bad Request when `request.QS_ID <= 0` is true.
            if (request.QS_ID <= 0)
            {
                return BadRequest("A valid quotation store ID is required.");
            }
            // Return HTTP 400 Bad Request when `request.QuotationStoreIds == null || request.Materials == null` is true.
            if (request.QuotationStoreIds == null || request.Materials == null)
            {
                return BadRequest("Quotation store IDs and materials must be provided as lists.");
            }
            // Return HTTP 400 Bad Request when `request.QuotationStoreIds.Any(storeId => storeId <= 0)` is true.
            if (request.QuotationStoreIds.Any(storeId => storeId <= 0))
            {
                return BadRequest("All quotation store IDs must be positive integers.");
            }
            // Return HTTP 400 Bad Request when `request.Materials.Any(material => material.Material_ID <= 0) || request.Materials.Select(material => material.Material_ID).Distinct().Count() != request.Materials.Count` is true.
            if (request.Materials.Any(material => material.Material_ID <= 0) ||
                request.Materials.Select(material => material.Material_ID).Distinct().Count() != request.Materials.Count)
            {
                return BadRequest("Selected materials must have valid, unique material IDs.");
            }

            var quotationStoreIds = request.QuotationStoreIds
                .Append(request.QS_ID)
                .Distinct()
                .ToList();
            var quotationStores = await _Localdb.Quotation_Store
                // Apply this predicate in the database query before materializing results.
                .Where(store => quotationStoreIds.Contains(store.QS_ID))
                .ToListAsync();
            // Return HTTP 400 Bad Request when `quotationStores.Count != quotationStoreIds.Count` is true.
            if (quotationStores.Count != quotationStoreIds.Count)
            {
                return BadRequest("One or more selected quotation stores no longer exist.");
            }
            var primaryStore = quotationStores.FirstOrDefault(store => store.QS_ID == request.QS_ID);
            // Return HTTP 400 Bad Request when `primaryStore == null || quotationStores.Any(store => store.Quotation_ID != primaryStore.Quotation_ID)` is true.
            if (primaryStore == null || quotationStores.Any(store => store.Quotation_ID != primaryStore.Quotation_ID))
            {
                return BadRequest("A quotation item can only be shared between stores in the same quotation.");
            }

            var materialIds = request.Materials.Select(material => material.Material_ID).ToList();
            var materialsById = await _Localdb.Materials
                // Apply this predicate in the database query before materializing results.
                .Where(material => materialIds.Contains(material.Material_ID))
                .ToDictionaryAsync(material => material.Material_ID);
            // Return HTTP 400 Bad Request when `materialsById.Count != materialIds.Count` is true.
            if (materialsById.Count != materialIds.Count)
            {
                return BadRequest("One or more selected materials no longer exist.");
            }

            var quotationItem = new Quotation_Items
            {
                Requirements = request.Requirements,
                Description = request.Description,
                Width = request.Width,
                Height = request.Height,
                Measurement_Unit = request.Measurement_Unit,
                Price_Per_Square_Inch = request.Price_Per_Square_Inch,
                Quantity = request.Quantity,
                Unit_Price = request.Unit_Price,
                Total_Amount = request.Total_Amount,
                QS_ID = request.QS_ID,
                Materials = request.Materials.Select(selection =>
                {
                    // Resolves each selected material and builds its quotation line-item representation.
                    var material = materialsById[selection.Material_ID];
                    return new QuotationItemMaterial
                    {
                        Material_ID = material.Material_ID,
                        Code_Name = material.Code_Name,
                        Item_Name = material.Item_Name,
                        Unit = material.Unit
                    };
                }).ToList()
            };

            await using var transaction = await _Localdb.Database.BeginTransactionAsync();
            _Localdb.Quotation_Items.Add(quotationItem);
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            _Localdb.QuotationItemStores.AddRange(quotationStoreIds.Select(storeId => new QuotationItemStore
            {
                QI_ID = quotationItem.QI_ID,
                QS_ID = storeId
            }));
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            await transaction.CommitAsync();
            quotationItem.QuotationStoreIds = quotationStoreIds;
            return Ok(quotationItem);
        }

        /// <summary>Updates the local quotation items.</summary>
        [HttpPost("UpdateLocalQuotationItems")]
        public async Task<ActionResult<Quotation_Items>> UpdateLocalQuotationItems(
            [FromQuery] int Id,
            [FromBody] Quotation_Items UpdateQuotationItems)
        {
            // Enter this branch when `Id <= 0 || UpdateQuotationItems == null` is true.
            if (Id <= 0 || UpdateQuotationItems == null)
            {
                // Coordinates the are API endpoints, including request handling and response generation.
                return BadRequest("A valid quotation item ID and record are required.");
            }
            var updateQuotationItems = await _Localdb.Quotation_Items
                .FirstOrDefaultAsync(item => item.QI_ID == Id);
            // Return HTTP 404 Not Found when `updateQuotationItems == null` is true.
            if (updateQuotationItems == null)
            {
                return NotFound();
            }

            // Return HTTP 400 Bad Request when `string.IsNullOrWhiteSpace(UpdateQuotationItems.Requirements) || UpdateQuotationItems.Width is not > 0 || UpdateQuotationItems.Height is not > 0 || UpdateQuotationItems.Quantity is not > 0 || UpdateQuotationItems.Price_Per_Square_Inch is not >= 0` is true.
            if (string.IsNullOrWhiteSpace(UpdateQuotationItems.Requirements) ||
                UpdateQuotationItems.Width is not > 0 ||
                UpdateQuotationItems.Height is not > 0 ||
                UpdateQuotationItems.Quantity is not > 0 ||
                UpdateQuotationItems.Price_Per_Square_Inch is not >= 0)
            {
                return BadRequest("Requirement, dimensions, quantity, and a non-negative price per square inch are required.");
            }

            var inchesPerUnit = UpdateQuotationItems.Measurement_Unit?.Trim().ToLowerInvariant() switch
            {
                "mm" => 1m / 25.4m,
                "cm" => 1m / 2.54m,
                "inch" => 1m,
                "m" => 100m / 2.54m,
                _ => 0m
            };
            // Return HTTP 400 Bad Request when `inchesPerUnit == 0m` is true.
            if (inchesPerUnit == 0m)
            {
                return BadRequest("A supported measurement unit is required (mm, cm, inch, or m).");
            }

            updateQuotationItems.Requirements = UpdateQuotationItems.Requirements;
            updateQuotationItems.Description = UpdateQuotationItems.Description;
            updateQuotationItems.Width = UpdateQuotationItems.Width;
            updateQuotationItems.Height = UpdateQuotationItems.Height;
            updateQuotationItems.Measurement_Unit = UpdateQuotationItems.Measurement_Unit;
            updateQuotationItems.Quantity = UpdateQuotationItems.Quantity;
            updateQuotationItems.Price_Per_Square_Inch = UpdateQuotationItems.Price_Per_Square_Inch;
            updateQuotationItems.Unit_Price = decimal.Round(
                UpdateQuotationItems.Width.Value * UpdateQuotationItems.Height.Value *
                inchesPerUnit * inchesPerUnit * UpdateQuotationItems.Price_Per_Square_Inch.Value,
                2,
                MidpointRounding.AwayFromZero);
            updateQuotationItems.Total_Amount = decimal.Round(
                updateQuotationItems.Unit_Price.Value * UpdateQuotationItems.Quantity.Value,
                2,
                MidpointRounding.AwayFromZero);

            var quotationStore = await _Localdb.Quotation_Store
                .FirstOrDefaultAsync(store => store.QS_ID == updateQuotationItems.QS_ID);
            // Return HTTP 400 Bad Request when `quotationStore == null` is true.
            if (quotationStore == null)
            {
                return BadRequest("The quotation item is not linked to a saved quotation store.");
            }
            var quotation = await _Localdb.Quotation
                .FirstOrDefaultAsync(record => record.Quotation_ID == quotationStore.Quotation_ID);
            // Return HTTP 400 Bad Request when `quotation == null` is true.
            if (quotation == null)
            {
                return BadRequest("The quotation item is not linked to a saved quotation.");
            }
            var storeIds = await _Localdb.Quotation_Store
                // Apply this predicate in the database query before materializing results.
                .Where(store => store.Quotation_ID == quotation.Quotation_ID)
                .Select(store => store.QS_ID)
                .ToListAsync();
            var subtotal = await _Localdb.Quotation_Items
                // Apply this predicate in the database query before materializing results.
                .Where(item => storeIds.Contains(item.QS_ID))
                .SumAsync(item => item.Total_Amount ?? 0m);
            quotation.Sub_Total = decimal.Round(subtotal, 2, MidpointRounding.AwayFromZero);
            quotation.VAT = decimal.Round(quotation.Sub_Total * 0.12m, 2, MidpointRounding.AwayFromZero);
            quotation.Overall_Total = decimal.Round(
                quotation.Sub_Total + (quotation.Delivery_Charge ?? 0m) +
                (quotation.Installation_Charge ?? 0m) + (quotation.VAT ?? 0m),
                2,
                MidpointRounding.AwayFromZero);

            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            return Ok(updateQuotationItems);
        }

        /// <summary>Deletes the local quotation items.</summary>
        [HttpPut("DeleteQuotationItems")]
        public ActionResult<Quotation_Items> DeleteLocalQuotationItems(Int32 Id)
        {
            var quotationItemDetails = _Localdb.Quotation_Items.FirstOrDefault(x => x.QI_ID == Id);
            // Return HTTP 404 Not Found when `quotationItemDetails == null` is true.
            if (quotationItemDetails == null)
            {
                return NotFound();
            }
            _Localdb.Remove(quotationItemDetails);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- QUOTATION ITEMS -------------------------------

        // ------------------------------- QUOTATION STORE -------------------------------


        [HttpGet("GetLocalQuotationStore")]
        // Retrieves all local quotations store. Returns List<Quotation_Store>.
        public List<Quotation_Store> GetAllLocalQuotationsStore()
        {
            return _Localdb.Quotation_Store.ToList();
        }

        /// <summary>Retrieves the local quotation store details.</summary>
        [HttpGet("GetLocalQuotationStoreByID")]
        public ActionResult<Quotation_Store> GetLocalQuotationStoreDetails(Int32 Id)
        {
            // Return HTTP 400 Bad Request when `Id == 0` is true.
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var QS_Id = _Localdb.Quotation_Store.FirstOrDefault(x => x.QS_ID == Id);
            // Return HTTP 404 Not Found when `QS_Id == null` is true.
            if (QS_Id == null)
            {
                return NotFound("Client not found.");
            }
            return QS_Id;
        }

        /// <summary>Adds the local quotation store.</summary>
        [HttpPost("AddLocalQuotationStore")]
        public ActionResult<Quotation_Store> AddLocalQuotationStore([FromBody] Quotation_Store QuotationStoreDetails)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Quotation_Store.Add(QuotationStoreDetails);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(QuotationStoreDetails);
        }

        /// <summary>Updates the local quotation store.</summary>
        [HttpPost("UpdateLocalQuotationStore")]
        public ActionResult<Quotation_Store> UpdateLocalQuotationStore(Int32 Id, [FromBody] Quotation_Store UpdateQuotationStore)
        {
            // Return HTTP 400 Bad Request when `UpdateQuotationStore == null` is true.
            if (UpdateQuotationStore == null)
            {
                return BadRequest(UpdateQuotationStore);
            }
            var updateQuotationStore = _Localdb.Quotation_Store.FirstOrDefault(x => x.QS_ID == Id);
            // Return HTTP 404 Not Found when `updateQuotationStore == null` is true.
            if (updateQuotationStore == null)
            {
                return NotFound();
            }

            // Return HTTP 400 Bad Request when `UpdateQuotationStore.Quotation_ID != updateQuotationStore.Quotation_ID` is true.
            if (UpdateQuotationStore.Quotation_ID != updateQuotationStore.Quotation_ID)
            {
                return BadRequest("A quotation store cannot be moved to a different quotation.");
            }
            updateQuotationStore.Store = UpdateQuotationStore.Store;

            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(updateQuotationStore);
        }

        /// <summary>Deletes the local quotation store.</summary>
        [HttpPut("DeleteQuotationStore")]
        public ActionResult<Quotation_Store> DeleteLocalQuotationStore(Int32 Id)
        {
            var quotationStoreDetails = _Localdb.Quotation_Store.FirstOrDefault(x => x.QS_ID == Id);
            // Return HTTP 404 Not Found when `quotationStoreDetails == null` is true.
            if (quotationStoreDetails == null)
            {
                return NotFound();
            }
            _Localdb.Remove(quotationStoreDetails);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- QUOTATION STORE -------------------------------


        // ------------------------------- CONFORME -------------------------------

        [HttpGet("GetLocalConforme")]
        // Retrieves all local conforme. Returns List<Conforme>.
        public List<Conforme> GetAllLocalConforme()
        {
            var conforme = _Localdb.Conforme.AsNoTracking().ToList();

            // Process each `_conforme` returned by the collection expression in the loop header.
            foreach (var _conforme in conforme)
            {
                _conforme.FIleData = _encryptionService.Decrypt(_conforme.FIleData);

            }

            return conforme;


        }

        /// <summary>Retrieves the local conforme by id.</summary>
        [HttpGet("GetLocalConformeByID")]
        public ActionResult<Conforme> GetLocalConformeByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var conforme = _Localdb.Conforme.AsNoTracking().FirstOrDefault(x => x.FileID == id);
            // Return HTTP 404 Not Found when `conforme == null` is true.
            if (conforme == null) return NotFound();
            conforme.FIleData = _encryptionService.TryDecrypt(conforme.FIleData);
            return Ok(conforme);
        }

        /// <summary>Adds the local conforme.</summary>
        [HttpPost ("AddLocalConforme")]
        public ActionResult<Conforme> AddLocalConforme(Conforme conforme)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            conforme.FIleData = _encryptionService.Encrypt(conforme.FIleData);

            _Localdb.Conforme.Add(conforme);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(conforme);
        }

        /// <summary>Adds the local conforme batch.</summary>
        [HttpPost("AddLocalConformeBatch")]
        [RequestSizeLimit(100_000_000)]
        public ActionResult<List<int>> AddLocalConformeBatch([FromBody] List<Conforme> files)
        {
            // Return HTTP 400 Bad Request when `files == null || files.Count == 0` is true.
            if (files == null || files.Count == 0) return BadRequest("No files.");

            // Repeat the body while `the loop condition` remains true.
            for (int i = 0; i < files.Count; i++)
            {
                var error = UploadValidator.ValidatePdf(files[i].FIleData);
                // Return HTTP 400 Bad Request when `error != null` is true.
                if (error != null) return BadRequest($"File {i + 1}: {error}");
            }

            // Process each `f` returned by the collection expression in the loop header.
            foreach (var f in files)
            {
                f.FIleData = _encryptionService.Encrypt(f.FIleData);
                _Localdb.Conforme.Add(f);
            }

            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();   // one call = all rows or none
            return Ok(files.Select(f => f.FileID).ToList());
        }


        /// <summary>Updates the local conforme.</summary>
        [HttpPost("UpdateLocalConforme")]
        [RequestSizeLimit(10_000_000)]
        public ActionResult<Conforme> UpdateLocalConforme(Int32 Id, [FromBody] Conforme _conforme)
        {
            // Return HTTP 400 Bad Request when `_conforme == null` is true.
            if (_conforme == null)
            {
                return BadRequest(_conforme);
            }
            var updateConforme = _Localdb.Conforme.FirstOrDefault(x => x.FileID == Id);
            // Return HTTP 404 Not Found when `updateConforme == null` is true.
            if (updateConforme == null)
            {
                return NotFound();
            }

            // Enter this branch when `_conforme.FIleData is { Length: > 0 }` is true.
            if (_conforme.FIleData is { Length: > 0 })
            {
                var error = UploadValidator.ValidatePdf(_conforme.FIleData);
                // Return HTTP 400 Bad Request when `error != null` is true.
                if (error != null)
                {
                    return BadRequest(error);
                }
                updateConforme.FIleData = _encryptionService.Encrypt(_conforme.FIleData);
            }
            updateConforme.FileName = _conforme.FileName;
            updateConforme.FileExtension = _conforme.FileExtension;
            updateConforme.Status = _conforme.Status;

            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(updateConforme);
        }

        /// <summary>Deletes the local conforme.</summary>
        [HttpPut ("DeleteLocalConforme")]
        public ActionResult<Conforme> DeleteLocalConforme(int id)
        {
            var conforme = _Localdb.Conforme.FirstOrDefault(x => x.FileID == id);
            // Return HTTP 404 Not Found when `conforme == null` is true.
            if (conforme == null)
            {
                return NotFound();
            }
            _Localdb.Remove(conforme);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }


        /// <summary>Retrieves all conforme records from the cloud database.</summary>
        [HttpGet("GetCloudConforme")]
        public List<Conforme> GetAllCloudConforme()
        {
            return _CloudDb.Conforme.ToList();
        }

        /// <summary>Retrieves the cloud conforme by id.</summary>
        [HttpGet("GetCloudConformeByID")]
        public ActionResult<Conforme> GetCloudConformeByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var conforme = _CloudDb.Conforme.FirstOrDefault(x => x.FileID == id);
            // Return HTTP 404 Not Found when `conforme == null` is true.
            if (conforme == null)
            {
                return NotFound();
            }
            return Ok(conforme);
        }

        /// <summary>Adds the cloud conforme.</summary>
        [HttpPost("AddCloudConforme")]
        public ActionResult<Conforme> AddCloudConforme(Conforme conforme)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Conforme.Add(conforme);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(conforme);
        }

        /// <summary>Updates the cloud conforme.</summary>
        [HttpPost("UpdateCloudConforme")]
        public ActionResult<Quotation> UpdateCloudConforme(Int32 Id, [FromBody] Conforme _conforme)
        {
            // Return HTTP 400 Bad Request when `_conforme == null` is true.
            if (_conforme == null)
            {
                return BadRequest(_conforme);
            }
            var updateConforme = _CloudDb.Conforme.FirstOrDefault(x => x.FileID == Id);
            // Return HTTP 404 Not Found when `updateConforme == null` is true.
            if (updateConforme == null)
            {
                return NotFound();
            }
            updateConforme.Quotation_ID = _conforme.Quotation_ID;
            updateConforme.FileName = _conforme.FileName;
            updateConforme.FileExtension = _conforme.FileExtension;
            updateConforme.FIleData = _conforme.FIleData;
            updateConforme.Quotation_ID = _conforme.Quotation_ID;

            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(updateConforme);
        }

        /// <summary>Deletes the cloud conforme.</summary>
        [HttpPut("DeleteCloudConforme")]
        public ActionResult<Conforme> DeleteCloudConforme(int id)
        {
            var conforme = _CloudDb.Conforme.FirstOrDefault(x => x.FileID == id);
            // Return HTTP 404 Not Found when `conforme == null` is true.
            if (conforme == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(conforme);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- CONFORME -------------------------------

        // ------------------------------- JOB ORDER -------------------------------

        [HttpGet("GetLocalJobOrder")]
        // Retrieves all job order. Returns List<JobOrder> asynchronously.
        public async Task<List<JobOrder>> GetAllJobOrder()
        {
            var jobOrders = await GetMarketingVisibleJobOrders()
                .AsNoTracking()
                .ToListAsync(HttpContext.RequestAborted);
            await JobOrderClientQueries.PopulateAsync(_Localdb, jobOrders, HttpContext.RequestAborted);
            return jobOrders;
        }

        /// <summary>Retrieves the completed local job orders.</summary>
        [HttpGet("GetCompletedLocalJobOrders")]
        public async Task<ActionResult<List<JobOrder>>> GetCompletedLocalJobOrders([FromQuery] string? recordType)
        {
            var jobOrders = await _Localdb.JobOrder
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(jobOrder =>
                    jobOrder.Production_Completed ||
                    jobOrder.Status == "Completed" ||
                    jobOrder.Status == "Complete" ||
                    jobOrder.Status == "complited")
                // Apply the ordering expression here before returning or materializing the results.
                .OrderByDescending(jobOrder => jobOrder.JobOrder_ID)
                .ToListAsync();

            // Enter this branch when `recordType?.Trim().Equals(, StringComparison.OrdinalIgnoreCase) == true` is true.
            if (recordType?.Trim().Equals("delivery-receipt", StringComparison.OrdinalIgnoreCase) == true)
            {
                var jobOrderIds = jobOrders.Select(jobOrder => jobOrder.JobOrder_ID).ToList();
                // Exit the current operation when `jobOrderIds.Count == 0` is true.
                if (jobOrderIds.Count == 0)
                {
                    return Ok(new List<JobOrder>());
                }

                var jobOrderItems = await _Localdb.JobOrderItems
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(item => jobOrderIds.Contains(item.JobOrder_ID))
                    .ToListAsync();
                var deliveryReceipts = await _Localdb.DeliveryReceipt
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(receipt => receipt.JobOrderID.HasValue && jobOrderIds.Contains(receipt.JobOrderID.Value))
                    .ToListAsync();
                var deliveryReceiptIds = deliveryReceipts.Select(receipt => receipt.Delivery_ID).ToList();
                var deliveryReceiptItems = deliveryReceiptIds.Count == 0
                    ? new List<DeliveryReceiptItems>()
                    : await _Localdb.DeliveryReceiptItems
                        .AsNoTracking()
                        // Apply this predicate in the database query before materializing results.
                        .Where(item => deliveryReceiptIds.Contains(item.Delivery_ID))
                        .ToListAsync();
                var jobOrderIdByDeliveryId = deliveryReceipts
                    // Apply this predicate in the database query before materializing results.
                    .Where(receipt => receipt.JobOrderID.HasValue)
                    .ToDictionary(receipt => receipt.Delivery_ID, receipt => receipt.JobOrderID!.Value);
                var deliveredBranchesByJobOrderId = new Dictionary<int, HashSet<string>>();
                // Process each `receiptItem` returned by the collection expression in the loop header.
                foreach (var receiptItem in deliveryReceiptItems)
                {
                    // Skip this item and continue when `!jobOrderIdByDeliveryId.TryGetValue(receiptItem.Delivery_ID, out var jobOrderId) || string.IsNullOrWhiteSpace(receiptItem.Store)` is true.
                    if (!jobOrderIdByDeliveryId.TryGetValue(receiptItem.Delivery_ID, out var jobOrderId) ||
                        string.IsNullOrWhiteSpace(receiptItem.Store))
                    {
                        continue;
                    }

                    // Enter this branch when `!deliveredBranchesByJobOrderId.TryGetValue(jobOrderId, out var branches)` is true.
                    if (!deliveredBranchesByJobOrderId.TryGetValue(jobOrderId, out var branches))
                    {
                        branches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        deliveredBranchesByJobOrderId[jobOrderId] = branches;
                    }
                    branches.Add(receiptItem.Store.Trim());
                }

                var branchesByJobOrderId = jobOrderItems
                    // Apply this predicate in the database query before materializing results.
                    .Where(item => !string.IsNullOrWhiteSpace(item.Store))
                    .GroupBy(item => item.JobOrder_ID)
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .Select(item => item.Store!.Trim())
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList());
                var eligibleJobOrders = new List<JobOrder>();
                // Process each `jobOrder` returned by the collection expression in the loop header.
                foreach (var jobOrder in jobOrders)
                {
                    // Skip this item and continue when `!branchesByJobOrderId.TryGetValue(jobOrder.JobOrder_ID, out var branches)` is true.
                    if (!branchesByJobOrderId.TryGetValue(jobOrder.JobOrder_ID, out var branches))
                    {
                        continue;
                    }

                    deliveredBranchesByJobOrderId.TryGetValue(jobOrder.JobOrder_ID, out var deliveredBranches);
                    var remainingBranches = branches
                        // Apply this predicate in the database query before materializing results.
                        .Where(branch => deliveredBranches == null || !deliveredBranches.Contains(branch))
                        .ToList();
                    // Skip this item and continue when `remainingBranches.Count == 0` is true.
                    if (remainingBranches.Count == 0)
                    {
                        continue;
                    }

                    jobOrder.AvailableDeliveryReceiptBranches = remainingBranches;
                    eligibleJobOrders.Add(jobOrder);
                }

                return Ok(eligibleJobOrders);
            }

            var existingJobOrderIds = new HashSet<int>();
            // Select behavior based on `recordType?.Trim().ToLowerInvariant()`.
            switch (recordType?.Trim().ToLowerInvariant())
            {
                // Handle the `null` scenario.
                case null:
                // Handle the `""` scenario.
                case "":
                    break;
                // Handle the `"collection-receipt"` scenario.
                case "collection-receipt":
                    existingJobOrderIds.UnionWith(await _Localdb.CollectionReceipt
                        .AsNoTracking()
                        .Select(receipt => receipt.JobOrderID)
                        .ToListAsync());
                    break;
                // Handle the `"service-invoice"` scenario.
                case "service-invoice":
                    existingJobOrderIds.UnionWith(await _Localdb.ServiceInvoice
                        .AsNoTracking()
                        // Filter the query to rows satisfying `.Where(invoice => invoice.JobOrder_ID.HasValue) .Select(invoice => invoice.JobOrder_ID!.Value) .ToListAsync()` before materializing the results.
                        .Where(invoice => invoice.JobOrder_ID.HasValue)
                        .Select(invoice => invoice.JobOrder_ID!.Value)
                        .ToListAsync());
                    break;
                // Handle values not covered by the explicit switch cases.
                default:
                    // Coordinates the type API endpoints, including request handling and response generation.
                    return BadRequest("A valid Job Order record type is required.");
            }

            return Ok(jobOrders
                // Filter the query to rows satisfying `.Where(jobOrder => !existingJobOrderIds.Contains(jobOrder.JobOrder_ID)) .ToList()` before materializing the results.
                .Where(jobOrder => !existingJobOrderIds.Contains(jobOrder.JobOrder_ID))
                .ToList());
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

            // Return HTTP 404 Not Found when `!await GetMarketingVisibleJobOrders().AnyAsync(jobOrder => jobOrder.JobOrder_ID == jobOrderId)` is true.
            if (!await GetMarketingVisibleJobOrders().AnyAsync(jobOrder => jobOrder.JobOrder_ID == jobOrderId))
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

            // Return HTTP 404 Not Found when `!await GetMarketingVisibleJobOrders().AnyAsync(jobOrder => jobOrder.JobOrder_ID == request.JobOrder_ID)` is true.
            if (!await GetMarketingVisibleJobOrders().AnyAsync(jobOrder => jobOrder.JobOrder_ID == request.JobOrder_ID))
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

        /// <summary>Retrieves the installation report job orders.</summary>
        [HttpGet("GetInstallationReportJobOrders")]
        public async Task<ActionResult<IReadOnlyList<InstallationReportJobOrderOption>>> GetInstallationReportJobOrders()
        {
            var jobOrders = await _Localdb.JobOrder
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(jobOrder =>
                    jobOrder.Production_Completed ||
                    jobOrder.Status == "Completed" ||
                    jobOrder.Status == "Complete" ||
                    jobOrder.Status == "complited")
                // Apply the ordering expression here before returning or materializing the results.
                .OrderByDescending(jobOrder => jobOrder.JobOrder_ID)
                .ToListAsync();
            var existingReportJobOrderIds = await _Localdb.InstallationReport
                .AsNoTracking()
                .Select(report => report.JobOrder_ID)
                .Distinct()
                .ToListAsync();
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
                .ToDictionaryAsync(conforme => conforme.FileID);

            var quotationIds = conformes.Values.Select(conforme => conforme.Quotation_ID).Distinct().ToArray();
            var quotations = await _Localdb.Quotation
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(quotation => quotationIds.Contains(quotation.Quotation_ID))
                .Select(quotation => new { quotation.Quotation_ID, quotation.Client_ID, quotation.Title })
                .ToDictionaryAsync(quotation => quotation.Quotation_ID);

            var clientIds = quotations.Values.Select(quotation => quotation.Client_ID).Distinct().ToArray();
            var clients = await _Localdb.Clients
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(client => clientIds.Contains(client.Client_ID))
                .Select(client => new { client.Client_ID, client.Client_Name, client.Company_Address })
                .ToDictionaryAsync(client => client.Client_ID);

            var options = jobOrders.Select(jobOrder =>
            {
                // Combines job-order, client, quotation, and conforme data into a display option.
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
        public async Task<ActionResult> AddLocalInstallationReport([FromBody] CreateInstallationReportRequest request)
        {
            // Exit the current operation when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var jobOrderExists = await _Localdb.JobOrder
                .AsNoTracking()
                .AnyAsync(jobOrder => jobOrder.JobOrder_ID == request.JobOrderId);
            // Return HTTP 404 Not Found when `!jobOrderExists` is true.
            if (!jobOrderExists)
            {
                return NotFound("The selected Job Order was not found.");
            }
            // Return HTTP 409 Conflict when `await _Localdb.InstallationReport .AsNoTracking() .AnyAsync(report => report.JobOrder_ID == request.JobOrderId)` is true.
            if (await _Localdb.InstallationReport
                .AsNoTracking()
                .AnyAsync(report => report.JobOrder_ID == request.JobOrderId))
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

            await using var transaction = await _Localdb.Database.BeginTransactionAsync();
            _Localdb.InstallationReport.Add(report);
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            // Process each `signatureFile` returned by the collection expression in the loop header.
            foreach (var signatureFile in signatureFiles)
            {
                signatureFile.InstallationReport_ID = report.InstallationReport_ID;
            }
            await _Localdb.InstallationReportFiles.AddRangeAsync(signatureFiles);
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                installationReportId = report.InstallationReport_ID,
                jobOrderId = report.JobOrder_ID
            });
        }

        /// <summary>Updates the local installation report.</summary>
        [HttpPost("UpdateLocalInstallationReport")]
        public async Task<ActionResult<InstallationReport>> UpdateLocalInstallationReport(
            [FromQuery] int Id,
            [FromBody] InstallationReport report)
        {
            // Enter this branch when `Id <= 0 || report == null` is true.
            if (Id <= 0 || report == null)
            {
                // Coordinates the are API endpoints, including request handling and response generation.
                return BadRequest("A valid installation report ID and record are required.");
            }

            // Return HTTP 409 Conflict when `await _Localdb.InstallationReportFiles .IgnoreQueryFilters() .AnyAsync(file => file.InstallationReport_ID == Id && file.File_Role == )` is true.
            if (await _Localdb.InstallationReportFiles
                .IgnoreQueryFilters()
                .AnyAsync(file =>
                    file.InstallationReport_ID == Id &&
                    file.File_Role == "ApproverSigned"))
            {
                return Conflict("An approved Installation Report cannot be edited.");
            }

            var savedReport = await _Localdb.InstallationReport
                .FirstOrDefaultAsync(record => record.InstallationReport_ID == Id);
            // Return HTTP 404 Not Found when `savedReport == null` is true.
            if (savedReport == null)
            {
                return NotFound("Installation report not found.");
            }
            // Return HTTP 400 Bad Request when `!await _Localdb.JobOrder .AnyAsync(jobOrder => jobOrder.JobOrder_ID == report.JobOrder_ID)` is true.
            if (!await _Localdb.JobOrder
                .AnyAsync(jobOrder => jobOrder.JobOrder_ID == report.JobOrder_ID))
            {
                return BadRequest("The selected Job Order was not found.");
            }

            savedReport.JobOrder_ID = report.JobOrder_ID;
            savedReport.ClientName = report.ClientName;
            savedReport.ProjectName = report.ProjectName;
            savedReport.Location = report.Location;
            savedReport.ReportDate = report.ReportDate;
            savedReport.TimeStarted = report.TimeStarted;
            savedReport.TimeEnded = report.TimeEnded;
            savedReport.Installers = report.Installers;
            savedReport.OnTime = report.OnTime;
            savedReport.UniformAndId = report.UniformAndId;
            savedReport.GraphicsProperlyInstalled = report.GraphicsProperlyInstalled;
            savedReport.ProblemsOccurred = report.ProblemsOccurred;
            savedReport.ProblemsNote = report.ProblemsNote;
            savedReport.AreaClean = report.AreaClean;
            savedReport.InstallersMannered = report.InstallersMannered;
            savedReport.ClientRemarks = report.ClientRemarks;
            savedReport.Obstruction = report.Obstruction;
            savedReport.ObstructionDetails = report.ObstructionDetails;
            savedReport.GraphicsDamaged = report.GraphicsDamaged;
            savedReport.DamageDetails = report.DamageDetails;
            savedReport.InstallerRemarks = report.InstallerRemarks;
            savedReport.ClientSignature = report.ClientSignature;
            savedReport.PreparedBy = report.PreparedBy;
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            return Ok(savedReport);
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
            var jobOrder = await GetMarketingVisibleJobOrders()
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
            // Return HTTP 404 Not Found when `!await GetMarketingVisibleJobOrders() .AnyAsync(jobOrder => jobOrder.JobOrder_ID == jobOrderId, cancellationToken)` is true.
            if (!await GetMarketingVisibleJobOrders()
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

        /// <summary>Adds the local job order.</summary>
        [HttpPost("AddLocalJobOrder")]
        public async Task<ActionResult<JobOrder>> AddLocalJobOrder([FromBody] CreateJobOrderRequest request)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var targetDeliveryError = WorkflowRules.ValidateTargetDelivery(request.Target_Delivery);
            // Return HTTP 400 Bad Request when `targetDeliveryError != null` is true.
            if (targetDeliveryError != null)
            {
                return BadRequest(targetDeliveryError);
            }

            var specialInstruction = request.SpecialInstruction?.Trim();
            var employeeName = string.IsNullOrWhiteSpace(specialInstruction)
                ? ""
                : _Localdb.GetCurrentEmployeeName().Trim();
            // Return HTTP 400 Bad Request when `!string.IsNullOrWhiteSpace(specialInstruction) && string.IsNullOrWhiteSpace(employeeName)` is true.
            if (!string.IsNullOrWhiteSpace(specialInstruction) && string.IsNullOrWhiteSpace(employeeName))
            {
                return BadRequest("The signed-in employee name is unavailable, so the special instruction cannot be saved.");
            }

            var conforme = _Localdb.Conforme.FirstOrDefault(x => x.FileID == request.Conforme_FileID);
            // Return HTTP 400 Bad Request when `conforme == null` is true.
            if (conforme == null)
            {
                return BadRequest($"Conforme {request.Conforme_FileID} was not found.");
            }

            var jobOrder = new JobOrder
            {
                Conforme_FileID = request.Conforme_FileID,
                Title = request.Title,
                Installation_Date = null,
                Target_Delivery = request.Target_Delivery,
                Date_Delivered = null,
                Tiling = request.Tiling,
                Eyelet = request.Eyelet,
                Bleeding = request.Bleeding,
                Status = "Pending",
                Technical_Completed = false,
                Production_Completed = false
            };
            // Enter this branch when `!string.IsNullOrWhiteSpace(specialInstruction)` is true.
            if (!string.IsNullOrWhiteSpace(specialInstruction))
            {
                jobOrder.Comments.Add(new JobOrderComment
                {
                    Employee_Name = employeeName,
                    Comment = specialInstruction,
                    CreatedAt = DateTime.UtcNow
                });
            }

            _Localdb.Add(jobOrder);
            conforme.Status = "Completed";
            // Process each `purchaseOrder` from `_Localdb.PurchaseOrder.Where(x => x.Conforme_FileID == request.Conforme_FileID` using the operation in the loop body.
            // Filter the query to rows satisfying `Order.Where(x => x.Conforme_FileID == request.Conforme_FileID)` before materializing the results.
            foreach (var purchaseOrder in _Localdb.PurchaseOrder.Where(x => x.Conforme_FileID == request.Conforme_FileID))
            {
                purchaseOrder.Status = "Completed";
            }
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            await _notificationService.NotifyJobOrderReadersAsync(jobOrder.JobOrder_ID);
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

            updateJobOrder.Title = _jobOrder.Title;
            updateJobOrder.Target_Delivery = _jobOrder.Target_Delivery;
            updateJobOrder.Tiling = _jobOrder.Tiling;
            updateJobOrder.Eyelet = _jobOrder.Eyelet;
            updateJobOrder.Bleeding = _jobOrder.Bleeding;
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

            var conforme = _CloudDb.Conforme.FirstOrDefault(x => x.FileID == jobOrder.Conforme_FileID);
            // Return HTTP 400 Bad Request when `conforme == null` is true.
            if (conforme == null)
            {
                return BadRequest($"Conforme {jobOrder.Conforme_FileID} was not found.");
            }

            _CloudDb.Add(jobOrder);
            conforme.Status = "Completed";
            // Process each `purchaseOrder` from `_CloudDb.PurchaseOrder.Where(x => x.Conforme_FileID == jobOrder.Conforme_FileID` using the operation in the loop body.
            // Filter the query to rows satisfying `Order.Where(x => x.Conforme_FileID == jobOrder.Conforme_FileID)` before materializing the results.
            foreach (var purchaseOrder in _CloudDb.PurchaseOrder.Where(x => x.Conforme_FileID == jobOrder.Conforme_FileID))
            {
                purchaseOrder.Status = "Completed";
            }
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

        // ------------------------------- JOB ORDER -------------------------------

        // ------------------------------- JOB ORDER ITEMS -------------------------------

        [HttpGet("GetLocalJobOrderItems")]
        // Retrieves all job order items. Returns List<JobOrderItems>.
        public List<JobOrderItems> GetAllJobOrderItems()
        {
            return _Localdb.JobOrderItems.ToList();
        }

        /// <summary>Retrieves the local job order items by id.</summary>
        [HttpGet("GetLocalJobOrderItemsByID")]
        public ActionResult<JobOrderItems> GetLocalJobOrderItemsByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var jobOrder = _Localdb.JobOrderItems.FirstOrDefault(x => x.JobOrderItems_ID == id);
            // Return HTTP 404 Not Found when `jobOrder == null` is true.
            if (jobOrder == null)
            {
                return NotFound();
            }
            return Ok(jobOrder);
        }

        /// <summary>Adds the local job order items.</summary>
        [HttpPost("AddLocalJobOrderItems")]
        public ActionResult<JobOrderItems> AddLocalJobOrderItems(JobOrderItems jobOrderItems)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(jobOrderItems);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(jobOrderItems);
        }

        /// <summary>Updates the local job order.</summary>
        [HttpPost("UpdateLocalJobOrderItems")]
        public ActionResult<JobOrderItems> UpdateLocalJobOrder(Int32 Id, [FromBody] JobOrderItems _jobOrderItems)
        {
            // Return HTTP 400 Bad Request when `_jobOrderItems == null` is true.
            if (_jobOrderItems == null)
            {
                return BadRequest(_jobOrderItems);
            }
            var updateJobOrderItems = _Localdb.JobOrderItems.FirstOrDefault(x => x.JobOrderItems_ID == Id);
            // Return HTTP 404 Not Found when `updateJobOrderItems == null` is true.
            if (updateJobOrderItems == null)
            {
                return NotFound();
            }

            // Enter this branch when `_jobOrderItems.QI_ID.HasValue` is true.
            if (_jobOrderItems.QI_ID.HasValue)
            {
                updateJobOrderItems.QI_ID = _jobOrderItems.QI_ID;
            }
            updateJobOrderItems.Store = _jobOrderItems.Store;
            updateJobOrderItems.Requirements = _jobOrderItems.Requirements;
            updateJobOrderItems.Description = _jobOrderItems.Description;
            updateJobOrderItems.Width = _jobOrderItems.Width;
            updateJobOrderItems.Length = _jobOrderItems.Length;
            updateJobOrderItems.Quantity = _jobOrderItems.Quantity;
            updateJobOrderItems.Production_Initial = _jobOrderItems.Production_Initial;
            updateJobOrderItems.Artist_Initial = _jobOrderItems.Artist_Initial;
            updateJobOrderItems.Remarks = _jobOrderItems.Remarks;


            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(updateJobOrderItems);
        }

        /// <summary>Deletes the local job order items.</summary>
        [HttpPut("DeleteLocalJobOrderItems")]
        public ActionResult<JobOrderItems> DeleteLocalJobOrderItems(int id)
        {
            var jobOrderItems = _Localdb.JobOrderItems.FirstOrDefault(x => x.JobOrderItems_ID == id);
            // Return HTTP 404 Not Found when `jobOrderItems == null` is true.
            if (jobOrderItems == null)
            {
                return NotFound();
            }
            _Localdb.Remove(jobOrderItems);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }


        /// <summary>Retrieves all job-order items from the cloud database.</summary>
        [HttpGet("GetCloudJobOrderItems")]
        public List<JobOrderItems> GetAllCloudJobOrderItems()
        {
            return _Localdb.JobOrderItems.ToList();
        }

        /// <summary>Retrieves the cloud job order items by id.</summary>
        [HttpGet("GetCloudJobOrderItemsByID")]
        public ActionResult<JobOrderItems> GetCloudJobOrderItemsByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var jobOrder = _CloudDb.JobOrderItems.FirstOrDefault(x => x.JobOrderItems_ID == id);
            // Return HTTP 404 Not Found when `jobOrder == null` is true.
            if (jobOrder == null)
            {
                return NotFound();
            }
            return Ok(jobOrder);
        }

        /// <summary>Adds the cloud job order items.</summary>
        [HttpPost("AddCloudJobOrderItems")]
        public ActionResult<JobOrderItems> AddCloudJobOrderItems(JobOrderItems jobOrderItems)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(jobOrderItems);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(jobOrderItems);
        }

        /// <summary>Updates the cloud job order items.</summary>
        [HttpPost("UpdateCloudJobOrderItems")]
        public ActionResult<JobOrderItems> UpdateCloudJobOrderItems(Int32 Id, [FromBody] JobOrderItems _jobOrderItems)
        {
            // Return HTTP 400 Bad Request when `_jobOrderItems == null` is true.
            if (_jobOrderItems == null)
            {
                return BadRequest(_jobOrderItems);
            }
            var updateJobOrderItems = _CloudDb.JobOrderItems.FirstOrDefault(x => x.JobOrderItems_ID == Id);
            // Return HTTP 404 Not Found when `updateJobOrderItems == null` is true.
            if (updateJobOrderItems == null)
            {
                return NotFound();
            }

            updateJobOrderItems.Production_Initial = _jobOrderItems.Production_Initial;
            updateJobOrderItems.Artist_Initial = _jobOrderItems.Artist_Initial;
            updateJobOrderItems.Remarks = _jobOrderItems.Remarks;



            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(updateJobOrderItems);
        }

        /// <summary>Deletes the cloud job order items.</summary>
        [HttpPut("DeleteCloudJobOrderItems")]
        public ActionResult<JobOrderItems> DeleteCloudJobOrderItems(int id)
        {
            var jobOrderItems = _CloudDb.JobOrderItems.FirstOrDefault(x => x.JobOrderItems_ID == id);
            // Return HTTP 404 Not Found when `jobOrderItems == null` is true.
            if (jobOrderItems == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(jobOrderItems);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- JOB ORDER ITEMS -------------------------------


        // ------------------------------- PURCHASE ORDER -------------------------------

        [HttpGet("GetLocalPurchaseOrder")]
        // Retrieves all purchase order. Returns List<PurchaseOrder>.
        public List<PurchaseOrder> GetAllPurchaseOrder()
        {
            var list = _Localdb.PurchaseOrder.AsNoTracking().ToList();
            // Process each `r` returned by the collection expression in the loop header.
            foreach (var r in list) r.FileData = _encryptionService.TryDecrypt(r.FileData);
            return list;
        }

        /// <summary>Retrieves the local purchase order by id.</summary>
        [HttpGet("GetLocalPurchaseOrderByID")]
        public ActionResult<PurchaseOrder> GetLocalPurchaseOrderByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var purchaseOrder = _Localdb.PurchaseOrder.AsNoTracking().FirstOrDefault(x => x.FileID == id);
            // Return HTTP 404 Not Found when `purchaseOrder == null` is true.
            if (purchaseOrder == null) return NotFound();
            purchaseOrder.FileData = _encryptionService.TryDecrypt(purchaseOrder.FileData);
            return Ok(purchaseOrder);
        }

        /// <summary>Adds the local purchase order.</summary>
        [HttpPost("AddLocalPurchaseOrder")]
        public ActionResult<PurchaseOrder> AddLocalPurchaseOrder(PurchaseOrder purchaseOrder)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var error = UploadValidator.ValidatePdf(purchaseOrder.FileData);
            // Return HTTP 400 Bad Request when `error != null` is true.
            if (error != null) return BadRequest(error);

            purchaseOrder.Status = _Localdb.JobOrder.Any(
                jobOrder => jobOrder.Conforme_FileID == purchaseOrder.Conforme_FileID)
                ? "Completed"
                : "Pending";
            var plain = purchaseOrder.FileData;
            purchaseOrder.FileData = _encryptionService.Encrypt(plain);
            _Localdb.PurchaseOrder.Add(purchaseOrder);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            purchaseOrder.FileData = plain;
            return Ok(purchaseOrder);
        }

        /// <summary>Adds the local purchase order batch.</summary>
        [HttpPost("AddLocalPurchaseOrderBatch")]
        [RequestSizeLimit(100_000_000)]
        public ActionResult<List<int>> AddLocalPurchaseOrderBatch([FromBody] List<PurchaseOrder> purchaseOrders)
        {
            // Return HTTP 400 Bad Request when `purchaseOrders == null || purchaseOrders.Count == 0` is true.
            if (purchaseOrders == null || purchaseOrders.Count == 0)
                return BadRequest("No files.");

            // Repeat the body while `the loop condition` remains true.
            for (int i = 0; i < purchaseOrders.Count; i++)
            {
                var error = UploadValidator.ValidatePdf(purchaseOrders[i].FileData);
                // Return HTTP 400 Bad Request when `error != null` is true.
                if (error != null) return BadRequest($"File {i + 1}: {error}");
            }

            // Process each `f` returned by the collection expression in the loop header.
            foreach (var f in purchaseOrders)
            {
                f.Status = _Localdb.JobOrder.Any(
                    jobOrder => jobOrder.Conforme_FileID == f.Conforme_FileID)
                    ? "Completed"
                    : "Pending";
                f.FileData = _encryptionService.Encrypt(f.FileData);
                _Localdb.PurchaseOrder.Add(f);
            }
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(purchaseOrders.Select(f => f.FileID).ToList());
        }

        /// <summary>Updates the local purchase order.</summary>
        [HttpPost("UpdateLocalPurchaseOrder")]
        [RequestSizeLimit(10_000_000)]
        public ActionResult<PurchaseOrder> UpdateLocalPurchaseOrder(Int32 Id, [FromBody] PurchaseOrder _purchaseOrder)
        {
            // Return HTTP 400 Bad Request when `_purchaseOrder == null` is true.
            if (_purchaseOrder == null)
            {
                return BadRequest(_purchaseOrder);
            }
            var updatePurchaseOrder = _Localdb.PurchaseOrder.FirstOrDefault(x => x.FileID == Id);
            // Return HTTP 404 Not Found when `updatePurchaseOrder == null` is true.
            if (updatePurchaseOrder == null)
            {
                return NotFound();
            }

            // Enter this branch when `_purchaseOrder.FileData is { Length: > 0 }` is true.
            if (_purchaseOrder.FileData is { Length: > 0 })
            {
                var error = UploadValidator.ValidatePdf(_purchaseOrder.FileData);
                // Return HTTP 400 Bad Request when `error != null` is true.
                if (error != null)
                {
                    return BadRequest(error);
                }
                updatePurchaseOrder.FileData = _encryptionService.Encrypt(_purchaseOrder.FileData);
            }
            updatePurchaseOrder.FileName = _purchaseOrder.FileName;
            updatePurchaseOrder.FileExtension = _purchaseOrder.FileExtension;
            updatePurchaseOrder.Status = _Localdb.JobOrder.Any(
                jobOrder => jobOrder.Conforme_FileID == updatePurchaseOrder.Conforme_FileID)
                ? "Completed"
                : "Pending";

            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(updatePurchaseOrder);
        }

        /// <summary>Deletes the local purchase order.</summary>
        [HttpPut("DeleteLocalPurchaseOrder")]
        public ActionResult<PurchaseOrder> DeleteLocalPurchaseOrder(int id)
        {
            var purchaseOrder = _Localdb.PurchaseOrder.FirstOrDefault(x => x.FileID == id);
            // Return HTTP 404 Not Found when `purchaseOrder == null` is true.
            if (purchaseOrder == null)
            {
                return NotFound();
            }
            _Localdb.Remove(purchaseOrder);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }


        /// <summary>Retrieves all purchase orders from the cloud database.</summary>
        [HttpGet("GetCloudPurchaseOrder")]
        public List<PurchaseOrder> GetAllCloudPurchaseOrder()
        {
            return _CloudDb.PurchaseOrder.ToList();
        }

        /// <summary>Retrieves the cloud purchase order by id.</summary>
        [HttpGet("GetCloudPurchaseOrderByID")]
        public ActionResult<PurchaseOrder> GetCloudPurchaseOrderByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var purchaseOrder = _CloudDb.PurchaseOrder.FirstOrDefault(x => x.FileID == id);
            // Return HTTP 404 Not Found when `purchaseOrder == null` is true.
            if (purchaseOrder == null)
            {
                return NotFound();
            }
            return Ok(purchaseOrder);
        }

        /// <summary>Adds the cloud purchase order.</summary>
        [HttpPost("AddCloudPurchaseOrder")]
        public ActionResult<PurchaseOrder> AddCloudPurchaseOrder(PurchaseOrder purchaseOrder)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            purchaseOrder.Status = _CloudDb.JobOrder.Any(
                jobOrder => jobOrder.Conforme_FileID == purchaseOrder.Conforme_FileID)
                ? "Completed"
                : "Pending";
            _CloudDb.Add(purchaseOrder);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(purchaseOrder);
        }

        /// <summary>Updates the cloud purchase order.</summary>
        [HttpPost("UpdateCloudPurchaseOrder")]
        public ActionResult<PurchaseOrder> UpdateCloudPurchaseOrder(Int32 Id, [FromBody] PurchaseOrder _purchaseOrder)
        {
            // Return HTTP 400 Bad Request when `_purchaseOrder == null` is true.
            if (_purchaseOrder == null)
            {
                return BadRequest(_purchaseOrder);
            }
            var updatePurchaseOrder = _CloudDb.PurchaseOrder.FirstOrDefault(x => x.FileID == Id);
            // Return HTTP 404 Not Found when `updatePurchaseOrder == null` is true.
            if (updatePurchaseOrder == null)
            {
                return NotFound();
            }
            updatePurchaseOrder.Conforme_FileID = _purchaseOrder.Conforme_FileID;
            updatePurchaseOrder.FileName = _purchaseOrder.FileName;
            updatePurchaseOrder.FileExtension = _purchaseOrder.FileExtension;
            updatePurchaseOrder.FileData = _purchaseOrder.FileData;
            updatePurchaseOrder.Status = _CloudDb.JobOrder.Any(
                jobOrder => jobOrder.Conforme_FileID == updatePurchaseOrder.Conforme_FileID)
                ? "Completed"
                : "Pending";
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(updatePurchaseOrder);
        }

        /// <summary>Deletes the cloud purchase order.</summary>
        [HttpPut("DeleteCloudPurchaseOrder")]
        public ActionResult<PurchaseOrder> DeleteCloudPurchaseOrder(int id)
        {
            var purchaseOrder = _CloudDb.PurchaseOrder.FirstOrDefault(x => x.FileID == id);
            // Return HTTP 404 Not Found when `purchaseOrder == null` is true.
            if (purchaseOrder == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(purchaseOrder);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- PURCHASE ORDER -------------------------------

        // ------------------------------- GRAPHICS -------------------------------

        [HttpGet("GetLocalGraphics")]
        // Retrieves all graphics. Returns List<Graphics>.
        public List<Graphics> GetAllGraphics()
        {
            var list = _Localdb.Graphics.AsNoTracking().ToList();
            // Process each `r` returned by the collection expression in the loop header.
            foreach (var r in list) r.FileData = _encryptionService.TryDecrypt(r.FileData);
            return list;
        }

        /// <summary>Retrieves the local graphics quotation i ds.</summary>
        [HttpGet("GetLocalGraphicsQuotationIDs")]
        public List<int> GetLocalGraphicsQuotationIDs()
        {
            return _Localdb.Graphics
                .AsNoTracking()
                .Select(graphic => graphic.Quotation_ID)
                .Distinct()
                .ToList();
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
            var graphics = _Localdb.Graphics.AsNoTracking().FirstOrDefault(x => x.FileID == id);
            // Return HTTP 404 Not Found when `graphics == null` is true.
            if (graphics == null) return NotFound();
            graphics.FileData = _encryptionService.TryDecrypt(graphics.FileData);
            return Ok(graphics);
        }

        /// <summary>Adds the local graphics.</summary>
        [HttpPost("AddLocalGraphics")]
        public ActionResult<Graphics> AddLocalGraphics(Graphics graphics)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var error = UploadValidator.ValidatePdf(graphics.FileData);   // ValidateImage for Collection Receipt, ValidateImageOrPdf for Graphics, ValidateImage for Delivery Files
            // Return HTTP 400 Bad Request when `error != null` is true.
            if (error != null) return BadRequest(error);

            var plain = graphics.FileData;
            graphics.FileData = _encryptionService.Encrypt(plain);
            _Localdb.Graphics.Add(graphics);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            graphics.FileData = plain;
            return Ok(graphics);
        }

        /// <summary>Adds the local graphics batch.</summary>
        [HttpPost("AddLocalGraphicsBatch")]
        [RequestSizeLimit(100_000_000)]
        public ActionResult<List<int>> AddLocalGraphicsBatch([FromBody] List<Graphics> graphicsFiles)
        {
            // Return HTTP 400 Bad Request when `graphicsFiles == null || graphicsFiles.Count == 0` is true.
            if (graphicsFiles == null || graphicsFiles.Count == 0)
                return BadRequest("No files.");

            // Repeat the body while `the loop condition` remains true.
            for (int i = 0; i < graphicsFiles.Count; i++)
            {
                var graphics = graphicsFiles[i];
                // Enter this branch when `graphics == null` is true.
                if (graphics == null)
                    // Coordinates the is API endpoints, including request handling and response generation.
                    return BadRequest($"File {i + 1}: A graphics record is required.");

                // Return HTTP 400 Bad Request when `graphics.Quotation_ID <= 0 || !_Localdb.Quotation.AsNoTracking().Any(quotation => quotation.Quotation_ID == graphics.Quotation_ID)` is true.
                if (graphics.Quotation_ID <= 0 ||
                    !_Localdb.Quotation.AsNoTracking().Any(quotation => quotation.Quotation_ID == graphics.Quotation_ID))
                    return BadRequest($"File {i + 1}: Select a valid quotation.");

                var link = graphics.Link?.Trim();
                // Return HTTP 400 Bad Request when `string.IsNullOrWhiteSpace(link) || link.Length > 2048 || !Uri.TryCreate(link, UriKind.Absolute, out var parsedLink) || (parsedLink.Scheme != Uri.UriSchemeHttp && parsedLink.Scheme != Uri.UriSchemeHttps)` is true.
                if (string.IsNullOrWhiteSpace(link) || link.Length > 2048 ||
                    !Uri.TryCreate(link, UriKind.Absolute, out var parsedLink) ||
                    (parsedLink.Scheme != Uri.UriSchemeHttp && parsedLink.Scheme != Uri.UriSchemeHttps))
                    return BadRequest($"File {i + 1}: A valid HTTP or HTTPS link is required.");
                graphics.Link = link;

                var error = UploadValidator.ValidateImageOrPdf(graphics.FileData);
                // Return HTTP 400 Bad Request when `error != null` is true.
                if (error != null) return BadRequest($"File {i + 1}: {error}");
            }

            // Process each `f` returned by the collection expression in the loop header.
            foreach (var f in graphicsFiles)
            {
                f.FileData = _encryptionService.Encrypt(f.FileData);
                _Localdb.Graphics.Add(f);
            }
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(graphicsFiles.Select(f => f.FileID).ToList());
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

            updateGraphics.FileData = _encryptionService.Encrypt(_graphics.FileData);
            // Enter this branch when `_graphics.Link != null` is true.
            if (_graphics.Link != null)
            {
                updateGraphics.Link = _graphics.Link;
            }

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

        // ------------------------------- GRAPHICS -------------------------------

        // ------------------------------- SERVICE INVOICE ---------------------------

        [HttpGet("GetLocalServiceInvoice")]
        // Retrieves all local service invoice. Returns List<ServiceInvoice>.
        public List<ServiceInvoice> GetAllLocalServiceInvoice()
        {
            return _Localdb.ServiceInvoice.ToList();
        }

        /// <summary>Retrieves the local service invoice id.</summary>
        [HttpGet("GetLocalServiceInvoiceByID")]
        public ActionResult<ServiceInvoice> GetLocalServiceInvoiceID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid service invoice ID.");
            }
            var serviceInvoice = _Localdb.ServiceInvoice.FirstOrDefault(x => x.ServiceInvoice_ID == id);
            // Return HTTP 404 Not Found when `serviceInvoice == null` is true.
            if (serviceInvoice == null)
            {
                return NotFound();
            }
            return Ok(serviceInvoice);
        }

        /// <summary>Retrieves the local service invoice job order items.</summary>
        [HttpGet("GetLocalServiceInvoiceJobOrderItems")]
        public async Task<ActionResult<List<ServiceInvoiceJobOrderItemDto>>> GetLocalServiceInvoiceJobOrderItems(int jobOrderId)
        {
            // Return HTTP 400 Bad Request when `jobOrderId <= 0` is true.
            if (jobOrderId <= 0)
            {
                return BadRequest("Invalid Job Order ID.");
            }

            var jobOrder = await _Localdb.JobOrder
                .AsNoTracking()
                .FirstOrDefaultAsync(order => order.JobOrder_ID == jobOrderId);
            // Return HTTP 404 Not Found when `jobOrder is null` is true.
            if (jobOrder is null)
            {
                return NotFound("Job Order not found.");
            }

            var jobOrderItems = await _Localdb.JobOrderItems
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(item => item.JobOrder_ID == jobOrderId)
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(item => item.JobOrderItems_ID)
                .ToListAsync();
            // Exit the current operation when `jobOrderItems.Count == 0` is true.
            if (jobOrderItems.Count == 0)
            {
                return Ok(new List<ServiceInvoiceJobOrderItemDto>());
            }

            var quotationItems = new List<Quotation_Items>();
            var storeNamesById = new Dictionary<int, string>();
            var conforme = await _Localdb.Conforme
                .AsNoTracking()
                .FirstOrDefaultAsync(file => file.FileID == jobOrder.Conforme_FileID);
            // Enter this branch when `conforme is not null` is true.
            if (conforme is not null)
            {
                var stores = await _Localdb.Quotation_Store
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(store => store.Quotation_ID == conforme.Quotation_ID)
                    .ToListAsync();
                storeNamesById = stores.ToDictionary(store => store.QS_ID, store => store.Store ?? string.Empty);
                var storeIds = stores.Select(store => store.QS_ID).ToList();
                quotationItems = await _Localdb.Quotation_Items
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(item => storeIds.Contains(item.QS_ID))
                    // Apply the ordering expression here before returning or materializing the results.
                    .OrderBy(item => item.QI_ID)
                    .ToListAsync();
            }

            var matchedQuotationItemIds = new HashSet<int>();
            var result = new List<ServiceInvoiceJobOrderItemDto>(jobOrderItems.Count);
            // Process each `jobOrderItem` returned by the collection expression in the loop header.
            foreach (var jobOrderItem in jobOrderItems)
            {
                var quotationItem = jobOrderItem.QI_ID.HasValue
                    ? quotationItems.FirstOrDefault(item => item.QI_ID == jobOrderItem.QI_ID.Value)
                    : null;

                // Enter this branch when `quotationItem is null` is true.
                if (quotationItem is null)
                {
                    // Apply this predicate in the database query before materializing results.
                    var matchingItems = quotationItems.Where(item =>
                        !matchedQuotationItemIds.Contains(item.QI_ID) &&
                        storeNamesById.TryGetValue(item.QS_ID, out var storeName) &&
                        string.Equals(storeName.Trim(), jobOrderItem.Store?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(item.Requirements?.Trim(), jobOrderItem.Requirements?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(item.Description?.Trim(), jobOrderItem.Description?.Trim(), StringComparison.OrdinalIgnoreCase));

                    quotationItem = matchingItems.FirstOrDefault(item =>
                        !jobOrderItem.Quantity.HasValue ||
                        item.Quantity == jobOrderItem.Quantity.Value)
                        ?? matchingItems.FirstOrDefault();
                }

                // Enter this branch when `quotationItem is not null` is true.
                if (quotationItem is not null)
                {
                    matchedQuotationItemIds.Add(quotationItem.QI_ID);
                }

                var dimensions = string.IsNullOrWhiteSpace(jobOrderItem.Width) &&
                                 string.IsNullOrWhiteSpace(jobOrderItem.Length)
                    ? null
                    : $"{jobOrderItem.Width} x {jobOrderItem.Length}";
                var description = string.Join(" - ", new[]
                {
                    jobOrderItem.Requirements,
                    jobOrderItem.Description,
                    dimensions
                // Filter the query to rows satisfying `}.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim())` before materializing the results.
                }.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()));

                result.Add(new ServiceInvoiceJobOrderItemDto
                {
                    Quantity = jobOrderItem.Quantity,
                    Unit = quotationItem?.Measurement_Unit?.Trim() ?? string.Empty,
                    Description = description,
                    Unit_Price = 0,
                    Amount = 0
                });
            }

            return Ok(result);
        }

        /// <summary>Adds the local service invoice.</summary>
        [HttpPost("AddLocalServiceInvoice")]
        public ActionResult<ServiceInvoice> AddLocalServiceInvoice([FromBody] ServiceInvoice serviceInvoice)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            // Return HTTP 400 Bad Request when `serviceInvoice.JobOrder_ID is not > 0` is true.
            if (serviceInvoice.JobOrder_ID is not > 0)
            {
                return BadRequest("A valid Job Order is required for a Service Invoice.");
            }
            // Return HTTP 404 Not Found when `!_Localdb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == serviceInvoice.JobOrder_ID)` is true.
            if (!_Localdb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == serviceInvoice.JobOrder_ID))
            {
                return NotFound("The selected Job Order was not found.");
            }
            // Return HTTP 409 Conflict when `_Localdb.ServiceInvoice.Any(invoice => invoice.JobOrder_ID == serviceInvoice.JobOrder_ID)` is true.
            if (_Localdb.ServiceInvoice.Any(invoice => invoice.JobOrder_ID == serviceInvoice.JobOrder_ID))
            {
                return Conflict("A Service Invoice already exists for the selected Job Order.");
            }
            serviceInvoice.Status = "Completed";
            // Enter this branch when `serviceInvoice.Invoice_Date == default` is true.
            if (serviceInvoice.Invoice_Date == default)
            {
                serviceInvoice.Invoice_Date = DateOnly.FromDateTime(DateTime.Today);
            }
            _Localdb.ServiceInvoice.Add(serviceInvoice);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(serviceInvoice);
        }

        /// <summary>Adds the local service invoice with items.</summary>
        [HttpPost("AddLocalServiceInvoiceWithItems")]
        public async Task<ActionResult<ServiceInvoice>> AddLocalServiceInvoiceWithItems(
            [FromBody] CreateServiceInvoiceDto submission)
        {
            // Return HTTP 400 Bad Request when `submission.JobOrder_ID <= 0 || !await _Localdb.JobOrder.AnyAsync(jobOrder => jobOrder.JobOrder_ID == submission.JobOrder_ID)` is true.
            if (submission.JobOrder_ID <= 0 ||
                !await _Localdb.JobOrder.AnyAsync(jobOrder => jobOrder.JobOrder_ID == submission.JobOrder_ID))
            {
                return BadRequest("Select a valid saved Job Order.");
            }
            // Return HTTP 400 Bad Request when `string.IsNullOrWhiteSpace(submission.Branch) || string.IsNullOrWhiteSpace(submission.Title) || string.IsNullOrWhiteSpace(submission.ClientName) || string.IsNullOrWhiteSpace(submission.Address) || string.IsNullOrWhiteSpace(submission.Terms)` is true.
            if (string.IsNullOrWhiteSpace(submission.Branch) ||
                string.IsNullOrWhiteSpace(submission.Title) ||
                string.IsNullOrWhiteSpace(submission.ClientName) ||
                string.IsNullOrWhiteSpace(submission.Address) ||
                string.IsNullOrWhiteSpace(submission.Terms))
            {
                return BadRequest("Branch, project title, client, address, and terms are required.");
            }
            var branch = submission.Branch.Trim();
            // Return HTTP 400 Bad Request when `!await _Localdb.JobOrderItems.AnyAsync(item => item.JobOrder_ID == submission.JobOrder_ID && item.Store == branch)` is true.
            if (!await _Localdb.JobOrderItems.AnyAsync(item =>
                    item.JobOrder_ID == submission.JobOrder_ID &&
                    item.Store == branch))
            {
                return BadRequest("The selected Branch does not belong to this Job Order.");
            }
            // Return HTTP 409 Conflict when `await _Localdb.ServiceInvoice .AsNoTracking() .AnyAsync(invoice => invoice.JobOrder_ID == submission.JobOrder_ID)` is true.
            if (await _Localdb.ServiceInvoice
                .AsNoTracking()
                .AnyAsync(invoice => invoice.JobOrder_ID == submission.JobOrder_ID))
            {
                return Conflict("A Service Invoice already exists for the selected Job Order.");
            }
            // Return HTTP 400 Bad Request when `submission.Items is null || submission.Items.Count == 0` is true.
            if (submission.Items is null || submission.Items.Count == 0)
            {
                return BadRequest("Add at least one Service Invoice item.");
            }
            // Repeat the body while `the loop condition` remains true.
            for (var index = 0; index < submission.Items.Count; index++)
            {
                var item = submission.Items[index];
                // Return HTTP 400 Bad Request when `item.Quantity <= 0 || string.IsNullOrWhiteSpace(item.Unit) || item.Unit.Length > 50 || string.IsNullOrWhiteSpace(item.Requirement) || string.IsNullOrWhiteSpace(item.Store) || !string.Equals(item.Store.Trim(), branch, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(item.Description) || item.Unit_Price < 0 || item.Amount < 0` is true.
                if (item.Quantity <= 0 || string.IsNullOrWhiteSpace(item.Unit) ||
                    item.Unit.Length > 50 ||
                    string.IsNullOrWhiteSpace(item.Requirement) ||
                    string.IsNullOrWhiteSpace(item.Store) ||
                    !string.Equals(item.Store.Trim(), branch, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(item.Description) ||
                    item.Unit_Price < 0 || item.Amount < 0)
                {
                    return BadRequest($"Service Invoice item {index + 1} has invalid or missing values.");
                }
                // Return HTTP 400 Bad Request when `decimal.Round(item.Quantity * item.Unit_Price, 2) != decimal.Round(item.Amount, 2)` is true.
                if (decimal.Round(item.Quantity * item.Unit_Price, 2) != decimal.Round(item.Amount, 2))
                {
                    return BadRequest($"Service Invoice item {index + 1} amount does not match quantity times unit price.");
                }
            }

            await using var transaction = await _Localdb.Database.BeginTransactionAsync();
            var invoice = new ServiceInvoice
            {
                JobOrder_ID = submission.JobOrder_ID,
                Branch = branch,
                Title = submission.Title.Trim(),
                ClientName = submission.ClientName.Trim(),
                Address = submission.Address.Trim(),
                Terms = submission.Terms.Trim(),
                Invoice_Date = submission.Invoice_Date == default
                    ? DateOnly.FromDateTime(DateTime.Today)
                    : submission.Invoice_Date,
                Status = "Completed"
            };
            _Localdb.ServiceInvoice.Add(invoice);
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();

            var invoiceItems = submission.Items.Select(item => new ServiceInvoiceItem
            {
                ServiceInvoice_ID = invoice.ServiceInvoice_ID,
                Quantity = item.Quantity,
                Unit = item.Unit.Trim(),
                Store = item.Store.Trim(),
                Requirement = item.Requirement.Trim(),
                Description = item.Description.Trim(),
                Unit_Price = decimal.Round(item.Unit_Price, 2),
                Amount = decimal.Round(item.Amount, 2)
            });
            _Localdb.ServiceInvoiceItems.AddRange(invoiceItems);
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(invoice);
        }

        /// <summary>Retrieves the local service invoice items.</summary>
        [HttpGet("GetLocalServiceInvoiceItems")]
        public async Task<ActionResult<List<ServiceInvoiceItem>>> GetLocalServiceInvoiceItems()
        {
            return await _Localdb.ServiceInvoiceItems.AsNoTracking().ToListAsync();
        }

        /// <summary>Updates the local service invoice item.</summary>
        [HttpPost("UpdateLocalServiceInvoiceItem")]
        public async Task<ActionResult<ServiceInvoiceItem>> UpdateLocalServiceInvoiceItem(
            int id,
            [FromBody] ServiceInvoiceItem serviceInvoiceItem)
        {
            // Return HTTP 400 Bad Request when `serviceInvoiceItem.Quantity <= 0 || string.IsNullOrWhiteSpace(serviceInvoiceItem.Unit) || serviceInvoiceItem.Unit.Length > 50 || string.IsNullOrWhiteSpace(serviceInvoiceItem.Description) || serviceInvoiceItem.Unit_Price < 0 || serviceInvoiceItem.Amount < 0 || decimal.Round(serviceInvoiceItem.Quantity * serviceInvoiceItem.Unit_Price, 2) != decimal.Round(serviceInvoiceItem.Amount, 2)` is true.
            if (serviceInvoiceItem.Quantity <= 0 ||
                string.IsNullOrWhiteSpace(serviceInvoiceItem.Unit) ||
                serviceInvoiceItem.Unit.Length > 50 ||
                string.IsNullOrWhiteSpace(serviceInvoiceItem.Description) ||
                serviceInvoiceItem.Unit_Price < 0 ||
                serviceInvoiceItem.Amount < 0 ||
                decimal.Round(serviceInvoiceItem.Quantity * serviceInvoiceItem.Unit_Price, 2) !=
                decimal.Round(serviceInvoiceItem.Amount, 2))
            {
                return BadRequest("The Service Invoice item has invalid or missing values.");
            }

            var existingItem = await _Localdb.ServiceInvoiceItems
                .FirstOrDefaultAsync(item => item.ServiceInvoiceItem_ID == id);
            // Return HTTP 404 Not Found when `existingItem is null` is true.
            if (existingItem is null)
            {
                return NotFound();
            }

            existingItem.Quantity = serviceInvoiceItem.Quantity;
            existingItem.Unit = serviceInvoiceItem.Unit.Trim();
            // Enter this branch when `serviceInvoiceItem.Store is not null` is true.
            if (serviceInvoiceItem.Store is not null)
            {
                existingItem.Store = serviceInvoiceItem.Store.Trim();
            }
            // Enter this branch when `serviceInvoiceItem.Requirement is not null` is true.
            if (serviceInvoiceItem.Requirement is not null)
            {
                existingItem.Requirement = serviceInvoiceItem.Requirement.Trim();
            }
            existingItem.Description = serviceInvoiceItem.Description.Trim();
            existingItem.Unit_Price = decimal.Round(serviceInvoiceItem.Unit_Price, 2);
            existingItem.Amount = decimal.Round(serviceInvoiceItem.Amount, 2);
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            return Ok(existingItem);
        }

        /// <summary>Updates the local service invoice.</summary>
        [HttpPost("UpdateLocalServiceInvoice")]
        public ActionResult<ServiceInvoice> UpdateLocalServiceInvoice(Int32 Id, [FromBody] ServiceInvoice serviceInvoice)
        {
            // Return HTTP 400 Bad Request when `serviceInvoice == null` is true.
            if (serviceInvoice == null)
            {
                return BadRequest(serviceInvoice);
            }
            var updateServiceInvoice = _Localdb.ServiceInvoice.FirstOrDefault(x => x.ServiceInvoice_ID == Id);
            // Return HTTP 404 Not Found when `updateServiceInvoice == null` is true.
            if (updateServiceInvoice == null)
            {
                return NotFound();
            }
            // Return HTTP 400 Bad Request when `serviceInvoice.JobOrder_ID is not > 0` is true.
            if (serviceInvoice.JobOrder_ID is not > 0)
            {
                return BadRequest("A valid Job Order is required for a Service Invoice.");
            }
            // Return HTTP 404 Not Found when `!_Localdb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == serviceInvoice.JobOrder_ID)` is true.
            if (!_Localdb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == serviceInvoice.JobOrder_ID))
            {
                return NotFound("The selected Job Order was not found.");
            }
            updateServiceInvoice.JobOrder_ID = serviceInvoice.JobOrder_ID;
            // Enter this branch when `serviceInvoice.Branch is not null` is true.
            if (serviceInvoice.Branch is not null)
            {
                updateServiceInvoice.Branch = serviceInvoice.Branch.Trim();
            }
            // Enter this branch when `serviceInvoice.Title is not null` is true.
            if (serviceInvoice.Title is not null)
            {
                updateServiceInvoice.Title = serviceInvoice.Title.Trim();
            }
            // Enter this branch when `serviceInvoice.ClientName is not null` is true.
            if (serviceInvoice.ClientName is not null)
            {
                updateServiceInvoice.ClientName = serviceInvoice.ClientName.Trim();
            }
            // Enter this branch when `serviceInvoice.Address is not null` is true.
            if (serviceInvoice.Address is not null)
            {
                updateServiceInvoice.Address = serviceInvoice.Address.Trim();
            }
            // Enter this branch when `serviceInvoice.Terms is not null` is true.
            if (serviceInvoice.Terms is not null)
            {
                updateServiceInvoice.Terms = serviceInvoice.Terms.Trim();
            }
            // Enter this branch when `serviceInvoice.Invoice_Date != default` is true.
            if (serviceInvoice.Invoice_Date != default)
            {
                updateServiceInvoice.Invoice_Date = serviceInvoice.Invoice_Date;
            }
            // Enter this branch when `serviceInvoice.Unit is not null` is true.
            if (serviceInvoice.Unit is not null)
            {
                updateServiceInvoice.Unit = serviceInvoice.Unit;
            }
            // Enter this branch when `serviceInvoice.Description is not null` is true.
            if (serviceInvoice.Description is not null)
            {
                updateServiceInvoice.Description = serviceInvoice.Description;
            }
            // Enter this branch when `serviceInvoice.Unit_Price.HasValue` is true.
            if (serviceInvoice.Unit_Price.HasValue)
            {
                updateServiceInvoice.Unit_Price = serviceInvoice.Unit_Price;
            }
            // Enter this branch when `serviceInvoice.Amount.HasValue` is true.
            if (serviceInvoice.Amount.HasValue)
            {
                updateServiceInvoice.Amount = serviceInvoice.Amount;
            }
            // Enter this branch when `serviceInvoice.Status is not null` is true.
            if (serviceInvoice.Status is not null)
            {
                updateServiceInvoice.Status = serviceInvoice.Status;
            }


            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(updateServiceInvoice);
        }

        /// <summary>Deletes the local service invoice.</summary>
        [HttpPut ("DeleteLocalServiceInvoice")]
        public ActionResult DeleteLocalServiceInvoice(int id)
        {
            var serviceInvoice = _Localdb.ServiceInvoice.FirstOrDefault(x => x.ServiceInvoice_ID == id);
            // Return HTTP 404 Not Found when `serviceInvoice == null` is true.
            if (serviceInvoice == null)
            {
                return NotFound();
            }
            _Localdb.Remove(serviceInvoice);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }

        /// <summary>Retrieves all service invoices from the cloud database.</summary>
        [HttpGet("GetCloudServiceInvoice")]
        public List<ServiceInvoice> GetAllCloudServiceInvoice()
        {
            return _CloudDb.ServiceInvoice.ToList();
        }

        /// <summary>Retrieves the cloud service invoice id.</summary>
        [HttpGet("GetCloudServiceInvoiceByID")]
        public ActionResult<ServiceInvoice> GetCloudServiceInvoiceID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid service invoice ID.");
            }
            var serviceInvoice = _CloudDb.ServiceInvoice.FirstOrDefault(x => x.ServiceInvoice_ID == id);
            // Return HTTP 404 Not Found when `serviceInvoice == null` is true.
            if (serviceInvoice == null)
            {
                return NotFound();
            }
            return Ok(serviceInvoice);
        }

        /// <summary>Adds the cloud service invoice.</summary>
        [HttpPost("AddCloudServiceInvoice")]
        public ActionResult<ServiceInvoice> AddCloudServiceInvoice([FromBody] ServiceInvoice serviceInvoice)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            // Return HTTP 400 Bad Request when `serviceInvoice.JobOrder_ID is not > 0` is true.
            if (serviceInvoice.JobOrder_ID is not > 0)
            {
                return BadRequest("A valid Job Order is required for a Service Invoice.");
            }
            // Return HTTP 404 Not Found when `!_CloudDb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == serviceInvoice.JobOrder_ID)` is true.
            if (!_CloudDb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == serviceInvoice.JobOrder_ID))
            {
                return NotFound("The selected Job Order was not found.");
            }
            // Enter this branch when `serviceInvoice.Invoice_Date == default` is true.
            if (serviceInvoice.Invoice_Date == default)
            {
                serviceInvoice.Invoice_Date = DateOnly.FromDateTime(DateTime.Today);
            }
            _CloudDb.ServiceInvoice.Add(serviceInvoice);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(serviceInvoice);
        }

        /// <summary>Updates the cloud service invoice.</summary>
        [HttpPost("UpdateCloudServiceInvoice")]
        public ActionResult<ServiceInvoice> UpdateCloudServiceInvoice(Int32 Id, [FromBody] ServiceInvoice serviceInvoice)
        {
            // Return HTTP 400 Bad Request when `serviceInvoice == null` is true.
            if (serviceInvoice == null)
            {
                return BadRequest(serviceInvoice);
            }
            var updateServiceInvoice = _CloudDb.ServiceInvoice.FirstOrDefault(x => x.ServiceInvoice_ID == Id);
            // Return HTTP 404 Not Found when `updateServiceInvoice == null` is true.
            if (updateServiceInvoice == null)
            {
                return NotFound();
            }
            // Return HTTP 400 Bad Request when `serviceInvoice.JobOrder_ID is not > 0` is true.
            if (serviceInvoice.JobOrder_ID is not > 0)
            {
                return BadRequest("A valid Job Order is required for a Service Invoice.");
            }
            // Return HTTP 404 Not Found when `!_CloudDb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == serviceInvoice.JobOrder_ID)` is true.
            if (!_CloudDb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == serviceInvoice.JobOrder_ID))
            {
                return NotFound("The selected Job Order was not found.");
            }
            updateServiceInvoice.JobOrder_ID = serviceInvoice.JobOrder_ID;
            // Enter this branch when `serviceInvoice.Invoice_Date != default` is true.
            if (serviceInvoice.Invoice_Date != default)
            {
                updateServiceInvoice.Invoice_Date = serviceInvoice.Invoice_Date;
            }
            // Enter this branch when `serviceInvoice.Unit is not null` is true.
            if (serviceInvoice.Unit is not null)
            {
                updateServiceInvoice.Unit = serviceInvoice.Unit;
            }
            // Enter this branch when `serviceInvoice.Description is not null` is true.
            if (serviceInvoice.Description is not null)
            {
                updateServiceInvoice.Description = serviceInvoice.Description;
            }
            // Enter this branch when `serviceInvoice.Unit_Price.HasValue` is true.
            if (serviceInvoice.Unit_Price.HasValue)
            {
                updateServiceInvoice.Unit_Price = serviceInvoice.Unit_Price;
            }
            // Enter this branch when `serviceInvoice.Amount.HasValue` is true.
            if (serviceInvoice.Amount.HasValue)
            {
                updateServiceInvoice.Amount = serviceInvoice.Amount;
            }
            // Enter this branch when `serviceInvoice.Status is not null` is true.
            if (serviceInvoice.Status is not null)
            {
                updateServiceInvoice.Status = serviceInvoice.Status;
            }
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(updateServiceInvoice);
        }
        /// <summary>Deletes the cloud service invoice.</summary>
        [HttpPut("DeleteCloudServiceInvoice")]
        public ActionResult DeleteCloudServiceInvoice(int id)
        {
            var serviceInvoice = _CloudDb.ServiceInvoice.FirstOrDefault(x => x.ServiceInvoice_ID == id);
            // Return HTTP 404 Not Found when `serviceInvoice == null` is true.
            if (serviceInvoice == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(serviceInvoice);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return NoContent();
        }

        /// <summary>Retrieves the local service invoice files.</summary>
        [HttpGet("GetLocalServiceInvoiceFiles")]
        public async Task<ActionResult<List<ServiceInvoiceFile>>> GetLocalServiceInvoiceFiles()
        {
            return await _Localdb.ServiceInvoiceFiles.AsNoTracking().ToListAsync();
        }

        /// <summary>Adds the local service invoice files batch.</summary>
        [HttpPost("AddLocalServiceInvoiceFilesBatch")]
        [RequestSizeLimit(100_000_000)]
        public async Task<ActionResult<List<int>>> AddLocalServiceInvoiceFilesBatch(
            [FromBody] List<ServiceInvoiceFile> files)
        {
            // Return HTTP 400 Bad Request when `files == null || files.Count == 0` is true.
            if (files == null || files.Count == 0)
            {
                return BadRequest("Select at least one Service Invoice file.");
            }

            // Repeat the body while `the loop condition` remains true.
            for (var index = 0; index < files.Count; index++)
            {
                var file = files[index];
                // Return HTTP 400 Bad Request when `file.JobOrderID <= 0 || !await _Localdb.JobOrder.AnyAsync(jobOrder => jobOrder.JobOrder_ID == file.JobOrderID)` is true.
                if (file.JobOrderID <= 0 ||
                    !await _Localdb.JobOrder.AnyAsync(jobOrder => jobOrder.JobOrder_ID == file.JobOrderID))
                {
                    return BadRequest($"File {index + 1} must be linked to a saved Job Order.");
                }

                // Return HTTP 400 Bad Request when `string.IsNullOrWhiteSpace(file.FileName) || string.IsNullOrWhiteSpace(file.FileExtension) || !string.Equals( Path.GetExtension(file.FileName), file.FileExtension, StringComparison.OrdinalIgnoreCase)` is true.
                if (string.IsNullOrWhiteSpace(file.FileName) ||
                    string.IsNullOrWhiteSpace(file.FileExtension) ||
                    !string.Equals(
                        Path.GetExtension(file.FileName),
                        file.FileExtension,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest($"File {index + 1} has invalid file details.");
                }

                // Return HTTP 400 Bad Request when `file.FileData is { Length: > 2_000_000 }` is true.
                if (file.FileData is { Length: > 2_000_000 })
                {
                    return BadRequest($"File {index + 1} must not exceed 2 MB.");
                }

                var extension = file.FileExtension.ToLowerInvariant();
                var validationError = extension == ".pdf"
                    ? UploadValidator.ValidatePdf(file.FileData)
                    : extension is ".jpg" or ".jpeg" or ".png"
                        ? UploadValidator.ValidateImage(file.FileData)
                        : "Only PDF, JPG, JPEG, and PNG files are supported.";
                // Return HTTP 400 Bad Request when `validationError != null` is true.
                if (validationError != null)
                {
                    return BadRequest($"File {index + 1}: {validationError}");
                }
            }

            _Localdb.ServiceInvoiceFiles.AddRange(files);
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            return Ok(files.Select(file => file.ServiceInvoiceFile_ID).ToList());
        }

        /// <summary>Retrieves the cloud service invoice files.</summary>
        [HttpGet("GetCloudServiceInvoiceFiles")]
        public async Task<ActionResult<List<ServiceInvoiceFile>>> GetCloudServiceInvoiceFiles()
        {
            return await _CloudDb.ServiceInvoiceFiles.AsNoTracking().ToListAsync();
        }

        /// <summary>Adds the cloud service invoice files batch.</summary>
        [HttpPost("AddCloudServiceInvoiceFilesBatch")]
        [RequestSizeLimit(100_000_000)]
        public async Task<ActionResult<List<int>>> AddCloudServiceInvoiceFilesBatch(
            [FromBody] List<ServiceInvoiceFile> files)
        {
            // Return HTTP 400 Bad Request when `files == null || files.Count == 0` is true.
            if (files == null || files.Count == 0)
            {
                return BadRequest("Select at least one Service Invoice file.");
            }

            // Repeat the body while `the loop condition` remains true.
            for (var index = 0; index < files.Count; index++)
            {
                var file = files[index];
                // Return HTTP 400 Bad Request when `file.JobOrderID <= 0 || !await _CloudDb.JobOrder.AnyAsync(jobOrder => jobOrder.JobOrder_ID == file.JobOrderID)` is true.
                if (file.JobOrderID <= 0 ||
                    !await _CloudDb.JobOrder.AnyAsync(jobOrder => jobOrder.JobOrder_ID == file.JobOrderID))
                {
                    return BadRequest($"File {index + 1} must be linked to a saved Job Order.");
                }

                // Return HTTP 400 Bad Request when `string.IsNullOrWhiteSpace(file.FileName) || string.IsNullOrWhiteSpace(file.FileExtension) || !string.Equals( Path.GetExtension(file.FileName), file.FileExtension, StringComparison.OrdinalIgnoreCase)` is true.
                if (string.IsNullOrWhiteSpace(file.FileName) ||
                    string.IsNullOrWhiteSpace(file.FileExtension) ||
                    !string.Equals(
                        Path.GetExtension(file.FileName),
                        file.FileExtension,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest($"File {index + 1} has invalid file details.");
                }

                // Return HTTP 400 Bad Request when `file.FileData is { Length: > 2_000_000 }` is true.
                if (file.FileData is { Length: > 2_000_000 })
                {
                    return BadRequest($"File {index + 1} must not exceed 2 MB.");
                }

                var extension = file.FileExtension.ToLowerInvariant();
                var validationError = extension == ".pdf"
                    ? UploadValidator.ValidatePdf(file.FileData)
                    : extension is ".jpg" or ".jpeg" or ".png"
                        ? UploadValidator.ValidateImage(file.FileData)
                        : "Only PDF, JPG, JPEG, and PNG files are supported.";
                // Return HTTP 400 Bad Request when `validationError != null` is true.
                if (validationError != null)
                {
                    return BadRequest($"File {index + 1}: {validationError}");
                }
            }

            _CloudDb.ServiceInvoiceFiles.AddRange(files);
            // Persist the pending entity changes to the database at this point.
            await _CloudDb.SaveChangesAsync();
            return Ok(files.Select(file => file.ServiceInvoiceFile_ID).ToList());
        }

        // ------------------------------- SERVICE INVOICE ---------------------------

        // ------------------------------- INSTALLATION SCHEDULE ---------------------------

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

        // ------------------------------- INSTALLATION SCHEDULE ---------------------------

        // ------------------------------- DELIVERY RECEIPT ---------------------------

        [HttpGet("GetLocalDeliveryReceipts")]
        // Retrieves all local delivery receipts. Returns List<DeliveryReceipt>.
        public List<DeliveryReceipt> GetAllLocalDeliveryReceipts()
        {
            var receipts = _Localdb.DeliveryReceipt.AsNoTracking().ToList();
            var deliveryIds = receipts.Select(receipt => receipt.Delivery_ID).ToArray();
            var files = deliveryIds.Length == 0
                ? new List<DeliveryFiles>()
                : _Localdb.DeliveryFiles.AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(file => deliveryIds.Contains(file.Delivery_ID))
                    .ToList();
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
            return receipts;
        }

        /// <summary>Retrieves the local delivery receipts id.</summary>
        [HttpGet("GetLocalDeliveryReceiptsByID")]
        public ActionResult<DeliveryReceipt> GetLocalDeliveryReceiptsID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var deliveryReceipt = _Localdb.DeliveryReceipt.AsNoTracking().FirstOrDefault(x => x.Delivery_ID == id);
            // Return HTTP 404 Not Found when `deliveryReceipt == null` is true.
            if (deliveryReceipt == null)
            {
                return NotFound();
            }
            var files = _Localdb.DeliveryFiles.AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(file => file.Delivery_ID == id)
                .ToList();
            // Process each `file` returned by the collection expression in the loop header.
            foreach (var file in files)
            {
                file.File_Data = _encryptionService.TryDecrypt(file.File_Data);
            }
            deliveryReceipt.ApprovalStatus = WorkflowRules.GetDeliveryReceiptApprovalStatus(
                deliveryReceipt.Status,
                files,
                id);
            deliveryReceipt.Status = WorkflowRules.GetDeliveryReceiptStatus(
                WorkflowRules.HasReceivedByPhoto(files, id));
            return Ok(deliveryReceipt);
        }

        /// <summary>Adds the local delivery receipts.</summary>
        [HttpPost("AddLocalDeliveryReceipts")]
        public ActionResult<DeliveryReceipt> AddLocalDeliveryReceipts(DeliveryReceipt deliveryReceipt)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            // Return HTTP 400 Bad Request when `deliveryReceipt.JobOrderID is not > 0 || !_Localdb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == deliveryReceipt.JobOrderID)` is true.
            if (deliveryReceipt.JobOrderID is not > 0 ||
                !_Localdb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == deliveryReceipt.JobOrderID))
            {
                return BadRequest("A valid Job Order is required for the Delivery Receipt.");
            }
            deliveryReceipt.Status = "Request";
            _Localdb.Add(deliveryReceipt);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            deliveryReceipt.ApprovalStatus = "Request";
            deliveryReceipt.Status = WorkflowRules.GetDeliveryReceiptStatus(false);
            return Ok(deliveryReceipt);
        }

        /// <summary>Updates the local delivery receipts.</summary>
        [HttpPost("UpdateLocalDeliveryReceipts")]
        public ActionResult<DeliveryReceipt> UpdateLocalDeliveryReceipts(Int32 Id, [FromBody] DeliveryReceipt deliveryReceipt)
        {
            // Return HTTP 400 Bad Request when `deliveryReceipt == null` is true.
            if (deliveryReceipt == null)
            {
                return BadRequest(deliveryReceipt);
            }
            // Return HTTP 400 Bad Request when `deliveryReceipt.JobOrderID is not > 0 || !_Localdb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == deliveryReceipt.JobOrderID)` is true.
            if (deliveryReceipt.JobOrderID is not > 0 ||
                !_Localdb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == deliveryReceipt.JobOrderID))
            {
                return BadRequest("A valid Job Order is required for the Delivery Receipt.");
            }
            using var transaction = _Localdb.Database.BeginTransaction(IsolationLevel.Serializable);
            var updateDeliveryReceipt = _Localdb.DeliveryReceipt.FirstOrDefault(x => x.Delivery_ID == Id);
            // Return HTTP 404 Not Found when `updateDeliveryReceipt == null` is true.
            if (updateDeliveryReceipt == null)
            {
                return NotFound();
            }
            var existingFiles = _Localdb.DeliveryFiles.AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(file => file.Delivery_ID == Id)
                .ToList();
            // Return HTTP 409 Conflict when `WorkflowRules.GetDeliveryReceiptApprovalStatus( updateDeliveryReceipt.Status, existingFiles, Id) !=` is true.
            if (WorkflowRules.GetDeliveryReceiptApprovalStatus(
                    updateDeliveryReceipt.Status,
                    existingFiles,
                    Id) != "Request")
            {
                return Conflict("An approved or rejected Delivery Receipt can no longer be edited.");
            }
            // Process each `file` returned by the collection expression in the loop header.
            foreach (var file in existingFiles)
            {
                file.File_Data = _encryptionService.TryDecrypt(file.File_Data);
            }
            updateDeliveryReceipt.JobOrderID = deliveryReceipt.JobOrderID;
            updateDeliveryReceipt.Title = deliveryReceipt.Title;
            updateDeliveryReceipt.ClientName = deliveryReceipt.ClientName;
            updateDeliveryReceipt.Address = deliveryReceipt.Address;
            updateDeliveryReceipt.Terms = deliveryReceipt.Terms;
            updateDeliveryReceipt.date = deliveryReceipt.date;
            updateDeliveryReceipt.time = deliveryReceipt.time;
            updateDeliveryReceipt.Status = "Request";
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            updateDeliveryReceipt.ApprovalStatus = "Request";
            updateDeliveryReceipt.Status = WorkflowRules.GetDeliveryReceiptStatus(
                WorkflowRules.HasReceivedByPhoto(existingFiles, Id));
            transaction.Commit();
            return Ok(updateDeliveryReceipt);
        }

        /// <summary>Deletes the local delivery receipts.</summary>
        [HttpPut("DeleteLocalDeliveryReceipts")]
        public ActionResult DeleteLocalDeliveryReceipts(int id)
        {
            var deliveryReceipt = _Localdb.DeliveryReceipt.FirstOrDefault(x => x.Delivery_ID == id);
            // Return HTTP 404 Not Found when `deliveryReceipt == null` is true.
            if (deliveryReceipt == null)
            {
                return NotFound();
            }
            _Localdb.Remove(deliveryReceipt);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }


        /// <summary>Retrieves all delivery receipts from the cloud database.</summary>
        [HttpGet("GetCloudDeliveryReceipts")]
        public List<DeliveryReceipt> GetAllCloudDeliveryReceipts()
        {
            var receipts = _CloudDb.DeliveryReceipt.AsNoTracking().ToList();
            var deliveryIds = receipts.Select(receipt => receipt.Delivery_ID).ToArray();
            var files = deliveryIds.Length == 0
                ? new List<DeliveryFiles>()
                : _CloudDb.DeliveryFiles.AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(file => deliveryIds.Contains(file.Delivery_ID))
                    .ToList();
            // Process each `receipt` returned by the collection expression in the loop header.
            foreach (var receipt in receipts)
            {
                receipt.Status = WorkflowRules.GetDeliveryReceiptStatus(
                    WorkflowRules.HasReceivedByPhoto(files, receipt.Delivery_ID));
            }
            return receipts;
        }

        /// <summary>Retrieves the cloud delivery receipts id.</summary>
        [HttpGet("GetCloudDeliveryReceiptsByID")]
        public ActionResult<DeliveryReceipt> GetCloudDeliveryReceiptsID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var deliveryReceipt = _CloudDb.DeliveryReceipt.AsNoTracking().FirstOrDefault(x => x.Delivery_ID == id);
            // Return HTTP 404 Not Found when `deliveryReceipt == null` is true.
            if (deliveryReceipt == null)
            {
                return NotFound();
            }
            var files = _CloudDb.DeliveryFiles.AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(file => file.Delivery_ID == id)
                .ToList();
            deliveryReceipt.Status = WorkflowRules.GetDeliveryReceiptStatus(
                WorkflowRules.HasReceivedByPhoto(files, id));
            return Ok(deliveryReceipt);
        }

        /// <summary>Adds the cloud delivery receipts.</summary>
        [HttpPost("AddCloudDeliveryReceipts")]
        public ActionResult<DeliveryReceipt> AddCloudDeliveryReceipts(DeliveryReceipt deliveryReceipt)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            // Return HTTP 400 Bad Request when `deliveryReceipt.JobOrderID is not > 0 || !_CloudDb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == deliveryReceipt.JobOrderID)` is true.
            if (deliveryReceipt.JobOrderID is not > 0 ||
                !_CloudDb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == deliveryReceipt.JobOrderID))
            {
                return BadRequest("A valid Job Order is required for the Delivery Receipt.");
            }
            deliveryReceipt.Status = WorkflowRules.GetDeliveryReceiptStatus(false);
            _CloudDb.Add(deliveryReceipt);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(deliveryReceipt);
        }

        /// <summary>Updates the cloud delivery receipts.</summary>
        [HttpPost("UpdateCloudDeliveryReceipts")]
        public ActionResult<DeliveryReceipt> UpdateCloudDeliveryReceipts(Int32 Id, [FromBody] DeliveryReceipt deliveryReceipt)
        {
            // Return HTTP 400 Bad Request when `deliveryReceipt == null` is true.
            if (deliveryReceipt == null)
            {
                return BadRequest(deliveryReceipt);
            }
            // Return HTTP 400 Bad Request when `deliveryReceipt.JobOrderID is not > 0 || !_CloudDb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == deliveryReceipt.JobOrderID)` is true.
            if (deliveryReceipt.JobOrderID is not > 0 ||
                !_CloudDb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == deliveryReceipt.JobOrderID))
            {
                return BadRequest("A valid Job Order is required for the Delivery Receipt.");
            }
            var updateDeliveryReceipt = _CloudDb.DeliveryReceipt.FirstOrDefault(x => x.Delivery_ID == Id);
            // Return HTTP 404 Not Found when `updateDeliveryReceipt == null` is true.
            if (updateDeliveryReceipt == null)
            {
                return NotFound();
            }
            // Return HTTP 409 Conflict when `IsReviewedDeliveryReceiptStatus(updateDeliveryReceipt.Status)` is true.
            if (IsReviewedDeliveryReceiptStatus(updateDeliveryReceipt.Status))
            {
                return Conflict("An approved or rejected Delivery Receipt can no longer be edited.");
            }
            updateDeliveryReceipt.JobOrderID = deliveryReceipt.JobOrderID;
            updateDeliveryReceipt.Title = deliveryReceipt.Title;
            updateDeliveryReceipt.ClientName = deliveryReceipt.ClientName;
            updateDeliveryReceipt.Address = deliveryReceipt.Address;
            updateDeliveryReceipt.Terms = deliveryReceipt.Terms;
            updateDeliveryReceipt.date = deliveryReceipt.date;
            updateDeliveryReceipt.time = deliveryReceipt.time;
            updateDeliveryReceipt.Status = WorkflowRules.GetDeliveryReceiptStatus(
                WorkflowRules.HasReceivedByPhoto(
                    // Filter the query to rows satisfying `DeliveryFiles.Where(file => file.Delivery_ID == Id).ToList(), Id` before materializing the results.
                    _CloudDb.DeliveryFiles.Where(file => file.Delivery_ID == Id).ToList(),
                    Id));
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(updateDeliveryReceipt);
        }

        /// <summary>Deletes the cloud delivery receipts.</summary>
        [HttpPut("DeleteCloudDeliveryReceipts")]
        public ActionResult DeleteCloudDeliveryReceipts(int id)
        {
            var deliveryReceipt = _CloudDb.DeliveryReceipt.FirstOrDefault(x => x.Delivery_ID == id);
            // Return HTTP 404 Not Found when `deliveryReceipt == null` is true.
            if (deliveryReceipt == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(deliveryReceipt);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return NoContent();
        }


        // ------------------------------- DELIVERY RECEIPT ---------------------------

        // ------------------------------- DELIVERY RECEIPT ITEMS ---------------------------

        [HttpGet("GetLocalDeliveryReceiptsItems")]
        // Retrieves all local delivery receipts items. Returns List<DeliveryReceiptItems>.
        public List<DeliveryReceiptItems> GetAllLocalDeliveryReceiptsItems()
        {
            return _Localdb.DeliveryReceiptItems.ToList();
        }

        /// <summary>Retrieves the local delivery receipts items id.</summary>
        [HttpGet("GetLocalDeliveryReceiptsItemsByID")]
        public ActionResult<DeliveryReceiptItems> GetLocalDeliveryReceiptsItemsID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var deliveryReceipt = _Localdb.DeliveryReceiptItems.FirstOrDefault(x => x.DeliveryReceiptItems_ID == id);
            // Return HTTP 404 Not Found when `deliveryReceipt == null` is true.
            if (deliveryReceipt == null)
            {
                return NotFound();
            }
            return Ok(deliveryReceipt);
        }

        /// <summary>Adds the local delivery receipts items.</summary>
        [HttpPost("AddLocalDeliveryReceiptsItems")]
        public ActionResult<DeliveryReceiptItems> AddLocalDeliveryReceiptsItems(DeliveryReceiptItems deliveryReceiptItems)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(deliveryReceiptItems);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(deliveryReceiptItems);
        }

        /// <summary>Updates the local delivery receipt items.</summary>
        [HttpPost("UpdateLocalDeliveryReceiptItems")]
        public ActionResult<DeliveryReceiptItems> UpdateLocalDeliveryReceiptItems(
            [FromQuery] int Id,
            [FromBody] DeliveryReceiptItems deliveryReceiptItems)
        {
            // Enter this branch when `Id <= 0 || deliveryReceiptItems == null` is true.
            if (Id <= 0 || deliveryReceiptItems == null)
            {
                // Coordinates the are API endpoints, including request handling and response generation.
                return BadRequest("A valid delivery receipt item ID and record are required.");
            }

            var savedItem = _Localdb.DeliveryReceiptItems
                .FirstOrDefault(item => item.DeliveryReceiptItems_ID == Id);
            // Return HTTP 404 Not Found when `savedItem == null` is true.
            if (savedItem == null)
            {
                return NotFound("Delivery receipt item not found.");
            }
            savedItem.Requirement = deliveryReceiptItems.Requirement;
            savedItem.Description = deliveryReceiptItems.Description;
            savedItem.Store = deliveryReceiptItems.Store;
            savedItem.Quantity = deliveryReceiptItems.Quantity;
            savedItem.Delivery_ID = deliveryReceiptItems.Delivery_ID;
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(savedItem);
        }

        /// <summary>Deletes the local delivery receipt items.</summary>
        [HttpPut("DeleteLocalDeliveryReceiptsItems")]
        public ActionResult DeleteLocalDeliveryReceiptItems(int id)
        {
            var deliveryReceiptItems = _Localdb.DeliveryReceiptItems.FirstOrDefault(x => x.DeliveryReceiptItems_ID == id);
            // Return HTTP 404 Not Found when `deliveryReceiptItems == null` is true.
            if (deliveryReceiptItems == null)
            {
                return NotFound();
            }
            _Localdb.Remove(deliveryReceiptItems);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }
        // ------------------------------- DELIVERY RECEIPT ITEMS---------------------------

        // ------------------------------- DELIVERY FILES ---------------------------

        [HttpGet("GetLocalDeliveryFiles")]
        // Retrieves all delivery files. Returns List<DeliveryFiles>.
        public List<DeliveryFiles> GetAllDeliveryFiles()
        {
            var list = _Localdb.DeliveryFiles.AsNoTracking().ToList();
            // Process each `r` returned by the collection expression in the loop header.
            foreach (var r in list) r.File_Data = _encryptionService.TryDecrypt(r.File_Data);
            return list;
        }

        /// <summary>Retrieves the local delivery receipt approvals.</summary>
        [HttpGet("GetLocalDeliveryReceiptApprovals")]
        public ActionResult<List<DeliveryFiles>> GetLocalDeliveryReceiptApprovals()
        {
            var files = _Localdb.DeliveryFiles.AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(file => file.File_Role == "ApproverSigned")
                .ToList();
            var preparedByUsers = _Localdb.DeliveryFiles.AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(file => file.File_Role == "PreparedBy")
                .GroupBy(file => file.Delivery_ID)
                .ToDictionary(group => group.Key, group => group
                    // Apply the ordering expression here before returning or materializing the results.
                    .OrderBy(file => file.File_ID)
                    .Select(file => file.CreatedByUserId)
                    .FirstOrDefault());
            var approvals = files
                // Apply this predicate in the database query before materializing results.
                .Where(file =>
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

        /// <summary>Retrieves the local delivery files by id.</summary>
        [HttpGet("GetLocalDeliveryFilesByID")]
        public ActionResult<DeliveryFiles> GetLocalDeliveryFilesByID(int id)
        {
            var deliveryFiles = _Localdb.DeliveryFiles.AsNoTracking().FirstOrDefault(x => x.File_ID == id);
            // Return HTTP 404 Not Found when `deliveryFiles == null` is true.
            if (deliveryFiles == null) return NotFound();
            deliveryFiles.File_Data = _encryptionService.TryDecrypt(deliveryFiles.File_Data);
            return Ok(deliveryFiles);
        }

        /// <summary>Adds the local delivery files.</summary>
        [HttpPost("AddLocalDeliveryFiles")]
        public ActionResult<DeliveryFiles> AddLocalDeliveryFiles(DeliveryFiles deliveryFiles)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid) return BadRequest(ModelState);
            // Return HTTP 400 Bad Request when `IsApprovedByRole(deliveryFiles.File_Role)` is true.
            if (IsApprovedByRole(deliveryFiles.File_Role))
            {
                return BadRequest("Approved by signatures can only be added through the Delivery Receipt approval action.");
            }

            var isReceivedBy = WorkflowRules.IsReceivedByRole(deliveryFiles.File_Role);
            var error = isReceivedBy
                ? UploadValidator.ValidateImage(deliveryFiles.File_Data)
                : UploadValidator.ValidatePdf(deliveryFiles.File_Data);
            // Return HTTP 400 Bad Request when `error != null` is true.
            if (error != null) return BadRequest(error);
            DeliveryReceipt? deliveryReceipt = null;
            JobOrder? jobOrder = null;
            // Enter this branch when `isReceivedBy` is true.
            if (isReceivedBy)
            {
                deliveryReceipt = _Localdb.DeliveryReceipt
                    .FirstOrDefault(receipt => receipt.Delivery_ID == deliveryFiles.Delivery_ID);
                // Return HTTP 400 Bad Request when `deliveryReceipt?.JobOrderID is not > 0` is true.
                if (deliveryReceipt?.JobOrderID is not > 0)
                {
                    return BadRequest("A Received By photo must be linked to a Delivery Receipt with a valid Job Order.");
                }
                jobOrder = _Localdb.JobOrder
                    .FirstOrDefault(record => record.JobOrder_ID == deliveryReceipt.JobOrderID);
                // Return HTTP 400 Bad Request when `jobOrder == null` is true.
                if (jobOrder == null)
                {
                    return BadRequest("The Delivery Receipt is linked to a missing Job Order.");
                }
            }

            var plain = deliveryFiles.File_Data;
            deliveryFiles.File_Data = _encryptionService.Encrypt(plain);
            _Localdb.DeliveryFiles.Add(deliveryFiles);
            // Enter this branch when `isReceivedBy` is true.
            if (isReceivedBy)
            {
                jobOrder!.Date_Delivered = DateOnly.FromDateTime(DateTime.Now);
            }
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            deliveryFiles.File_Data = plain;
            return Ok(deliveryFiles);
        }

        /// <summary>Adds the local delivery files batch.</summary>
        [HttpPost("AddLocalDeliveryFilesBatch")]
        [RequestSizeLimit(100_000_000)]
        public ActionResult<List<int>> AddLocalDeliveryFilesBatch([FromBody] List<DeliveryFiles> files)
        {
            // Return HTTP 400 Bad Request when `files == null || files.Count == 0` is true.
            if (files == null || files.Count == 0) return BadRequest("No files.");
            // Return HTTP 400 Bad Request when `files.Any(file => IsApprovedByRole(file.File_Role))` is true.
            if (files.Any(file => IsApprovedByRole(file.File_Role)))
            {
                return BadRequest("Approved by signatures can only be added through the Delivery Receipt approval action.");
            }

            var receivedByDeliveryIds = files
                // Apply this predicate in the database query before materializing results.
                .Where(file => string.Equals(file.File_Role?.Trim(), "ReceivedBy", StringComparison.OrdinalIgnoreCase))
                .Select(file => file.Delivery_ID)
                .Distinct()
                .ToArray();
            var deliveryReceipts = _Localdb.DeliveryReceipt
                // Apply this predicate in the database query before materializing results.
                .Where(receipt => receivedByDeliveryIds.Contains(receipt.Delivery_ID))
                .ToDictionary(receipt => receipt.Delivery_ID);
            // Return HTTP 400 Bad Request when `deliveryReceipts.Count != receivedByDeliveryIds.Length` is true.
            if (deliveryReceipts.Count != receivedByDeliveryIds.Length)
            {
                return BadRequest("A Received By file must be linked to a saved Delivery Receipt.");
            }
            // Return HTTP 400 Bad Request when `deliveryReceipts.Values.Any(receipt => receipt.JobOrderID is not > 0)` is true.
            if (deliveryReceipts.Values.Any(receipt => receipt.JobOrderID is not > 0))
            {
                return BadRequest("The Delivery Receipt must be linked to a valid Job Order.");
            }

            var deliveredJobOrderIds = deliveryReceipts.Values
                .Select(receipt => receipt.JobOrderID.GetValueOrDefault())
                .Distinct()
                .ToArray();
            var deliveredJobOrders = _Localdb.JobOrder
                // Apply this predicate in the database query before materializing results.
                .Where(jobOrder => deliveredJobOrderIds.Contains(jobOrder.JobOrder_ID))
                .ToDictionary(jobOrder => jobOrder.JobOrder_ID);
            // Return HTTP 400 Bad Request when `deliveredJobOrders.Count != deliveredJobOrderIds.Length` is true.
            if (deliveredJobOrders.Count != deliveredJobOrderIds.Length)
            {
                return BadRequest("The Delivery Receipt is linked to a missing Job Order.");
            }

            // Repeat the body while `the loop condition` remains true.
            for (int i = 0; i < files.Count; i++)
            {
                var error = UploadValidator.ValidateImage(files[i].File_Data);
                // Return HTTP 400 Bad Request when `error != null` is true.
                if (error != null) return BadRequest($"File {i + 1}: {error}");
            }

            // Process each `f` returned by the collection expression in the loop header.
            foreach (var f in files)
            {
                f.File_Data = _encryptionService.Encrypt(f.File_Data);
                _Localdb.DeliveryFiles.Add(f);
            }
            // Process each `deliveryReceipt` returned by the collection expression in the loop header.
            foreach (var deliveryReceipt in deliveryReceipts.Values)
            {
                var jobOrder = deliveredJobOrders[deliveryReceipt.JobOrderID.GetValueOrDefault()];
                jobOrder.Date_Delivered = DateOnly.FromDateTime(DateTime.Now);
            }
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(files.Select(f => f.File_ID).ToList());
        }

        /// <summary>Updates the local delivery files.</summary>
        [HttpPost("UpdateLocalDeliveryFiles")]
        public ActionResult<DeliveryFiles> UpdateLocalDeliveryFiles(Int32 Id, [FromBody] DeliveryFiles deliveryFiles)
        {
            // Return HTTP 400 Bad Request when `deliveryFiles == null` is true.
            if (deliveryFiles == null)
            {
                return BadRequest(deliveryFiles);
            }
            var updateDeliveryFiles = _Localdb.DeliveryFiles.FirstOrDefault(x => x.File_ID == Id);
            // Return HTTP 404 Not Found when `updateDeliveryFiles == null` is true.
            if (updateDeliveryFiles == null)
            {
                return NotFound();
            }
            // Return HTTP 400 Bad Request when `IsApprovedByRole(updateDeliveryFiles.File_Role) || IsApprovedByRole(deliveryFiles.File_Role)` is true.
            if (IsApprovedByRole(updateDeliveryFiles.File_Role) || IsApprovedByRole(deliveryFiles.File_Role))
            {
                return BadRequest("Approved by signatures cannot be changed through the file update action.");
            }
            var isReceivedBy = WorkflowRules.IsReceivedByRole(updateDeliveryFiles.File_Role);
            var error = isReceivedBy
                ? UploadValidator.ValidateImage(deliveryFiles.File_Data)
                : UploadValidator.ValidatePdf(deliveryFiles.File_Data);
            // Return HTTP 400 Bad Request when `error != null` is true.
            if (error != null)
            {
                return BadRequest(error);
            }
            updateDeliveryFiles.File_Data = _encryptionService.Encrypt(deliveryFiles.File_Data);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(updateDeliveryFiles);
        }

        /// <summary>Deletes the local delivery files.</summary>
        [HttpPut ("DeleteLocalDeliveryFiles")]
        public ActionResult DeleteLocalDeliveryFiles(int id)
        {
            var deliveryFiles = _Localdb.DeliveryFiles.FirstOrDefault(x => x.File_ID == id);
            // Return HTTP 404 Not Found when `deliveryFiles == null` is true.
            if (deliveryFiles == null)
            {
                return NotFound();
            }
            // Return HTTP 400 Bad Request when `IsApprovedByRole(deliveryFiles.File_Role)` is true.
            if (IsApprovedByRole(deliveryFiles.File_Role))
            {
                return BadRequest("Approved by signatures cannot be deleted outside the approval workflow.");
            }
            _Localdb.Remove(deliveryFiles);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }

        /// <summary>Retrieves all delivery files from the cloud database.</summary>
        [HttpGet("GetCloudDeliveryFiles")]
        public List<DeliveryFiles> GetAllCloudDeliveryFiles()
        {
            return _CloudDb.DeliveryFiles.ToList();
        }

        /// <summary>Retrieves the cloud delivery files by id.</summary>
        [HttpGet("GetCloudDeliveryFilesByID")]
        public ActionResult<DeliveryFiles> GetCloudDeliveryFilesByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var deliveryFiles = _CloudDb.DeliveryFiles.FirstOrDefault(x => x.File_ID == id);
            // Return HTTP 404 Not Found when `deliveryFiles == null` is true.
            if (deliveryFiles == null)
            {
                return NotFound();
            }
            return Ok(deliveryFiles);
        }

        /// <summary>Adds the cloud delivery files.</summary>
        [HttpPost("AddCloudDeliveryFiles")]
        public ActionResult<DeliveryFiles> AddCloudDeliveryFiles(DeliveryFiles deliveryFiles)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            // Return HTTP 400 Bad Request when `IsApprovedByRole(deliveryFiles.File_Role)` is true.
            if (IsApprovedByRole(deliveryFiles.File_Role))
            {
                return BadRequest("Approved by signatures can only be added through the Delivery Receipt approval action.");
            }
            DeliveryReceipt? deliveryReceipt = null;
            JobOrder? jobOrder = null;
            // Enter this branch when `WorkflowRules.IsReceivedByRole(deliveryFiles.File_Role)` is true.
            if (WorkflowRules.IsReceivedByRole(deliveryFiles.File_Role))
            {
                var error = UploadValidator.ValidateImage(deliveryFiles.File_Data);
                // Return HTTP 400 Bad Request when `error != null` is true.
                if (error != null)
                {
                    return BadRequest(error);
                }
                deliveryReceipt = _CloudDb.DeliveryReceipt
                    .FirstOrDefault(receipt => receipt.Delivery_ID == deliveryFiles.Delivery_ID);
                // Return HTTP 400 Bad Request when `deliveryReceipt?.JobOrderID is not > 0` is true.
                if (deliveryReceipt?.JobOrderID is not > 0)
                {
                    return BadRequest("A Received By photo must be linked to a Delivery Receipt with a valid Job Order.");
                }
                jobOrder = _CloudDb.JobOrder
                    .FirstOrDefault(record => record.JobOrder_ID == deliveryReceipt.JobOrderID);
                // Return HTTP 400 Bad Request when `jobOrder == null` is true.
                if (jobOrder == null)
                {
                    return BadRequest("The Delivery Receipt is linked to a missing Job Order.");
                }
            }
            _CloudDb.Add(deliveryFiles);
            // Enter this branch when `deliveryReceipt != null` is true.
            if (deliveryReceipt != null)
            {
                // Enter this branch when `!IsReviewedDeliveryReceiptStatus(deliveryReceipt.Status)` is true.
                if (!IsReviewedDeliveryReceiptStatus(deliveryReceipt.Status))
                {
                    deliveryReceipt.Status = WorkflowRules.GetDeliveryReceiptStatus(true);
                }
                jobOrder!.Date_Delivered = DateOnly.FromDateTime(DateTime.Now);
            }
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(deliveryFiles);
        }

        /// <summary>Updates the cloud delivery files.</summary>
        [HttpPost("UpdateCloudDeliveryFiles")]
        public ActionResult<DeliveryFiles> UpdateCloudDeliveryFiles(Int32 Id, [FromBody] DeliveryFiles deliveryFiles)
        {
            // Return HTTP 400 Bad Request when `deliveryFiles == null` is true.
            if (deliveryFiles == null)
            {
                return BadRequest(deliveryFiles);
            }
            var updateDeliveryFiles = _CloudDb.DeliveryFiles.FirstOrDefault(x => x.File_ID == Id);
            // Return HTTP 404 Not Found when `updateDeliveryFiles == null` is true.
            if (updateDeliveryFiles == null)
            {
                return NotFound();
            }
            // Return HTTP 400 Bad Request when `IsApprovedByRole(updateDeliveryFiles.File_Role) || IsApprovedByRole(deliveryFiles.File_Role)` is true.
            if (IsApprovedByRole(updateDeliveryFiles.File_Role) || IsApprovedByRole(deliveryFiles.File_Role))
            {
                return BadRequest("Approved by signatures cannot be changed through the file update action.");
            }
            var previousDeliveryId = updateDeliveryFiles.Delivery_ID;
            var wasReceivedBy = WorkflowRules.IsReceivedByRole(updateDeliveryFiles.File_Role);
            var isReceivedBy = WorkflowRules.IsReceivedByRole(deliveryFiles.File_Role);
            // Enter this branch when `isReceivedBy` is true.
            if (isReceivedBy)
            {
                var error = UploadValidator.ValidateImage(deliveryFiles.File_Data);
                // Return HTTP 400 Bad Request when `error != null` is true.
                if (error != null)
                {
                    return BadRequest(error);
                }
                var targetReceipt = _CloudDb.DeliveryReceipt
                    .FirstOrDefault(receipt => receipt.Delivery_ID == deliveryFiles.Delivery_ID);
                // Return HTTP 400 Bad Request when `targetReceipt?.JobOrderID is not > 0 || !_CloudDb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == targetReceipt.JobOrderID)` is true.
                if (targetReceipt?.JobOrderID is not > 0 ||
                    !_CloudDb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == targetReceipt.JobOrderID))
                {
                    return BadRequest("A Received By photo must be linked to a Delivery Receipt with a valid Job Order.");
                }
            }
            updateDeliveryFiles.Delivery_ID = deliveryFiles.Delivery_ID;
            updateDeliveryFiles.File_Role = deliveryFiles.File_Role;
            updateDeliveryFiles.File_Name = deliveryFiles.File_Name;
            updateDeliveryFiles.File_Extension = deliveryFiles.File_Extension;
            updateDeliveryFiles.File_Data = deliveryFiles.File_Data;
            // Enter this branch when `wasReceivedBy || isReceivedBy` is true.
            if (wasReceivedBy || isReceivedBy)
            {
                var affectedDeliveryIds = new[] { previousDeliveryId, updateDeliveryFiles.Delivery_ID }.Distinct().ToArray();
                var remainingFiles = _CloudDb.DeliveryFiles.AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(file => affectedDeliveryIds.Contains(file.Delivery_ID) && file.File_ID != Id)
                    .ToList();
                remainingFiles.Add(updateDeliveryFiles);
                // Process each `receipt` returned by the collection expression in the loop header.
                foreach (var receipt in _CloudDb.DeliveryReceipt
                             // Filter the query to rows satisfying `.Where(record => affectedDeliveryIds.Contains(record.Delivery_ID))` before materializing the results.
                             .Where(record => affectedDeliveryIds.Contains(record.Delivery_ID)))
                {
                    // Enter this branch when `!IsReviewedDeliveryReceiptStatus(receipt.Status)` is true.
                    if (!IsReviewedDeliveryReceiptStatus(receipt.Status))
                    {
                        receipt.Status = WorkflowRules.GetDeliveryReceiptStatus(
                            WorkflowRules.HasReceivedByPhoto(remainingFiles, receipt.Delivery_ID));
                    }
                }
            }
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(updateDeliveryFiles);
        }

        /// <summary>Deletes the cloud delivery files.</summary>
        [HttpPut("DeleteCloudDeliveryFiles")]
        public ActionResult DeleteCloudDeliveryFiles(int id)
        {
            var deliveryFiles = _CloudDb.DeliveryFiles.FirstOrDefault(x => x.File_ID == id);
            // Return HTTP 404 Not Found when `deliveryFiles == null` is true.
            if (deliveryFiles == null)
            {
                return NotFound();
            }
            // Return HTTP 400 Bad Request when `IsApprovedByRole(deliveryFiles.File_Role)` is true.
            if (IsApprovedByRole(deliveryFiles.File_Role))
            {
                return BadRequest("Approved by signatures cannot be deleted outside the approval workflow.");
            }
            var deliveryId = deliveryFiles.Delivery_ID;
            var deliveryReceipt = _CloudDb.DeliveryReceipt
                .FirstOrDefault(receipt => receipt.Delivery_ID == deliveryId);
            // Enter this branch when `deliveryReceipt != null && WorkflowRules.IsReceivedByRole(deliveryFiles.File_Role) && !IsReviewedDeliveryReceiptStatus(deliveryReceipt.Status)` is true.
            if (deliveryReceipt != null &&
                WorkflowRules.IsReceivedByRole(deliveryFiles.File_Role) &&
                !IsReviewedDeliveryReceiptStatus(deliveryReceipt.Status))
            {
                var remainingFiles = _CloudDb.DeliveryFiles.AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(file => file.Delivery_ID == deliveryId && file.File_ID != id)
                    .ToList();
                deliveryReceipt.Status = WorkflowRules.GetDeliveryReceiptStatus(
                    WorkflowRules.HasReceivedByPhoto(remainingFiles, deliveryId));
            }
            _CloudDb.Remove(deliveryFiles);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- DELIVERY FILES ---------------------------

        // ------------------------------- COLLECTION RECEIPT ---------------------------

        [HttpGet("GetLocalCollectionReceipts")]
        // Retrieves all local collection receipts. Returns List<CollectionReceipt>.
        public List<CollectionReceipt> GetAllLocalCollectionReceipts()
        {
            return _Localdb.CollectionReceipt.ToList();
        }

        /// <summary>Retrieves the local collection receipt payment transactions.</summary>
        [HttpGet("GetLocalCollectionReceiptPaymentTransactions")]
        public async Task<ActionResult<List<PaymentTransactionDto>>> GetLocalCollectionReceiptPaymentTransactions(
            CancellationToken cancellationToken)
        {
            var payments = await _Localdb.PaymentTransactions
                .AsNoTracking()
                // Apply the ordering expression here before returning or materializing the results.
                .OrderByDescending(payment => payment.PaidAtUtc)
                // Apply the ordering expression here before returning or materializing the results.
                .ThenByDescending(payment => payment.PaymentTransaction_ID)
                .Select(payment => new PaymentTransactionDto
                {
                    PaymentTransaction_ID = payment.PaymentTransaction_ID,
                    CollectionReceipt_ID = payment.CollectionReceipt_ID,
                    JobOrderID = payment.JobOrderID,
                    Quotation_ID = payment.Quotation_ID,
                    Amount = payment.Amount,
                    PaidAtUtc = payment.PaidAtUtc,
                    CreatedBy = payment.CreatedBy,
                    CreatedByUserId = payment.CreatedByUserId,
                    CreatedByDepartmentId = payment.CreatedByDepartmentId
                })
                .ToListAsync(cancellationToken);

            return Ok(payments);
        }

        /// <summary>Retrieves the local collection receipts by id.</summary>
        [HttpGet("GetLocalCollectionReceiptsByID")]
        public ActionResult<CollectionReceipt> GetLocalCollectionReceiptsByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var collectionReceipt = _Localdb.CollectionReceipt.FirstOrDefault(x => x.CollectionReceipt_ID == id);
            // Return HTTP 404 Not Found when `collectionReceipt == null` is true.
            if (collectionReceipt == null)
            {
                return NotFound();
            }
            return Ok(collectionReceipt);
        }

        /// <summary>Adds the local collection receipt.</summary>
        [HttpPost("AddLocalCollectionReceipt")]
        public async Task<ActionResult<CollectionReceipt>> AddLocalCollectionReceipt(
            CollectionReceipt collectionReceipt,
            CancellationToken cancellationToken)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var saveResult = await SaveCollectionReceiptsWithPaymentsAsync(
                new[] { collectionReceipt },
                cancellationToken);
            // Exit the current operation when `saveResult.StatusCode.HasValue` is true.
            if (saveResult.StatusCode.HasValue)
            {
                return StatusCode(saveResult.StatusCode.Value, saveResult.Message);
            }

            return Ok(collectionReceipt);
        }

        /// <summary>Adds the local collection receipt batch.</summary>
        [HttpPost("AddLocalCollectionReceiptBatch")]
        [RequestSizeLimit(100_000_000)]
        public async Task<ActionResult<List<int>>> AddLocalCollectionReceiptBatch(
            [FromBody] List<CollectionReceipt> collectionReceiptsFiles,
            CancellationToken cancellationToken)
        {
            // Return HTTP 400 Bad Request when `collectionReceiptsFiles == null || collectionReceiptsFiles.Count == 0` is true.
            if (collectionReceiptsFiles == null || collectionReceiptsFiles.Count == 0) return BadRequest("No files.");

            // Repeat the body while `the loop condition` remains true.
            for (int i = 0; i < collectionReceiptsFiles.Count; i++)

            {
                // Return HTTP 400 Bad Request when `collectionReceiptsFiles[i] == null` is true.
                if (collectionReceiptsFiles[i] == null)
                {
                    return BadRequest($"File {i + 1}: Invalid file data.");
                }
                var error = UploadValidator.ValidateImage(collectionReceiptsFiles[i].FileData);
                // Return HTTP 400 Bad Request when `error != null` is true.
                if (error != null) return BadRequest($"File {i + 1}: {error}");
            }

            var saveResult = await SaveCollectionReceiptsWithPaymentsAsync(
                collectionReceiptsFiles,
                cancellationToken);
            // Exit the current operation when `saveResult.StatusCode.HasValue` is true.
            if (saveResult.StatusCode.HasValue)
            {
                return StatusCode(saveResult.StatusCode.Value, saveResult.Message);
            }

            return Ok(collectionReceiptsFiles.Select(f => f.CollectionReceipt_ID).ToList());
        }

        // Persists collection receipts with payments. Inputs: receipts (receipts); cancellationToken (cancellation token for the asynchronous operation). Returns (int? StatusCode, string? Message) asynchronously.
        private async Task<(int? StatusCode, string? Message)> SaveCollectionReceiptsWithPaymentsAsync(
            IReadOnlyCollection<CollectionReceipt> receipts,
            CancellationToken cancellationToken)
        {
            var jobOrderIds = receipts.Select(receipt => receipt.JobOrderID).Distinct().ToList();
            // Return HTTP 400 Bad Request when `jobOrderIds.Any(jobOrderId => jobOrderId <= 0)` is true.
            if (jobOrderIds.Any(jobOrderId => jobOrderId <= 0))
            {
                return (StatusCodes.Status400BadRequest, "A valid Job Order is required for every Collection Receipt.");
            }

            await using var transaction = await _Localdb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            try
            {
                // Return HTTP 409 Conflict when `await _Localdb.CollectionReceipt .IgnoreQueryFilters() .AnyAsync(receipt => jobOrderIds.Contains(receipt.JobOrderID), cancellationToken)` is true.
                if (await _Localdb.CollectionReceipt
                    .IgnoreQueryFilters()
                    .AnyAsync(receipt => jobOrderIds.Contains(receipt.JobOrderID), cancellationToken))
                {
                    return (StatusCodes.Status409Conflict,
                        "A Collection Receipt already exists for the selected Job Order.");
                }

                // Return HTTP 409 Conflict when `await _Localdb.PaymentTransactions .IgnoreQueryFilters() .AnyAsync(payment => jobOrderIds.Contains(payment.JobOrderID), cancellationToken)` is true.
                if (await _Localdb.PaymentTransactions
                    .IgnoreQueryFilters()
                    .AnyAsync(payment => jobOrderIds.Contains(payment.JobOrderID), cancellationToken))
                {
                    return (StatusCodes.Status409Conflict,
                        "A payment transaction already exists for the selected Job Order.");
                }

                var quotationLinks = await (
                    from jobOrder in _Localdb.JobOrder.IgnoreQueryFilters().AsNoTracking()
                    join conforme in _Localdb.Conforme.IgnoreQueryFilters().AsNoTracking()
                        on jobOrder.Conforme_FileID equals conforme.FileID
                    join quotation in _Localdb.Quotation.IgnoreQueryFilters().AsNoTracking()
                        on conforme.Quotation_ID equals quotation.Quotation_ID
                    where jobOrderIds.Contains(jobOrder.JobOrder_ID)
                    select new
                    {
                        JobOrderID = jobOrder.JobOrder_ID,
                        Quotation_ID = quotation.Quotation_ID,
                        Amount = quotation.Overall_Total
                    })
                    .ToListAsync(cancellationToken);
                var quotationByJobOrder = quotationLinks.ToDictionary(link => link.JobOrderID);
                var unresolvedJobOrderIds = jobOrderIds
                    // Apply this predicate in the database query before materializing results.
                    .Where(jobOrderId => !quotationByJobOrder.ContainsKey(jobOrderId))
                    .ToList();
                // Return HTTP 400 Bad Request when `unresolvedJobOrderIds.Count > 0` is true.
                if (unresolvedJobOrderIds.Count > 0)
                {
                    return (StatusCodes.Status400BadRequest,
                        $"A linked quotation and persisted total could not be resolved for Job Order(s): " +
                        $"{string.Join(", ", unresolvedJobOrderIds)}. No receipt or payment was saved.");
                }

                // Process each `receipt` returned by the collection expression in the loop header.
                foreach (var receipt in receipts)
                {
                    receipt.Status = "Completed";
                }

                _Localdb.CollectionReceipt.AddRange(receipts);
                _Localdb.PaymentTransactions.AddRange(jobOrderIds.Select(jobOrderId =>
                {
                    // Creates a payment transaction from the job order, receipt, and quotation.
                    var quotation = quotationByJobOrder[jobOrderId];
                    return new PaymentTransaction
                    {
                        CollectionReceipt = receipts.First(receipt => receipt.JobOrderID == jobOrderId),
                        JobOrderID = jobOrderId,
                        Quotation_ID = quotation.Quotation_ID,
                        Amount = quotation.Amount,
                        PaidAtUtc = DateTime.UtcNow
                    };
                }));

                // Persist the pending entity changes to the database at this point.
                await _Localdb.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return (null, null);
            }
            // Handle `Microsoft.EntityFrameworkCore.DbUpdateException exception` raised by the preceding operation.
            catch (Microsoft.EntityFrameworkCore.DbUpdateException exception)
                when (IsUniqueConstraintViolation(exception))
            {
                await transaction.RollbackAsync(cancellationToken);
                return (StatusCodes.Status409Conflict,
                    "A Collection Receipt or payment transaction already exists for the selected Job Order.");
            }
        }

        // Checks whether a database exception represents a unique-constraint violation.
        private static bool IsUniqueConstraintViolation(
            Microsoft.EntityFrameworkCore.DbUpdateException exception) =>
            exception.GetBaseException() is Microsoft.Data.SqlClient.SqlException
            {
                Number: 2601 or 2627
            };

        /// <summary>Updates the local collection receipt.</summary>
        [HttpPost("UpdateLocalCollectionReceipt")]
        [RequestSizeLimit(10_000_000)]
        public ActionResult<CollectionReceipt> UpdateLocalCollectionReceipt(Int32 Id, [FromBody] CollectionReceipt collectionReceipt)
        {
            // Return HTTP 400 Bad Request when `collectionReceipt == null` is true.
            if (collectionReceipt == null)
            {
                return BadRequest(collectionReceipt);
            }
            var updateCollectionReceipt = _Localdb.CollectionReceipt.FirstOrDefault(x => x.CollectionReceipt_ID == Id);
            // Return HTTP 404 Not Found when `updateCollectionReceipt == null` is true.
            if (updateCollectionReceipt == null)
            {
                return NotFound();
            }

            // Enter this branch when `collectionReceipt.FileData is { Length: > 0 }` is true.
            if (collectionReceipt.FileData is { Length: > 0 })
            {
                var error = UploadValidator.ValidateImage(collectionReceipt.FileData);
                // Return HTTP 400 Bad Request when `error != null` is true.
                if (error != null)
                {
                    return BadRequest(error);
                }
                updateCollectionReceipt.FileData = collectionReceipt.FileData;
            }
            updateCollectionReceipt.FileName = collectionReceipt.FileName;
            updateCollectionReceipt.FileExtension = collectionReceipt.FileExtension;
            updateCollectionReceipt.JobOrderID = collectionReceipt.JobOrderID;
            updateCollectionReceipt.Status = collectionReceipt.Status;

            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(updateCollectionReceipt);
        }

        /// <summary>Deletes the local collection receipt.</summary>
        [HttpPut ("DeleteLocalCollectionReceipt")]
        public IActionResult DeleteLocalCollectionReceipt(int id)
        {
            var collectionReceipt = _Localdb.CollectionReceipt.FirstOrDefault(x => x.CollectionReceipt_ID == id);
            // Return HTTP 404 Not Found when `collectionReceipt == null` is true.
            if (collectionReceipt == null)
            {
                return NotFound();
            }
            _Localdb.Remove(collectionReceipt);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }


        /// <summary>Retrieves all collection receipts from the cloud database.</summary>
        [HttpGet("GetCloudCollectionReceipts")]
        public List<CollectionReceipt> GetAllCloudCollectionReceipts()
        {
            return _CloudDb.CollectionReceipt.ToList();
        }

        /// <summary>Retrieves the cloud collection receipts by id.</summary>
        [HttpGet("GetCloudCollectionReceiptsByID")]
        public ActionResult<CollectionReceipt> GetCloudCollectionReceiptsByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var collectionReceipt = _CloudDb.CollectionReceipt.FirstOrDefault(x => x.CollectionReceipt_ID == id);
            // Return HTTP 404 Not Found when `collectionReceipt == null` is true.
            if (collectionReceipt == null)
            {
                return NotFound();
            }
            return Ok(collectionReceipt);
        }

        /// <summary>Adds the cloud collection receipt.</summary>
        [HttpPost("AddCloudCollectionReceipt")]
        public ActionResult<CollectionReceipt> AddCloudCollectionReceipt(CollectionReceipt collectionReceipt)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(collectionReceipt);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(collectionReceipt);
        }

        /// <summary>Updates the cloud collection receipt.</summary>
        [HttpPost("UpdateCloudCollectionReceipt")]
        public ActionResult<CollectionReceipt> UpdateCloudCollectionReceipt(Int32 Id, [FromBody] CollectionReceipt collectionReceipt)
        {
            // Return HTTP 400 Bad Request when `collectionReceipt == null` is true.
            if (collectionReceipt == null)
            {
                return BadRequest(collectionReceipt);
            }
            var updateCollectionReceipt = _CloudDb.CollectionReceipt.FirstOrDefault(x => x.CollectionReceipt_ID == Id);
            // Return HTTP 404 Not Found when `updateCollectionReceipt == null` is true.
            if (updateCollectionReceipt == null)
            {
                return NotFound();
            }
            updateCollectionReceipt.CollectionReceipt_ID = collectionReceipt.CollectionReceipt_ID;
            updateCollectionReceipt.FileName = collectionReceipt.FileName;
            updateCollectionReceipt.FileExtension = collectionReceipt.FileExtension;
            updateCollectionReceipt.FileData = collectionReceipt.FileData;
            updateCollectionReceipt.JobOrderID = collectionReceipt.JobOrderID;
            updateCollectionReceipt.Status = collectionReceipt.Status;

            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(updateCollectionReceipt);
        }

        /// <summary>Deletes the cloud collection receipt.</summary>
        [HttpPut("DeleteCloudCollectionReceipt")]
        public IActionResult DeleteCloudCollectionReceipt(int id)
        {
            var collectionReceipt = _CloudDb.CollectionReceipt.FirstOrDefault(x => x.CollectionReceipt_ID == id);
            // Return HTTP 404 Not Found when `collectionReceipt == null` is true.
            if (collectionReceipt == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(collectionReceipt);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- COLLECTION RECEIPT ---------------------------

        // ------------------------------- ISSUED BY ---------------------------

        [HttpGet("GetLocalIssuedBy")]
        // Retrieves all local issued by. Returns List<IssuedBy>.
        public List<IssuedBy> GetAllLocalIssuedBy()
        {
            return _Localdb.IssuedBy.ToList();
        }

        /// <summary>Retrieves the local issued by id.</summary>
        [HttpGet("GetLocalIssuedByID")]
        public ActionResult<IssuedBy> GetLocalIssuedByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var issuedBy = _Localdb.IssuedBy.FirstOrDefault(x => x.FileID == id);
            // Return HTTP 404 Not Found when `issuedBy == null` is true.
            if (issuedBy == null)
            {
                return NotFound();
            }
            return Ok(issuedBy);
        }

        /// <summary>Adds the local issued by.</summary>
        [HttpPost("AddLocalIssuedBy")]
        public ActionResult<IssuedBy> AddLocalIssuedBy(IssuedBy issuedBy)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(issuedBy);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(issuedBy);
        }

        /// <summary>Updates the local issued by.</summary>
        [HttpPost("UpdateLocalIssuedBy")]
        public ActionResult<IssuedBy> UpdateLocalIssuedBy(Int32 Id, [FromBody] IssuedBy issuedBy)
        {
            // Return HTTP 400 Bad Request when `issuedBy == null` is true.
            if (issuedBy == null)
            {
                return BadRequest(issuedBy);
            }
            var updateIssuedBy = _Localdb.IssuedBy.FirstOrDefault(x => x.FileID == Id);
            // Return HTTP 404 Not Found when `updateIssuedBy == null` is true.
            if (updateIssuedBy == null)
            {
                return NotFound();
            }
            updateIssuedBy.FileID = issuedBy.FileID;
            updateIssuedBy.FileName = issuedBy.FileName;
            updateIssuedBy.FileExtension = issuedBy.FileExtension;
            updateIssuedBy.FileData = issuedBy.FileData;
            updateIssuedBy.CollectionReceipt_ID = issuedBy.CollectionReceipt_ID;

            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(updateIssuedBy);
        }

        /// <summary>Deletes the local issued by.</summary>
        [HttpPut("DeleteLocalIssuedBy")]
        public IActionResult DeleteLocalIssuedBy(int id)
        {
            var issuedBy = _Localdb.IssuedBy.FirstOrDefault(x => x.FileID == id);
            // Return HTTP 404 Not Found when `issuedBy == null` is true.
            if (issuedBy == null)
            {
                return NotFound();
            }
            _Localdb.Remove(issuedBy);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return NoContent();
        }

        /// <summary>Retrieves all issued-by records from the cloud database.</summary>
        [HttpGet("GetCloudIssuedBy")]
        public List<IssuedBy> GetAllCloudIssuedBy()
        {
            return _CloudDb.IssuedBy.ToList();
        }

        /// <summary>Retrieves the cloud issued by id.</summary>
        [HttpGet("GetCloudIssuedByID")]
        public ActionResult<IssuedBy> GetCloudIssuedByID(int id)
        {
            // Return HTTP 400 Bad Request when `id == 0` is true.
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var issuedBy = _CloudDb.IssuedBy.FirstOrDefault(x => x.FileID == id);
            // Return HTTP 404 Not Found when `issuedBy == null` is true.
            if (issuedBy == null)
            {
                return NotFound();
            }
            return Ok(issuedBy);
        }

        /// <summary>Adds the cloud issued by.</summary>
        [HttpPost("AddCloudIssuedBy")]
        public ActionResult<IssuedBy> AddCloudIssuedBy(IssuedBy issuedBy)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(issuedBy);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(issuedBy);
        }

        /// <summary>Updates the cloud issued by.</summary>
        [HttpPost("UpdateCloudIssuedBy")]
        public ActionResult<IssuedBy> UpdateCloudIssuedBy(Int32 Id, [FromBody] IssuedBy issuedBy)
        {
            // Return HTTP 400 Bad Request when `issuedBy == null` is true.
            if (issuedBy == null)
            {
                return BadRequest(issuedBy);
            }
            var updateIssuedBy = _CloudDb.IssuedBy.FirstOrDefault(x => x.FileID == Id);
            // Return HTTP 404 Not Found when `updateIssuedBy == null` is true.
            if (updateIssuedBy == null)
            {
                return NotFound();
            }
            updateIssuedBy.FileID = issuedBy.FileID;
            updateIssuedBy.FileName = issuedBy.FileName;
            updateIssuedBy.FileExtension = issuedBy.FileExtension;
            updateIssuedBy.FileData = issuedBy.FileData;
            updateIssuedBy.CollectionReceipt_ID = issuedBy.CollectionReceipt_ID;

            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return Ok(updateIssuedBy);
        }

        /// <summary>Deletes the cloud issued by.</summary>
        [HttpPut("DeleteCloudIssuedBy")]
        public IActionResult DeleteCloudIssuedBy(int id)
        {
            var issuedBy = _CloudDb.IssuedBy.FirstOrDefault(x => x.FileID == id);
            // Return HTTP 404 Not Found when `issuedBy == null` is true.
            if (issuedBy == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(issuedBy);
            // Persist the pending entity changes to the database at this point.
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- ISSUED BY ---------------------------

        [Authorize(Policy = "admin")]   // adjust to your admin policy name
        /// <summary>Encrypts the existing files.</summary>
        [HttpPost("EncryptExistingFiles")]
        public IActionResult EncryptExistingFiles()
        {
            var result = new
            {
                PurchaseOrder = EncryptTable(_Localdb.PurchaseOrder, x => x.FileID, x => x.FileData, (x, d) => x.FileData = d),
                Graphics = EncryptTable(_Localdb.Graphics, x => x.FileID, x => x.FileData, (x, d) => x.FileData = d),
                DeliveryFiles = EncryptTable(_Localdb.DeliveryFiles, x => x.File_ID, x => x.File_Data, (x, d) => x.File_Data = d),
                InstallationReportFiles = EncryptTable(_Localdb.InstallationReportFiles, x => x.InstallationReportFile_ID, x => x.File_Data, (x, d) => x.File_Data = d),
                CollectionReceipt = EncryptTable(_Localdb.CollectionReceipt, x => x.CollectionReceipt_ID, x => x.FileData, (x, d) => x.FileData = d)
            };
            return Ok(result);
        }

        // Performs the encrypt table operation. Inputs: set (set); idSelector (id selector); get (get); put (put). Returns int.
        private int EncryptTable<T>(
            DbSet<T> set,
            System.Linq.Expressions.Expression<Func<T, int>> idSelector,
            Func<T, byte[]?> get,
            Action<T, byte[]> put) where T : class
        {
            int count = 0;
            var ids = set.AsNoTracking().Select(idSelector).ToList();

            // Process each `id` returned by the collection expression in the loop header.
            foreach (var id in ids)
            {
                var row = set.Find(id);
                var data = row == null ? null : get(row);

                // Enter this branch when `row != null && data is { Length: > 0 } && !EncryptionService.IsProtected(data)` is true.
                if (row != null && data is { Length: > 0 } && !EncryptionService.IsProtected(data))
                {
                    put(row, _encryptionService.Encrypt(data));
                    // Persist the pending entity changes to the database at this point.
                    _Localdb.SaveChanges();
                    count++;
                }
                _Localdb.ChangeTracker.Clear();
            }
            return count;
        }

    }
}
