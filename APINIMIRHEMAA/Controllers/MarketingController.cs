using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO.InstallationReport;
using APINIMIRHEMAA.DTO.InstallationSchedule;
using APINIMIRHEMAA.DTO.JobOrders;
using APINIMIRHEMAA.DTO.QuotationItems;
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
    public class MarketingController : ControllerBase
    {
        private LocalDbContext _Localdb;
        private CloudDbContext _CloudDb;
        private readonly EncryptionService _encryptionService;
        public MarketingController(LocalDbContext Localcontext, CloudDbContext Cloudcontext, EncryptionService encryptionService)
        {
            _Localdb = Localcontext;
            _CloudDb = Cloudcontext;
            _encryptionService = encryptionService;
        }

        // ------------------------------- QUOTATION -------------------------------

        [HttpGet("GetLocalQuotations")]
        public async Task<ActionResult<List<Quotation>>> GetAllLocalQuotations()
        {
            var quotations = await _Localdb.Quotation.ToListAsync();
            var subjectsForResponse = new Dictionary<int, string>();
            var hasLegacyPlaintextSubjects = false;
            foreach (var quotation in quotations)
            {
                if (string.IsNullOrWhiteSpace(quotation.Client_Subject))
                {
                    continue;
                }

                if (quotation.Client_Subject.StartsWith("CfDJ8", StringComparison.Ordinal))
                {
                    var readableSubject = _encryptionService.TryDecrypt(quotation.Client_Subject);
                    if (string.IsNullOrWhiteSpace(readableSubject))
                    {
                        return StatusCode(
                            StatusCodes.Status500InternalServerError,
                            $"The Client Subject for quotation {quotation.Quotation_ID} could not be decrypted. Verify the ASP.NET Data Protection keys.");
                    }
                    subjectsForResponse[quotation.Quotation_ID] = readableSubject;
                }
                else
                {
                    subjectsForResponse[quotation.Quotation_ID] = quotation.Client_Subject;
                    quotation.Client_Subject = _encryptionService.Encrypt(quotation.Client_Subject);
                    hasLegacyPlaintextSubjects = true;
                }
            }

            if (hasLegacyPlaintextSubjects)
            {
                await _Localdb.SaveChangesAsync();
            }

            foreach (var quotation in quotations)
            {
                _Localdb.Entry(quotation).State = EntityState.Detached;
                if (subjectsForResponse.TryGetValue(quotation.Quotation_ID, out var subject))
                {
                    quotation.Client_Subject = subject;
                }
            }

            return Ok(quotations);
        }

        [HttpGet("GetLocalQuotationsByID")]
        public ActionResult<Quotation> GetLocalQuotationDetails(Int32 Id)
        {
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var QuotationId = _Localdb.Quotation.FirstOrDefault(x => x.Client_ID == Id);
            if (QuotationId == null)
            {
                return NotFound("Client not found.");
            }
            return QuotationId;
        }

        [HttpGet("PreviewLocalClientQuotationSubject")]
        public async Task<ActionResult> PreviewLocalClientQuotationSubject([FromQuery] int clientId)
        {
            if (clientId <= 0)
            {
                return BadRequest("A valid client ID is required.");
            }
            if (!await _Localdb.Clients.AsNoTracking().AnyAsync(record => record.Client_ID == clientId))
            {
                return BadRequest($"Client {clientId} was not found.");
            }

            var employeeName = _Localdb.GetCurrentEmployeeName().Trim();
            var initial = employeeName.FirstOrDefault(char.IsLetter);
            if (initial == default)
            {
                return BadRequest("The signed-in employee name is unavailable, so a Client Subject cannot be generated.");
            }

            var clientSubject = await PreviewClientQuotationSubjectAsync(_Localdb, clientId, initial);
            return Ok(new { clientSubject });
        }

        [HttpPost("ReserveLocalClientQuotationSubject")]
        public async Task<ActionResult> ReserveLocalClientQuotationSubject([FromQuery] int clientId)
        {
            if (clientId <= 0)
            {
                return BadRequest("A valid client ID is required.");
            }

            var clientExists = await _Localdb.Clients
                .AsNoTracking()
                .AnyAsync(record => record.Client_ID == clientId);
            if (!clientExists)
            {
                return BadRequest($"Client {clientId} was not found.");
            }

            var employeeName = _Localdb.GetCurrentEmployeeName().Trim();
            var initial = employeeName.FirstOrDefault(char.IsLetter);
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

        [HttpPost("AddLocalQuotations")]
        public async Task<ActionResult<Quotation>> AddLocalQuotations([FromBody] Quotation QuotationDetails)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var clientExists = await _Localdb.Clients
                .AsNoTracking()
                .AnyAsync(record => record.Client_ID == QuotationDetails.Client_ID);
            if (!clientExists)
            {
                return BadRequest($"Client {QuotationDetails.Client_ID} was not found.");
            }

            var employeeName = _Localdb.GetCurrentEmployeeName().Trim();
            var now = DateTime.Now;
            await using var transaction = await _Localdb.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            ClientQuotationReservation reservation;
            string clientSubject;
            if (QuotationDetails.Client_SubjectReservation_ID.HasValue)
            {
                reservation = await _Localdb.ClientQuotationReservations.FirstOrDefaultAsync(record =>
                    record.Reservation_ID == QuotationDetails.Client_SubjectReservation_ID.Value &&
                    record.Client_ID == QuotationDetails.Client_ID);
                if (reservation == null)
                {
                    return BadRequest("The Client Subject reservation is invalid for the selected client.");
                }
                if (reservation.IsUsed)
                {
                    return Conflict("This Client Subject reservation has already been used.");
                }
                if (!string.Equals(reservation.ReservedBy, employeeName, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
                clientSubject = ReadClientSubject(reservation.Client_Subject);
            }
            else
            {
                var initial = employeeName.FirstOrDefault(char.IsLetter);
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
            _Localdb.Quotation.Add(QuotationDetails);
            await _Localdb.SaveChangesAsync();
            await transaction.CommitAsync();
            _Localdb.Entry(QuotationDetails).State = EntityState.Detached;
            QuotationDetails.Client_Subject = clientSubject;
            return Ok(QuotationDetails);
        }

        [HttpPost("UpdateLocalQuotations")]
        public ActionResult<Quotation> UpdateLocalQuotations(Int32 Id, [FromBody] Quotation UpdateQuotation)
        {
            if (UpdateQuotation == null)
            {
                return BadRequest(UpdateQuotation);
            }
            var updateQuotation = _Localdb.Quotation.FirstOrDefault(x => x.Quotation_ID == Id);
            if (updateQuotation == null)
            {
                return NotFound();
            }

            updateQuotation.Status = UpdateQuotation.Status;

            _Localdb.SaveChanges();
            return Ok(UpdateQuotation);
        }

        [HttpPut ("DeleteQuotation")]
        public ActionResult<Quotation> DeleteLocalQuotations(Int32 Id)
        {
            var quotationDetails = _Localdb.Quotation.FirstOrDefault(x => x.Quotation_ID == Id);
            if (quotationDetails == null)
            {
                return NotFound();
            }
            _Localdb.Remove(quotationDetails);
            _Localdb.SaveChanges();
            return NoContent();
        }

        [HttpGet("GetCloudQuotations")]
        public async Task<ActionResult<List<Quotation>>> GetAllCloudQuotations()
        {
            var quotations = await _CloudDb.Quotation.ToListAsync();
            var subjectsForResponse = new Dictionary<int, string>();
            var hasLegacyPlaintextSubjects = false;
            foreach (var quotation in quotations)
            {
                if (string.IsNullOrWhiteSpace(quotation.Client_Subject))
                {
                    continue;
                }

                if (quotation.Client_Subject.StartsWith("CfDJ8", StringComparison.Ordinal))
                {
                    var readableSubject = _encryptionService.TryDecrypt(quotation.Client_Subject);
                    if (string.IsNullOrWhiteSpace(readableSubject))
                    {
                        return StatusCode(
                            StatusCodes.Status500InternalServerError,
                            $"The Client Subject for quotation {quotation.Quotation_ID} could not be decrypted. Verify the ASP.NET Data Protection keys.");
                    }
                    subjectsForResponse[quotation.Quotation_ID] = readableSubject;
                }
                else
                {
                    subjectsForResponse[quotation.Quotation_ID] = quotation.Client_Subject;
                    quotation.Client_Subject = _encryptionService.Encrypt(quotation.Client_Subject);
                    hasLegacyPlaintextSubjects = true;
                }
            }

            if (hasLegacyPlaintextSubjects)
            {
                await _CloudDb.SaveChangesAsync();
            }

            foreach (var quotation in quotations)
            {
                _CloudDb.Entry(quotation).State = EntityState.Detached;
                if (subjectsForResponse.TryGetValue(quotation.Quotation_ID, out var subject))
                {
                    quotation.Client_Subject = subject;
                }
            }

            return Ok(quotations);
        }

        [HttpGet("GetCloudQuotationsByID")]
        public ActionResult<Quotation> GetCloudQuotationDetails(Int32 Id)
        {
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var QuotationId = _CloudDb.Quotation.FirstOrDefault(x => x.Client_ID == Id);
            if (QuotationId == null)
            {
                return NotFound("Client not found.");
            }
            return QuotationId;
        }

        [HttpGet("PreviewCloudClientQuotationSubject")]
        public async Task<ActionResult> PreviewCloudClientQuotationSubject([FromQuery] int clientId)
        {
            if (clientId <= 0)
            {
                return BadRequest("A valid client ID is required.");
            }
            if (!await _CloudDb.Clients.AsNoTracking().AnyAsync(record => record.Client_ID == clientId))
            {
                return BadRequest($"Client {clientId} was not found.");
            }

            var employeeName = _Localdb.GetCurrentEmployeeName().Trim();
            var initial = employeeName.FirstOrDefault(char.IsLetter);
            if (initial == default)
            {
                return BadRequest("The signed-in employee name is unavailable, so a Client Subject cannot be generated.");
            }

            var clientSubject = await PreviewClientQuotationSubjectAsync(_CloudDb, clientId, initial);
            return Ok(new { clientSubject });
        }

        [HttpPost("ReserveCloudClientQuotationSubject")]
        public async Task<ActionResult> ReserveCloudClientQuotationSubject([FromQuery] int clientId)
        {
            if (clientId <= 0)
            {
                return BadRequest("A valid client ID is required.");
            }

            var clientExists = await _CloudDb.Clients
                .AsNoTracking()
                .AnyAsync(record => record.Client_ID == clientId);
            if (!clientExists)
            {
                return BadRequest($"Client {clientId} was not found.");
            }

            var employeeName = _Localdb.GetCurrentEmployeeName().Trim();
            var initial = employeeName.FirstOrDefault(char.IsLetter);
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

        [HttpPost("AddCloudQuotations")]
        public async Task<ActionResult<Quotation>> AddCloudQuotations([FromBody] Quotation QuotationDetails)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var clientExists = await _CloudDb.Clients
                .AsNoTracking()
                .AnyAsync(record => record.Client_ID == QuotationDetails.Client_ID);
            if (!clientExists)
            {
                return BadRequest($"Client {QuotationDetails.Client_ID} was not found.");
            }

            var employeeName = _Localdb.GetCurrentEmployeeName().Trim();
            var now = DateTime.Now;
            await using var transaction = await _CloudDb.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            ClientQuotationReservation reservation;
            string clientSubject;
            if (QuotationDetails.Client_SubjectReservation_ID.HasValue)
            {
                reservation = await _CloudDb.ClientQuotationReservations.FirstOrDefaultAsync(record =>
                    record.Reservation_ID == QuotationDetails.Client_SubjectReservation_ID.Value &&
                    record.Client_ID == QuotationDetails.Client_ID);
                if (reservation == null)
                {
                    return BadRequest("The Client Subject reservation is invalid for the selected client.");
                }
                if (reservation.IsUsed)
                {
                    return Conflict("This Client Subject reservation has already been used.");
                }
                if (!string.Equals(reservation.ReservedBy, employeeName, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
                clientSubject = ReadClientSubject(reservation.Client_Subject);
            }
            else
            {
                var initial = employeeName.FirstOrDefault(char.IsLetter);
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
            await _CloudDb.SaveChangesAsync();
            await transaction.CommitAsync();
            _CloudDb.Entry(QuotationDetails).State = EntityState.Detached;
            QuotationDetails.Client_Subject = clientSubject;
            return Ok(QuotationDetails);
        }

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

        private async Task<string> PreviewClientQuotationSubjectAsync(DbContext db, int clientId, char initial)
        {
            var now = DateTime.Now;
            var lastSequence = await db.Set<ClientQuotationSequence>()
                .AsNoTracking()
                .Where(record =>
                    record.Client_ID == clientId &&
                    record.SequenceYear == now.Year &&
                    record.SequenceMonth == now.Month)
                .Select(record => (int?)record.LastSequence)
                .FirstOrDefaultAsync();
            var nextSequence = checked((lastSequence ?? 0) + 1);
            return $"{now.Month:D2}{char.ToUpperInvariant(initial)}{nextSequence:D3}";
        }

        private async Task<(ClientQuotationReservation Reservation, string ClientSubject)> ReserveClientQuotationSubjectWithinTransactionAsync(
            DbContext db,
            int clientId,
            string employeeName,
            char initial)
        {
            var now = DateTime.Now;
            var year = now.Year;
            var month = now.Month;
            var sequence = await db.Set<ClientQuotationSequence>().FirstOrDefaultAsync(record =>
                record.Client_ID == clientId &&
                record.SequenceYear == year &&
                record.SequenceMonth == month);
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
            await db.SaveChangesAsync();
            return (reservation, clientSubject);
        }

        private string ReadClientSubject(string? storedSubject)
        {
            if (string.IsNullOrEmpty(storedSubject))
            {
                return string.Empty;
            }

            return storedSubject.StartsWith("CfDJ8", StringComparison.Ordinal)
                ? _encryptionService.TryDecrypt(storedSubject)
                : storedSubject;
        }

        [HttpPost("UpdateCloudQuotations")]
        public ActionResult<Quotation> UpdateCloudQuotations(Int32 Id, [FromBody] Quotation UpdateQuotation)
        {
            if (UpdateQuotation == null)
            {
                return BadRequest(UpdateQuotation);
            }
            var updateQuotation = _CloudDb.Quotation.FirstOrDefault(x => x.Quotation_ID == Id);
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

            _CloudDb.SaveChanges();
            return Ok(UpdateQuotation);
        }

        [HttpPut("DeleteCloudQuotation")]
        public ActionResult<Quotation> DeleteCloudQuotations(Int32 Id)
        {
            var quotationDetails = _CloudDb.Quotation.FirstOrDefault(x => x.Quotation_ID == Id);
            if (quotationDetails == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(quotationDetails);
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- QUOTATION -------------------------------

        // ------------------------------- QUOTATION ITEMS -------------------------------


        [HttpGet("GetLocalQuotationItems")]
        public List<Quotation_Items> GetAllLocalQuotationsItems()
        {
            return _Localdb.Quotation_Items
                .Include(item => item.Materials)
                .ToList();
        }

        [HttpGet("GetLocalQuotationItemsByID")]
        public ActionResult<Quotation_Items> GetLocalQuotationItemDetails(Int32 Id)
        {
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var QI_Id = _Localdb.Quotation_Items
                .Include(item => item.Materials)
                .FirstOrDefault(x => x.QI_ID == Id);
            if (QI_Id == null)
            {
                return NotFound("Client not found.");
            }
            return QI_Id;
        }

        [HttpGet("GetLocalQuotationItemMaterials")]
        public async Task<ActionResult<List<QuotationItemMaterial>>> GetLocalQuotationItemMaterials(int quotationItemId)
        {
            if (quotationItemId <= 0)
            {
                return BadRequest("A valid quotation item ID is required.");
            }
            if (!await _Localdb.Quotation_Items.AnyAsync(item => item.QI_ID == quotationItemId))
            {
                return NotFound("Quotation item not found.");
            }

            var materials = await _Localdb.QuotationItemMaterials
                .AsNoTracking()
                .Where(material => material.QI_ID == quotationItemId)
                .OrderBy(material => material.Code_Name)
                .ToListAsync();
            return Ok(materials);
        }

        [HttpPost("AddLocalQuotationItems")]
        public async Task<ActionResult<Quotation_Items>> AddLocalQuotationsItems([FromBody] AddQuotationItemDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (request.QS_ID <= 0)
            {
                return BadRequest("A valid quotation store ID is required.");
            }
            if (request.Materials.Any(material => material.Material_ID <= 0) ||
                request.Materials.Select(material => material.Material_ID).Distinct().Count() != request.Materials.Count)
            {
                return BadRequest("Selected materials must have valid, unique material IDs.");
            }

            var materialIds = request.Materials.Select(material => material.Material_ID).ToList();
            var materialsById = await _Localdb.Materials
                .Where(material => materialIds.Contains(material.Material_ID))
                .ToDictionaryAsync(material => material.Material_ID);
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

            _Localdb.Quotation_Items.Add(quotationItem);
            await _Localdb.SaveChangesAsync();
            return Ok(quotationItem);
        }

        [HttpPost("UpdateLocalQuotationItems")]
        public ActionResult<Quotation_Items> UpdateLocalQuotationItems(Int32 Id, [FromBody] Quotation_Items UpdateQuotationItems)
        {
            if (UpdateQuotationItems == null)
            {
                return BadRequest(UpdateQuotationItems);
            }
            var updateQuotationItems = _Localdb.Quotation_Items.FirstOrDefault(x => x.QI_ID == Id);
            if (updateQuotationItems == null)
            {
                return NotFound();
            }

            updateQuotationItems.QI_ID = UpdateQuotationItems.QI_ID;
            updateQuotationItems.Description = UpdateQuotationItems.Description;
            updateQuotationItems.Width = UpdateQuotationItems.Width;
            updateQuotationItems.Height = UpdateQuotationItems.Height;
            updateQuotationItems.Measurement_Unit = UpdateQuotationItems.Measurement_Unit;
            updateQuotationItems.Quantity = UpdateQuotationItems.Quantity;
            updateQuotationItems.Price_Per_Square_Inch = UpdateQuotationItems.Price_Per_Square_Inch;
            updateQuotationItems.Unit_Price = UpdateQuotationItems.Unit_Price;
            updateQuotationItems.Total_Amount = UpdateQuotationItems.Total_Amount;
            updateQuotationItems.QS_ID = UpdateQuotationItems.QS_ID;


            _Localdb.SaveChanges();
            return Ok(UpdateQuotationItems);
        }

        [HttpPut("DeleteQuotationItems")]
        public ActionResult<Quotation_Items> DeleteLocalQuotationItems(Int32 Id)
        {
            var quotationItemDetails = _Localdb.Quotation_Items.FirstOrDefault(x => x.QI_ID == Id);
            if (quotationItemDetails == null)
            {
                return NotFound();
            }
            _Localdb.Remove(quotationItemDetails);
            _Localdb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- QUOTATION ITEMS -------------------------------

        // ------------------------------- QUOTATION STORE -------------------------------


        [HttpGet("GetLocalQuotationStore")]
        public List<Quotation_Store> GetAllLocalQuotationsStore()
        {
            return _Localdb.Quotation_Store.ToList();
        }

        [HttpGet("GetLocalQuotationStoreByID")]
        public ActionResult<Quotation_Store> GetLocalQuotationStoreDetails(Int32 Id)
        {
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var QS_Id = _Localdb.Quotation_Store.FirstOrDefault(x => x.QS_ID == Id);
            if (QS_Id == null)
            {
                return NotFound("Client not found.");
            }
            return QS_Id;
        }

        [HttpPost("AddLocalQuotationStore")]
        public ActionResult<Quotation_Store> AddLocalQuotationStore([FromBody] Quotation_Store QuotationStoreDetails)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Quotation_Store.Add(QuotationStoreDetails);
            _Localdb.SaveChanges();
            return Ok(QuotationStoreDetails);
        }

        [HttpPost("UpdateLocalQuotationStore")]
        public ActionResult<Quotation_Store> UpdateLocalQuotationStore(Int32 Id, [FromBody] Quotation_Store UpdateQuotationStore)
        {
            if (UpdateQuotationStore == null)
            {
                return BadRequest(UpdateQuotationStore);
            }
            var updateQuotationStore = _Localdb.Quotation_Store.FirstOrDefault(x => x.QS_ID == Id);
            if (updateQuotationStore == null)
            {
                return NotFound();
            }

            UpdateQuotationStore.QS_ID = UpdateQuotationStore.QS_ID;
            UpdateQuotationStore.Store = UpdateQuotationStore.Store;
            UpdateQuotationStore.Quotation_ID = UpdateQuotationStore.Quotation_ID;


            _Localdb.SaveChanges();
            return Ok(UpdateQuotationStore);
        }

        [HttpPut("DeleteQuotationStore")]
        public ActionResult<Quotation_Store> DeleteLocalQuotationStore(Int32 Id)
        {
            var quotationStoreDetails = _Localdb.Quotation_Store.FirstOrDefault(x => x.QS_ID == Id);
            if (quotationStoreDetails == null)
            {
                return NotFound();
            }
            _Localdb.Remove(quotationStoreDetails);
            _Localdb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- QUOTATION STORE -------------------------------


        // ------------------------------- CONFORME -------------------------------

        [HttpGet("GetLocalConforme")]
        public List<Conforme> GetAllLocalConforme()
        {
            var conforme = _Localdb.Conforme.AsNoTracking().ToList();

            foreach (var _conforme in conforme)
            {
                _conforme.FIleData = _encryptionService.Decrypt(_conforme.FIleData);

            }

            return conforme;


        }

        [HttpGet("GetLocalConformeByID")]
        public ActionResult<Conforme> GetLocalConformeByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var conforme = _Localdb.Conforme.AsNoTracking().FirstOrDefault(x => x.FileID == id);
            if (conforme == null) return NotFound();
            conforme.FIleData = _encryptionService.TryDecrypt(conforme.FIleData);
            return Ok(conforme);
        }

        [HttpPost ("AddLocalConforme")]
        public ActionResult<Conforme> AddLocalConforme(Conforme conforme)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            conforme.FIleData = _encryptionService.Encrypt(conforme.FIleData);

            _Localdb.Conforme.Add(conforme);
            _Localdb.SaveChanges();
            return Ok(conforme);
        }

        [HttpPost("AddLocalConformeBatch")]
        [RequestSizeLimit(100_000_000)]
        public ActionResult<List<int>> AddLocalConformeBatch([FromBody] List<Conforme> files)
        {
            if (files == null || files.Count == 0) return BadRequest("No files.");

            for (int i = 0; i < files.Count; i++)
            {
                var error = UploadValidator.ValidatePdf(files[i].FIleData);
                if (error != null) return BadRequest($"File {i + 1}: {error}");
            }

            foreach (var f in files)
            {
                f.FIleData = _encryptionService.Encrypt(f.FIleData);
                _Localdb.Conforme.Add(f);
            }

            _Localdb.SaveChanges();   // one call = all rows or none
            return Ok(files.Select(f => f.FileID).ToList());
        }


        [HttpPost ("UpdateLocalConforme")]
        public ActionResult<Conforme> UpdateLocalConforme(Int32 Id, [FromBody] Conforme _conforme)
        {
            if (_conforme == null)
            {
                return BadRequest(_conforme);
            }
            var updateConforme = _Localdb.Conforme.FirstOrDefault(x => x.FileID == Id);
            if (updateConforme == null)
            {
                return NotFound();
            }

            updateConforme.Status = _conforme.Status;

            _Localdb.SaveChanges();
            return Ok(updateConforme);
        }

        [HttpPut ("DeleteLocalConforme")]
        public ActionResult<Conforme> DeleteLocalConforme(int id)
        {
            var conforme = _Localdb.Conforme.FirstOrDefault(x => x.FileID == id);
            if (conforme == null)
            {
                return NotFound();
            }
            _Localdb.Remove(conforme);
            _Localdb.SaveChanges();
            return NoContent();
        }


        [HttpGet("GetCloudConforme")]
        public List<Conforme> GetAllCloudConforme()
        {
            return _CloudDb.Conforme.ToList();
        }

        [HttpGet("GetCloudConformeByID")]
        public ActionResult<Conforme> GetCloudConformeByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var conforme = _CloudDb.Conforme.FirstOrDefault(x => x.FileID == id);
            if (conforme == null)
            {
                return NotFound();
            }
            return Ok(conforme);
        }

        [HttpPost("AddCloudConforme")]
        public ActionResult<Conforme> AddCloudConforme(Conforme conforme)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Conforme.Add(conforme);
            _CloudDb.SaveChanges();
            return Ok(conforme);
        }

        [HttpPost("UpdateCloudConforme")]
        public ActionResult<Quotation> UpdateCloudConforme(Int32 Id, [FromBody] Conforme _conforme)
        {
            if (_conforme == null)
            {
                return BadRequest(_conforme);
            }
            var updateConforme = _CloudDb.Conforme.FirstOrDefault(x => x.FileID == Id);
            if (updateConforme == null)
            {
                return NotFound();
            }
            updateConforme.Quotation_ID = _conforme.Quotation_ID;
            updateConforme.FileName = _conforme.FileName;
            updateConforme.FileExtension = _conforme.FileExtension;
            updateConforme.FIleData = _conforme.FIleData;
            updateConforme.Quotation_ID = _conforme.Quotation_ID;

            _CloudDb.SaveChanges();
            return Ok(updateConforme);
        }

        [HttpPut("DeleteCloudConforme")]
        public ActionResult<Conforme> DeleteCloudConforme(int id)
        {
            var conforme = _CloudDb.Conforme.FirstOrDefault(x => x.FileID == id);
            if (conforme == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(conforme);
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- CONFORME -------------------------------

        // ------------------------------- JOB ORDER -------------------------------

        [HttpGet("GetLocalJobOrder")]
        public List<JobOrder> GetAllJobOrder()
        {
            return _Localdb.JobOrder
                .AsNoTracking()
                .Include(jobOrder => jobOrder.Comments)
                .ToList();
        }

        [HttpGet("GetLocalJobOrderItemsByJobOrderID")]
        public async Task<ActionResult<List<JobOrderItems>>> GetLocalJobOrderItemsByJobOrderID(int jobOrderId)
        {
            if (jobOrderId <= 0)
            {
                return BadRequest("Invalid Job Order ID.");
            }

            if (!await _Localdb.JobOrder.AnyAsync(jobOrder => jobOrder.JobOrder_ID == jobOrderId))
            {
                return NotFound("Job Order not found.");
            }

            var items = await _Localdb.JobOrderItems
                .AsNoTracking()
                .Where(item => item.JobOrder_ID == jobOrderId)
                .OrderBy(item => item.JobOrderItems_ID)
                .ToListAsync();
            return Ok(items);
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

            if (!await _Localdb.JobOrder.AnyAsync(jobOrder => jobOrder.JobOrder_ID == request.JobOrder_ID))
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
            return Ok(comment);
        }

        [HttpGet("GetInstallationReportJobOrders")]
        public async Task<ActionResult<IReadOnlyList<InstallationReportJobOrderOption>>> GetInstallationReportJobOrders()
        {
            var jobOrders = await _Localdb.JobOrder
                .AsNoTracking()
                .OrderByDescending(jobOrder => jobOrder.JobOrder_ID)
                .ToListAsync();

            if (jobOrders.Count == 0)
            {
                return Ok(Array.Empty<InstallationReportJobOrderOption>());
            }

            var conformeIds = jobOrders.Select(jobOrder => jobOrder.Conforme_FileID).Distinct().ToArray();
            var conformes = await _Localdb.Conforme
                .AsNoTracking()
                .Where(conforme => conformeIds.Contains(conforme.FileID))
                .Select(conforme => new { conforme.FileID, conforme.Quotation_ID })
                .ToDictionaryAsync(conforme => conforme.FileID);

            var quotationIds = conformes.Values.Select(conforme => conforme.Quotation_ID).Distinct().ToArray();
            var quotations = await _Localdb.Quotation
                .AsNoTracking()
                .Where(quotation => quotationIds.Contains(quotation.Quotation_ID))
                .Select(quotation => new { quotation.Quotation_ID, quotation.Client_ID, quotation.Title })
                .ToDictionaryAsync(quotation => quotation.Quotation_ID);

            var clientIds = quotations.Values.Select(quotation => quotation.Client_ID).Distinct().ToArray();
            var clients = await _Localdb.Clients
                .AsNoTracking()
                .Where(client => clientIds.Contains(client.Client_ID))
                .Select(client => new { client.Client_ID, client.Client_Name, client.Company_Address })
                .ToDictionaryAsync(client => client.Client_ID);

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
        public async Task<ActionResult> AddLocalInstallationReport([FromBody] CreateInstallationReportRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var jobOrderExists = await _Localdb.JobOrder
                .AsNoTracking()
                .AnyAsync(jobOrder => jobOrder.JobOrder_ID == request.JobOrderId);
            if (!jobOrderExists)
            {
                return NotFound("The selected Job Order was not found.");
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
                ApprovedBy = request.ApprovedBy
            };

            _Localdb.InstallationReport.Add(report);
            await _Localdb.SaveChangesAsync();

            return Ok(new
            {
                installationReportId = report.InstallationReport_ID,
                jobOrderId = report.JobOrder_ID
            });
        }

        [HttpGet("GetLocalJobOrderByID")]
        public ActionResult<JobOrder> GetLocalJobOrderByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var jobOrder = _Localdb.JobOrder
                .Include(x => x.Comments)
                .FirstOrDefault(x => x.JobOrder_ID == id);
            if (jobOrder == null)
            {
                return NotFound();
            }
            return Ok(jobOrder);
        }

        [HttpPost("AddLocalJobOrder")]
        public ActionResult<JobOrder> AddLocalJobOrder([FromBody] CreateJobOrderRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var specialInstruction = request.SpecialInstruction?.Trim();
            var employeeName = string.IsNullOrWhiteSpace(specialInstruction)
                ? ""
                : _Localdb.GetCurrentEmployeeName().Trim();
            if (!string.IsNullOrWhiteSpace(specialInstruction) && string.IsNullOrWhiteSpace(employeeName))
            {
                return BadRequest("The signed-in employee name is unavailable, so the special instruction cannot be saved.");
            }

            var conforme = _Localdb.Conforme.FirstOrDefault(x => x.FileID == request.Conforme_FileID);
            if (conforme == null)
            {
                return BadRequest($"Conforme {request.Conforme_FileID} was not found.");
            }

            var jobOrder = new JobOrder
            {
                Conforme_FileID = request.Conforme_FileID,
                Title = request.Title,
                Installation_Date = request.Installation_Date,
                Target_Delivery = request.Target_Delivery,
                Date_Delivered = request.Date_Delivered,
                Tiling = request.Tiling,
                Eyelet = request.Eyelet,
                Bleeding = request.Bleeding,
                Status = request.Status
            };
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
            foreach (var purchaseOrder in _Localdb.PurchaseOrder.Where(x => x.Conforme_FileID == request.Conforme_FileID))
            {
                purchaseOrder.Status = "Completed";
            }
            _Localdb.SaveChanges();
            return Ok(jobOrder);
        }

        [HttpPost("UpdateLocalJobOrder")]
        public ActionResult<JobOrder> UpdateLocalJobOrder(Int32 Id, [FromBody] JobOrder _jobOrder) 
        {
            if (_jobOrder == null)
            {
                return BadRequest(_jobOrder);
            }
            var updateJobOrder = _Localdb.JobOrder.FirstOrDefault(x => x.JobOrder_ID == Id);
            if (updateJobOrder == null)
            {
                return NotFound();
            }

            updateJobOrder.Status = _jobOrder.Status;


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

            var conforme = _CloudDb.Conforme.FirstOrDefault(x => x.FileID == jobOrder.Conforme_FileID);
            if (conforme == null)
            {
                return BadRequest($"Conforme {jobOrder.Conforme_FileID} was not found.");
            }

            _CloudDb.Add(jobOrder);
            conforme.Status = "Completed";
            foreach (var purchaseOrder in _CloudDb.PurchaseOrder.Where(x => x.Conforme_FileID == jobOrder.Conforme_FileID))
            {
                purchaseOrder.Status = "Completed";
            }
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

        // ------------------------------- JOB ORDER -------------------------------

        // ------------------------------- JOB ORDER ITEMS -------------------------------

        [HttpGet("GetLocalJobOrderItems")]
        public List<JobOrderItems> GetAllJobOrderItems()
        {
            return _Localdb.JobOrderItems.ToList();
        }

        [HttpGet("GetLocalJobOrderItemsByID")]
        public ActionResult<JobOrderItems> GetLocalJobOrderItemsByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var jobOrder = _Localdb.JobOrderItems.FirstOrDefault(x => x.JobOrderItems_ID == id);
            if (jobOrder == null)
            {
                return NotFound();
            }
            return Ok(jobOrder);
        }

        [HttpPost("AddLocalJobOrderItems")]
        public ActionResult<JobOrderItems> AddLocalJobOrderItems(JobOrderItems jobOrderItems)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(jobOrderItems);
            _Localdb.SaveChanges();
            return Ok(jobOrderItems);
        }

        [HttpPost("UpdateLocalJobOrderItems")]
        public ActionResult<JobOrderItems> UpdateLocalJobOrder(Int32 Id, [FromBody] JobOrderItems _jobOrderItems)
        {
            if (_jobOrderItems == null)
            {
                return BadRequest(_jobOrderItems);
            }
            var updateJobOrderItems = _Localdb.JobOrderItems.FirstOrDefault(x => x.JobOrderItems_ID == Id);
            if (updateJobOrderItems == null)
            {
                return NotFound();
            }

            updateJobOrderItems.Production_Initial = _jobOrderItems.Production_Initial;
            updateJobOrderItems.Artist_Initial = _jobOrderItems.Artist_Initial;
            updateJobOrderItems.Remarks = _jobOrderItems.Remarks;


            _Localdb.SaveChanges();
            return Ok(updateJobOrderItems);
        }

        [HttpPut("DeleteLocalJobOrderItems")]
        public ActionResult<JobOrderItems> DeleteLocalJobOrderItems(int id)
        {
            var jobOrderItems = _Localdb.JobOrderItems.FirstOrDefault(x => x.JobOrderItems_ID == id);
            if (jobOrderItems == null)
            {
                return NotFound();
            }
            _Localdb.Remove(jobOrderItems);
            _Localdb.SaveChanges();
            return NoContent();
        }


        [HttpGet("GetCloudJobOrderItems")]
        public List<JobOrderItems> GetAllCloudJobOrderItems()
        {
            return _Localdb.JobOrderItems.ToList();
        }

        [HttpGet("GetCloudJobOrderItemsByID")]
        public ActionResult<JobOrderItems> GetCloudJobOrderItemsByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var jobOrder = _CloudDb.JobOrderItems.FirstOrDefault(x => x.JobOrderItems_ID == id);
            if (jobOrder == null)
            {
                return NotFound();
            }
            return Ok(jobOrder);
        }

        [HttpPost("AddCloudJobOrderItems")]
        public ActionResult<JobOrderItems> AddCloudJobOrderItems(JobOrderItems jobOrderItems)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(jobOrderItems);
            _CloudDb.SaveChanges();
            return Ok(jobOrderItems);
        }

        [HttpPost("UpdateCloudJobOrderItems")]
        public ActionResult<JobOrderItems> UpdateCloudJobOrderItems(Int32 Id, [FromBody] JobOrderItems _jobOrderItems)
        {
            if (_jobOrderItems == null)
            {
                return BadRequest(_jobOrderItems);
            }
            var updateJobOrderItems = _CloudDb.JobOrderItems.FirstOrDefault(x => x.JobOrderItems_ID == Id);
            if (updateJobOrderItems == null)
            {
                return NotFound();
            }

            updateJobOrderItems.Production_Initial = _jobOrderItems.Production_Initial;
            updateJobOrderItems.Artist_Initial = _jobOrderItems.Artist_Initial;
            updateJobOrderItems.Remarks = _jobOrderItems.Remarks;



            _CloudDb.SaveChanges();
            return Ok(updateJobOrderItems);
        }

        [HttpPut("DeleteCloudJobOrderItems")]
        public ActionResult<JobOrderItems> DeleteCloudJobOrderItems(int id)
        {
            var jobOrderItems = _CloudDb.JobOrderItems.FirstOrDefault(x => x.JobOrderItems_ID == id);
            if (jobOrderItems == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(jobOrderItems);
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- JOB ORDER ITEMS -------------------------------


        // ------------------------------- PURCHASE ORDER -------------------------------

        [HttpGet("GetLocalPurchaseOrder")]
        public List<PurchaseOrder> GetAllPurchaseOrder()
        {
            var list = _Localdb.PurchaseOrder.AsNoTracking().ToList();
            foreach (var r in list) r.FileData = _encryptionService.TryDecrypt(r.FileData);
            return list;
        }

        [HttpGet("GetLocalPurchaseOrderByID")]
        public ActionResult<PurchaseOrder> GetLocalPurchaseOrderByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var purchaseOrder = _Localdb.PurchaseOrder.AsNoTracking().FirstOrDefault(x => x.FileID == id);
            if (purchaseOrder == null) return NotFound();
            purchaseOrder.FileData = _encryptionService.TryDecrypt(purchaseOrder.FileData);
            return Ok(purchaseOrder);
        }

        [HttpPost("AddLocalPurchaseOrder")]
        public ActionResult<PurchaseOrder> AddLocalPurchaseOrder(PurchaseOrder purchaseOrder)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var error = UploadValidator.ValidatePdf(purchaseOrder.FileData);
            if (error != null) return BadRequest(error);

            purchaseOrder.Status = "Pending";
            var plain = purchaseOrder.FileData;
            purchaseOrder.FileData = _encryptionService.Encrypt(plain);
            _Localdb.PurchaseOrder.Add(purchaseOrder);
            _Localdb.SaveChanges();
            purchaseOrder.FileData = plain;
            return Ok(purchaseOrder);
        }

        [HttpPost("AddLocalPurchaseOrderBatch")]
        [RequestSizeLimit(100_000_000)]
        public ActionResult<List<int>> AddLocalPurchaseOrderBatch([FromBody] List<PurchaseOrder> purchaseOrders)
        {
            if (purchaseOrders == null || purchaseOrders.Count == 0)
                return BadRequest("No files.");

            for (int i = 0; i < purchaseOrders.Count; i++)
            {
                var error = UploadValidator.ValidatePdf(purchaseOrders[i].FileData);
                if (error != null) return BadRequest($"File {i + 1}: {error}");
            }

            foreach (var f in purchaseOrders)
            {
                f.Status = "Pending";
                f.FileData = _encryptionService.Encrypt(f.FileData);
                _Localdb.PurchaseOrder.Add(f);
            }
            _Localdb.SaveChanges();
            return Ok(purchaseOrders.Select(f => f.FileID).ToList());
        }

        [HttpPost("UpdateLocalPurchaseOrder")]
        public ActionResult<PurchaseOrder> UpdateLocalPurchaseOrder(Int32 Id, [FromBody] PurchaseOrder _purchaseOrder)
        {
            if (_purchaseOrder == null)
            {
                return BadRequest(_purchaseOrder);
            }
            var updatePurchaseOrder = _Localdb.PurchaseOrder.FirstOrDefault(x => x.FileID == Id);
            if (updatePurchaseOrder == null)
            {
                return NotFound();
            }

            updatePurchaseOrder.Status = _purchaseOrder.Status;

            _Localdb.SaveChanges();
            return Ok(updatePurchaseOrder);
        }

        [HttpPut("DeleteLocalPurchaseOrder")]
        public ActionResult<PurchaseOrder> DeleteLocalPurchaseOrder(int id)
        {
            var purchaseOrder = _Localdb.PurchaseOrder.FirstOrDefault(x => x.FileID == id);
            if (purchaseOrder == null)
            {
                return NotFound();
            }
            _Localdb.Remove(purchaseOrder);
            _Localdb.SaveChanges();
            return NoContent();
        }


        [HttpGet("GetCloudPurchaseOrder")]
        public List<PurchaseOrder> GetAllCloudPurchaseOrder()
        {
            return _CloudDb.PurchaseOrder.ToList();
        }

        [HttpGet("GetCloudPurchaseOrderByID")]
        public ActionResult<PurchaseOrder> GetCloudPurchaseOrderByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var purchaseOrder = _CloudDb.PurchaseOrder.FirstOrDefault(x => x.FileID == id);
            if (purchaseOrder == null)
            {
                return NotFound();
            }
            return Ok(purchaseOrder);
        }

        [HttpPost("AddCloudPurchaseOrder")]
        public ActionResult<PurchaseOrder> AddCloudPurchaseOrder(PurchaseOrder purchaseOrder)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            purchaseOrder.Status = "Pending";
            _CloudDb.Add(purchaseOrder);
            _CloudDb.SaveChanges();
            return Ok(purchaseOrder);
        }

        [HttpPost("UpdateCloudPurchaseOrder")]
        public ActionResult<PurchaseOrder> UpdateCloudPurchaseOrder(Int32 Id, [FromBody] PurchaseOrder _purchaseOrder)
        {
            if (_purchaseOrder == null)
            {
                return BadRequest(_purchaseOrder);
            }
            var updatePurchaseOrder = _CloudDb.PurchaseOrder.FirstOrDefault(x => x.FileID == Id);
            if (updatePurchaseOrder == null)
            {
                return NotFound();
            }
            updatePurchaseOrder.Conforme_FileID = _purchaseOrder.Conforme_FileID;
            updatePurchaseOrder.FileName = _purchaseOrder.FileName;
            updatePurchaseOrder.FileExtension = _purchaseOrder.FileExtension;
            updatePurchaseOrder.FileData = _purchaseOrder.FileData;
            _CloudDb.SaveChanges();
            return Ok(updatePurchaseOrder);
        }

        [HttpPut("DeleteCloudPurchaseOrder")]
        public ActionResult<PurchaseOrder> DeleteCloudPurchaseOrder(int id)
        {
            var purchaseOrder = _CloudDb.PurchaseOrder.FirstOrDefault(x => x.FileID == id);
            if (purchaseOrder == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(purchaseOrder);
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- PURCHASE ORDER -------------------------------

        // ------------------------------- GRAPHICS -------------------------------

        [HttpGet("GetLocalGraphics")]
        public List<Graphics> GetAllGraphics()
        {
            var list = _Localdb.Graphics.AsNoTracking().ToList();
            foreach (var r in list) r.FileData = _encryptionService.TryDecrypt(r.FileData);
            return list;
        }

        [HttpGet("GetLocalGraphicsByID")]
        public ActionResult<Graphics> GetLocalGraphicsByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var graphics = _Localdb.Graphics.AsNoTracking().FirstOrDefault(x => x.FileID == id);
            if (graphics == null) return NotFound();
            graphics.FileData = _encryptionService.TryDecrypt(graphics.FileData);
            return Ok(graphics);
        }

        [HttpPost("AddLocalGraphics")]
        public ActionResult<Graphics> AddLocalGraphics(Graphics graphics)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var error = UploadValidator.ValidatePdf(graphics.FileData);   // ValidateImage for Collection Receipt, ValidateImageOrPdf for Graphics, ValidateImage for Delivery Files
            if (error != null) return BadRequest(error);

            var plain = graphics.FileData;
            graphics.FileData = _encryptionService.Encrypt(plain);
            _Localdb.Graphics.Add(graphics);
            _Localdb.SaveChanges();
            graphics.FileData = plain;
            return Ok(graphics);
        }

        [HttpPost("AddLocalGraphicsBatch")]
        [RequestSizeLimit(100_000_000)]
        public ActionResult<List<int>> AddLocalGraphicsBatch([FromBody] List<Graphics> graphicsFiles)
        {
            if (graphicsFiles == null || graphicsFiles.Count == 0)
                return BadRequest("No files.");

            for (int i = 0; i < graphicsFiles.Count; i++)
            {
                var error = UploadValidator.ValidateImageOrPdf(graphicsFiles[i].FileData);
                if (error != null) return BadRequest($"File {i + 1}: {error}");
            }

            foreach (var f in graphicsFiles)
            {
                f.FileData = _encryptionService.Encrypt(f.FileData);
                _Localdb.Graphics.Add(f);
            }
            _Localdb.SaveChanges();
            return Ok(graphicsFiles.Select(f => f.FileID).ToList());
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

            updateGraphics.FileData = _encryptionService.Encrypt(_graphics.FileData);

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

        // ------------------------------- GRAPHICS -------------------------------

        // ------------------------------- SERVICE INVOICE ---------------------------

        [HttpGet("GetLocalServiceInvoice")]
        public List<ServiceInvoice> GetAllLocalServiceInvoice()
        {
            return _Localdb.ServiceInvoice.ToList();
        }

        [HttpGet("GetLocalServiceInvoiceByID")]
        public ActionResult<ServiceInvoice> GetLocalServiceInvoiceID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid service invoice ID.");
            }
            var serviceInvoice = _Localdb.ServiceInvoice.FirstOrDefault(x => x.Quantity == id);
            if (serviceInvoice == null)
            {
                return NotFound();
            }
            return Ok(serviceInvoice);
        }

        [HttpPost("AddLocalServiceInvoice")]
        public ActionResult<ServiceInvoice> AddLocalServiceInvoice([FromBody] ServiceInvoice serviceInvoice)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (serviceInvoice.JobOrder_ID is not > 0)
            {
                return BadRequest("A valid Job Order is required for a Service Invoice.");
            }
            if (!_Localdb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == serviceInvoice.JobOrder_ID))
            {
                return NotFound("The selected Job Order was not found.");
            }
            _Localdb.ServiceInvoice.Add(serviceInvoice);
            _Localdb.SaveChanges();
            return Ok(serviceInvoice);
        }

        [HttpPost("UpdateLocalServiceInvoice")]
        public ActionResult<ServiceInvoice> UpdateLocalServiceInvoice(Int32 Id, [FromBody] ServiceInvoice serviceInvoice)
        {
            if (serviceInvoice == null)
            {
                return BadRequest(serviceInvoice);
            }
            var updateServiceInvoice = _Localdb.ServiceInvoice.FirstOrDefault(x => x.Quantity == Id);
            if (updateServiceInvoice == null)
            {
                return NotFound();
            }
            if (serviceInvoice.JobOrder_ID is not > 0)
            {
                return BadRequest("A valid Job Order is required for a Service Invoice.");
            }
            if (!_Localdb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == serviceInvoice.JobOrder_ID))
            {
                return NotFound("The selected Job Order was not found.");
            }
            updateServiceInvoice.Quantity = serviceInvoice.Quantity;
            updateServiceInvoice.JobOrder_ID = serviceInvoice.JobOrder_ID;
            updateServiceInvoice.Unit = serviceInvoice.Unit;
            updateServiceInvoice.Description = serviceInvoice.Description;
            updateServiceInvoice.Unit_Price = serviceInvoice.Unit_Price;
            updateServiceInvoice.Amount = serviceInvoice.Amount;
            updateServiceInvoice.Status = serviceInvoice.Status;


            _Localdb.SaveChanges();
            return Ok(updateServiceInvoice);
        }

        [HttpPut ("DeleteLocalServiceInvoice")]
        public ActionResult DeleteLocalServiceInvoice(int id)
        {
            var serviceInvoice = _Localdb.ServiceInvoice.FirstOrDefault(x => x.Quantity == id);
            if (serviceInvoice == null)
            {
                return NotFound();
            }
            _Localdb.Remove(serviceInvoice);
            _Localdb.SaveChanges();
            return NoContent();
        }

        [HttpGet("GetCloudServiceInvoice")]
        public List<ServiceInvoice> GetAllCloudServiceInvoice()
        {
            return _CloudDb.ServiceInvoice.ToList();
        }

        [HttpGet("GetCloudServiceInvoiceByID")]
        public ActionResult<ServiceInvoice> GetCloudServiceInvoiceID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid service invoice ID.");
            }
            var serviceInvoice = _CloudDb.ServiceInvoice.FirstOrDefault(x => x.Quantity == id);
            if (serviceInvoice == null)
            {
                return NotFound();
            }
            return Ok(serviceInvoice);
        }

        [HttpPost("AddCloudServiceInvoice")]
        public ActionResult<ServiceInvoice> AddCloudServiceInvoice([FromBody] ServiceInvoice serviceInvoice)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (serviceInvoice.JobOrder_ID is not > 0)
            {
                return BadRequest("A valid Job Order is required for a Service Invoice.");
            }
            if (!_CloudDb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == serviceInvoice.JobOrder_ID))
            {
                return NotFound("The selected Job Order was not found.");
            }
            _CloudDb.ServiceInvoice.Add(serviceInvoice);
            _CloudDb.SaveChanges();
            return Ok(serviceInvoice);
        }

        [HttpPost("UpdateCloudServiceInvoice")]
        public ActionResult<ServiceInvoice> UpdateCloudServiceInvoice(Int32 Id, [FromBody] ServiceInvoice serviceInvoice)
        {
            if (serviceInvoice == null)
            {
                return BadRequest(serviceInvoice);
            }
            var updateServiceInvoice = _CloudDb.ServiceInvoice.FirstOrDefault(x => x.Quantity == Id);
            if (updateServiceInvoice == null)
            {
                return NotFound();
            }
            if (serviceInvoice.JobOrder_ID is not > 0)
            {
                return BadRequest("A valid Job Order is required for a Service Invoice.");
            }
            if (!_CloudDb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == serviceInvoice.JobOrder_ID))
            {
                return NotFound("The selected Job Order was not found.");
            }
            updateServiceInvoice.Quantity = serviceInvoice.Quantity;
            updateServiceInvoice.JobOrder_ID = serviceInvoice.JobOrder_ID;
            updateServiceInvoice.Unit = serviceInvoice.Unit;
            updateServiceInvoice.Description = serviceInvoice.Description;
            updateServiceInvoice.Unit_Price = serviceInvoice.Unit_Price;
            updateServiceInvoice.Amount = serviceInvoice.Amount;
            updateServiceInvoice.Status = serviceInvoice.Status;
            _CloudDb.SaveChanges();
            return Ok(updateServiceInvoice);
        }
        [HttpPut("DeleteCloudServiceInvoice")]
        public ActionResult DeleteCloudServiceInvoice(int id)
        {
            var serviceInvoice = _CloudDb.ServiceInvoice.FirstOrDefault(x => x.Quantity == id);
            if (serviceInvoice == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(serviceInvoice);
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- SERVICE INVOICE ---------------------------

        // ------------------------------- INSTALLATION SCHEDULE ---------------------------

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

        [HttpGet("GetLocalInstallationScheduleEmployeeAssignments")]
        public ActionResult<List<InstallationScheduleEmployeeAssignmentDto>> GetLocalInstallationScheduleEmployeeAssignments()
        {
            var assignments = (
                from assignment in _Localdb.Installation_Schedule_Employee.AsNoTracking()
                join employee in _Localdb.Employee.AsNoTracking()
                    on assignment.Employee_Id equals employee.Employee_Id
                orderby assignment.Installation_ID, employee.LastName, employee.FirstName
                select new InstallationScheduleEmployeeAssignmentDto
                {
                    Installation_ID = assignment.Installation_ID,
                    Employee_Id = employee.Employee_Id,
                    FirstName = employee.FirstName,
                    LastName = employee.LastName
                })
                .ToList();

            return Ok(assignments);
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

        // ------------------------------- INSTALLATION SCHEDULE ---------------------------

        // ------------------------------- DELIVERY RECEIPT ---------------------------

        [HttpGet("GetLocalDeliveryReceipts")]
        public List<DeliveryReceipt> GetAllLocalDeliveryReceipts()
        {
            return _Localdb.DeliveryReceipt.ToList();
        }

        [HttpGet("GetLocalDeliveryReceiptsByID")]
        public ActionResult<DeliveryReceipt> GetLocalDeliveryReceiptsID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var deliveryReceipt = _Localdb.DeliveryReceipt.FirstOrDefault(x => x.Delivery_ID == id);
            if (deliveryReceipt == null)
            {
                return NotFound();
            }
            return Ok(deliveryReceipt);
        }

        [HttpPost("AddLocalDeliveryReceipts")]
        public ActionResult<DeliveryReceipt> AddLocalDeliveryReceipts(DeliveryReceipt deliveryReceipt)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(deliveryReceipt);
            _Localdb.SaveChanges();
            return Ok(deliveryReceipt);
        }

        [HttpPost("UpdateLocalDeliveryReceipts")]
        public ActionResult<DeliveryReceipt> UpdateLocalDeliveryReceipts(Int32 Id, [FromBody] DeliveryReceipt deliveryReceipt)
        {
            if (deliveryReceipt == null)
            {
                return BadRequest(deliveryReceipt);
            }
            var updateDeliveryReceipt = _Localdb.DeliveryReceipt.FirstOrDefault(x => x.Delivery_ID == Id);
            if (updateDeliveryReceipt == null)
            {
                return NotFound();
            }

            updateDeliveryReceipt.Status = deliveryReceipt.Status;

            _Localdb.SaveChanges();
            return Ok(updateDeliveryReceipt);
        }

        [HttpPut("DeleteLocalDeliveryReceipts")]
        public ActionResult DeleteLocalDeliveryReceipts(int id)
        {
            var deliveryReceipt = _Localdb.DeliveryReceipt.FirstOrDefault(x => x.Delivery_ID == id);
            if (deliveryReceipt == null)
            {
                return NotFound();
            }
            _Localdb.Remove(deliveryReceipt);
            _Localdb.SaveChanges();
            return NoContent();
        }


        [HttpGet("GetCloudDeliveryReceipts")]
        public List<DeliveryReceipt> GetAllCloudDeliveryReceipts()
        {
            return _Localdb.DeliveryReceipt.ToList();
        }

        [HttpGet("GetCloudDeliveryReceiptsByID")]
        public ActionResult<DeliveryReceipt> GetCloudDeliveryReceiptsID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var deliveryReceipt = _CloudDb.DeliveryReceipt.FirstOrDefault(x => x.Delivery_ID == id);
            if (deliveryReceipt == null)
            {
                return NotFound();
            }
            return Ok(deliveryReceipt);
        }

        [HttpPost("AddCloudDeliveryReceipts")]
        public ActionResult<DeliveryReceipt> AddCloudDeliveryReceipts(DeliveryReceipt deliveryReceipt)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(deliveryReceipt);
            _CloudDb.SaveChanges();
            return Ok(deliveryReceipt);
        }

        [HttpPost("UpdateCloudDeliveryReceipts")]
        public ActionResult<DeliveryReceipt> UpdateCloudDeliveryReceipts(Int32 Id, [FromBody] DeliveryReceipt deliveryReceipt)
        {
            if (deliveryReceipt == null)
            {
                return BadRequest(deliveryReceipt);
            }
            var updateDeliveryReceipt = _CloudDb.DeliveryReceipt.FirstOrDefault(x => x.Delivery_ID == Id);
            if (updateDeliveryReceipt == null)
            {
                return NotFound();
            }
            updateDeliveryReceipt.Status = deliveryReceipt.Status;
            _CloudDb.SaveChanges();
            return Ok(updateDeliveryReceipt);
        }

        [HttpPut("DeleteCloudDeliveryReceipts")]
        public ActionResult DeleteCloudDeliveryReceipts(int id)
        {
            var deliveryReceipt = _CloudDb.DeliveryReceipt.FirstOrDefault(x => x.Delivery_ID == id);
            if (deliveryReceipt == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(deliveryReceipt);
            _CloudDb.SaveChanges();
            return NoContent();
        }


        // ------------------------------- DELIVERY RECEIPT ---------------------------

        // ------------------------------- DELIVERY RECEIPT ITEMS ---------------------------

        [HttpGet("GetLocalDeliveryReceiptsItems")]
        public List<DeliveryReceiptItems> GetAllLocalDeliveryReceiptsItems()
        {
            return _Localdb.DeliveryReceiptItems.ToList();
        }

        [HttpGet("GetLocalDeliveryReceiptsItemsByID")]
        public ActionResult<DeliveryReceiptItems> GetLocalDeliveryReceiptsItemsID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var deliveryReceipt = _Localdb.DeliveryReceiptItems.FirstOrDefault(x => x.DeliveryReceiptItems_ID == id);
            if (deliveryReceipt == null)
            {
                return NotFound();
            }
            return Ok(deliveryReceipt);
        }

        [HttpPost("AddLocalDeliveryReceiptsItems")]
        public ActionResult<DeliveryReceiptItems> AddLocalDeliveryReceiptsItems(DeliveryReceiptItems deliveryReceiptItems)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(deliveryReceiptItems);
            _Localdb.SaveChanges();
            return Ok(deliveryReceiptItems);
        }

        [HttpPut("DeleteLocalDeliveryReceiptsItems")]
        public ActionResult DeleteLocalDeliveryReceiptItems(int id)
        {
            var deliveryReceiptItems = _Localdb.DeliveryReceiptItems.FirstOrDefault(x => x.DeliveryReceiptItems_ID == id);
            if (deliveryReceiptItems == null)
            {
                return NotFound();
            }
            _Localdb.Remove(deliveryReceiptItems);
            _Localdb.SaveChanges();
            return NoContent();
        }
        // ------------------------------- DELIVERY RECEIPT ITEMS---------------------------

        // ------------------------------- DELIVERY FILES ---------------------------

        [HttpGet("GetLocalDeliveryFiles")]
        public List<DeliveryFiles> GetAllDeliveryFiles()
        {
            var list = _Localdb.DeliveryFiles.AsNoTracking().ToList();
            foreach (var r in list) r.File_Data = _encryptionService.TryDecrypt(r.File_Data);
            return list;
        }

        [HttpGet("GetLocalDeliveryFilesByID")]
        public ActionResult<DeliveryFiles> GetLocalDeliveryFilesByID(int id)
        {
            var deliveryFiles = _Localdb.DeliveryFiles.AsNoTracking().FirstOrDefault(x => x.File_ID == id);
            if (deliveryFiles == null) return NotFound();
            deliveryFiles.File_Data = _encryptionService.TryDecrypt(deliveryFiles.File_Data);
            return Ok(deliveryFiles);
        }

        [HttpPost("AddLocalDeliveryFiles")]
        public ActionResult<DeliveryFiles> AddLocalDeliveryFiles(DeliveryFiles deliveryFiles)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var error = UploadValidator.ValidatePdf(deliveryFiles.File_Data);   // ValidateImage for Collection Receipt, ValidateImageOrPdf for Graphics, ValidateImage for Delivery Files
            if (error != null) return BadRequest(error);

            var plain = deliveryFiles.File_Data;
            deliveryFiles.File_Data = _encryptionService.Encrypt(plain);
            _Localdb.DeliveryFiles.Add(deliveryFiles);
            _Localdb.SaveChanges();
            deliveryFiles.File_Data = plain;
            return Ok(deliveryFiles);
        }

        [HttpPost("AddLocalDeliveryFilesBatch")]
        [RequestSizeLimit(100_000_000)]
        public ActionResult<List<int>> AddLocalDeliveryFilesBatch([FromBody] List<DeliveryFiles> files)
        {
            if (files == null || files.Count == 0) return BadRequest("No files.");

            for (int i = 0; i < files.Count; i++)
            {
                var error = UploadValidator.ValidateImage(files[i].File_Data);
                if (error != null) return BadRequest($"File {i + 1}: {error}");
            }

            foreach (var f in files)
            {
                f.File_Data = _encryptionService.Encrypt(f.File_Data);
                _Localdb.DeliveryFiles.Add(f);
            }
            _Localdb.SaveChanges();
            return Ok(files.Select(f => f.File_ID).ToList());
        }

        [HttpPost("UpdateLocalDeliveryFiles")]
        public ActionResult<DeliveryFiles> UpdateLocalDeliveryFiles(Int32 Id, [FromBody] DeliveryFiles deliveryFiles)
        {
            if (deliveryFiles == null)
            {
                return BadRequest(deliveryFiles);
            }
            var updateDeliveryFiles = _Localdb.DeliveryFiles.FirstOrDefault(x => x.File_ID == Id);
            if (updateDeliveryFiles == null)
            {
                return NotFound();
            }
            updateDeliveryFiles.File_Data = _encryptionService.Encrypt(deliveryFiles.File_Data);
            _Localdb.SaveChanges();
            return Ok(updateDeliveryFiles);
        }

        [HttpPut ("DeleteLocalDeliveryFiles")]
        public ActionResult DeleteLocalDeliveryFiles(int id)
        {
            var deliveryFiles = _Localdb.DeliveryFiles.FirstOrDefault(x => x.File_ID == id);
            if (deliveryFiles == null)
            {
                return NotFound();
            }
            _Localdb.Remove(deliveryFiles);
            _Localdb.SaveChanges();
            return NoContent();
        }

        [HttpGet("GetCloudDeliveryFiles")]
        public List<DeliveryFiles> GetAllCloudDeliveryFiles()
        {
            return _CloudDb.DeliveryFiles.ToList();
        }

        [HttpGet("GetCloudDeliveryFilesByID")]
        public ActionResult<DeliveryFiles> GetCloudDeliveryFilesByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var deliveryFiles = _CloudDb.DeliveryFiles.FirstOrDefault(x => x.File_ID == id);
            if (deliveryFiles == null)
            {
                return NotFound();
            }
            return Ok(deliveryFiles);
        }

        [HttpPost("AddCloudDeliveryFiles")]
        public ActionResult<DeliveryFiles> AddCloudDeliveryFiles(DeliveryFiles deliveryFiles)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(deliveryFiles);
            _CloudDb.SaveChanges();
            return Ok(deliveryFiles);
        }

        [HttpPost("UpdateCloudDeliveryFiles")]
        public ActionResult<DeliveryFiles> UpdateCloudDeliveryFiles(Int32 Id, [FromBody] DeliveryFiles deliveryFiles)
        {
            if (deliveryFiles == null)
            {
                return BadRequest(deliveryFiles);
            }
            var updateDeliveryFiles = _CloudDb.DeliveryFiles.FirstOrDefault(x => x.File_ID == Id);
            if (updateDeliveryFiles == null)
            {
                return NotFound();
            }
            updateDeliveryFiles.File_ID = deliveryFiles.File_ID;
            updateDeliveryFiles.Delivery_ID = deliveryFiles.Delivery_ID;
            updateDeliveryFiles.File_Role = deliveryFiles.File_Role;
            updateDeliveryFiles.File_Name = deliveryFiles.File_Name;
            updateDeliveryFiles.File_Extension = deliveryFiles.File_Extension;
            updateDeliveryFiles.File_Data = deliveryFiles.File_Data;
            _CloudDb.SaveChanges();
            return Ok(updateDeliveryFiles);
        }

        [HttpPut("DeleteCloudDeliveryFiles")]
        public ActionResult DeleteCloudDeliveryFiles(int id)
        {
            var deliveryFiles = _CloudDb.DeliveryFiles.FirstOrDefault(x => x.File_ID == id);
            if (deliveryFiles == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(deliveryFiles);
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- DELIVERY FILES ---------------------------

        // ------------------------------- COLLECTION RECEIPT ---------------------------

        [HttpGet("GetLocalCollectionReceipts")]
        public List<CollectionReceipt> GetAllLocalCollectionReceipts()
        {
            return _Localdb.CollectionReceipt.ToList();
        }

        [HttpGet("GetLocalCollectionReceiptsByID")]
        public ActionResult<CollectionReceipt> GetLocalCollectionReceiptsByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var collectionReceipt = _Localdb.CollectionReceipt.FirstOrDefault(x => x.CollectionReceipt_ID == id);
            if (collectionReceipt == null)
            {
                return NotFound();
            }
            return Ok(collectionReceipt);
        }

        [HttpPost("AddLocalCollectionReceipt")]
        public ActionResult<CollectionReceipt> AddLocalCollectionReceipt(CollectionReceipt collectionReceipt)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(collectionReceipt);
            _Localdb.SaveChanges();
            return Ok(collectionReceipt);
        }

        [HttpPost("AddLocalCollectionReceiptBatch")]
        [RequestSizeLimit(100_000_000)]
        public ActionResult<List<int>> AddLocalCollectionReceiptBatch([FromBody] List<CollectionReceipt> collectionReceiptsFiles)
        {
            if (collectionReceiptsFiles == null || collectionReceiptsFiles.Count == 0) return BadRequest("No files.");
            for (int i = 0; i < collectionReceiptsFiles.Count; i++)

            {
                var error = UploadValidator.ValidateImage(collectionReceiptsFiles[i].FileData);
                if (error != null) return BadRequest($"File {i + 1}: {error}");
            }

            _Localdb.CollectionReceipt.AddRange(collectionReceiptsFiles);
            _Localdb.SaveChanges();
            return Ok(collectionReceiptsFiles.Select(f => f.CollectionReceipt_ID).ToList());
        }

        [HttpPost("UpdateLocalCollectionReceipt")]
        public ActionResult<CollectionReceipt> UpdateLocalCollectionReceipt(Int32 Id, [FromBody] CollectionReceipt collectionReceipt)
        {
            if (collectionReceipt == null)
            {
                return BadRequest(collectionReceipt);
            }
            var updateCollectionReceipt = _Localdb.CollectionReceipt.FirstOrDefault(x => x.CollectionReceipt_ID == Id);
            if (updateCollectionReceipt == null)
            {
                return NotFound();
            }

            updateCollectionReceipt.Status = collectionReceipt.Status;

            _Localdb.SaveChanges();
            return Ok(updateCollectionReceipt);
        }

        [HttpPut ("DeleteLocalCollectionReceipt")]
        public IActionResult DeleteLocalCollectionReceipt(int id)
        {
            var collectionReceipt = _Localdb.CollectionReceipt.FirstOrDefault(x => x.CollectionReceipt_ID == id);
            if (collectionReceipt == null)
            {
                return NotFound();
            }
            _Localdb.Remove(collectionReceipt);
            _Localdb.SaveChanges();
            return NoContent();
        }


        [HttpGet("GetCloudCollectionReceipts")]
        public List<CollectionReceipt> GetAllCloudCollectionReceipts()
        {
            return _CloudDb.CollectionReceipt.ToList();
        }

        [HttpGet("GetCloudCollectionReceiptsByID")]
        public ActionResult<CollectionReceipt> GetCloudCollectionReceiptsByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var collectionReceipt = _CloudDb.CollectionReceipt.FirstOrDefault(x => x.CollectionReceipt_ID == id);
            if (collectionReceipt == null)
            {
                return NotFound();
            }
            return Ok(collectionReceipt);
        }

        [HttpPost("AddCloudCollectionReceipt")]
        public ActionResult<CollectionReceipt> AddCloudCollectionReceipt(CollectionReceipt collectionReceipt)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(collectionReceipt);
            _CloudDb.SaveChanges();
            return Ok(collectionReceipt);
        }

        [HttpPost("UpdateCloudCollectionReceipt")]
        public ActionResult<CollectionReceipt> UpdateCloudCollectionReceipt(Int32 Id, [FromBody] CollectionReceipt collectionReceipt)
        {
            if (collectionReceipt == null)
            {
                return BadRequest(collectionReceipt);
            }
            var updateCollectionReceipt = _CloudDb.CollectionReceipt.FirstOrDefault(x => x.CollectionReceipt_ID == Id);
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

            _CloudDb.SaveChanges();
            return Ok(updateCollectionReceipt);
        }

        [HttpPut("DeleteCloudCollectionReceipt")]
        public IActionResult DeleteCloudCollectionReceipt(int id)
        {
            var collectionReceipt = _CloudDb.CollectionReceipt.FirstOrDefault(x => x.CollectionReceipt_ID == id);
            if (collectionReceipt == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(collectionReceipt);
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- COLLECTION RECEIPT ---------------------------

        // ------------------------------- ISSUED BY ---------------------------

        [HttpGet("GetLocalIssuedBy")]
        public List<IssuedBy> GetAllLocalIssuedBy()
        {
            return _Localdb.IssuedBy.ToList();
        }

        [HttpGet("GetLocalIssuedByID")]
        public ActionResult<IssuedBy> GetLocalIssuedByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var issuedBy = _Localdb.IssuedBy.FirstOrDefault(x => x.FileID == id);
            if (issuedBy == null)
            {
                return NotFound();
            }
            return Ok(issuedBy);
        }

        [HttpPost("AddLocalIssuedBy")]
        public ActionResult<IssuedBy> AddLocalIssuedBy(IssuedBy issuedBy)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(issuedBy);
            _Localdb.SaveChanges();
            return Ok(issuedBy);
        }

        [HttpPost("UpdateLocalIssuedBy")]
        public ActionResult<IssuedBy> UpdateLocalIssuedBy(Int32 Id, [FromBody] IssuedBy issuedBy)
        {
            if (issuedBy == null)
            {
                return BadRequest(issuedBy);
            }
            var updateIssuedBy = _Localdb.IssuedBy.FirstOrDefault(x => x.FileID == Id);
            if (updateIssuedBy == null)
            {
                return NotFound();
            }
            updateIssuedBy.FileID = issuedBy.FileID;
            updateIssuedBy.FileName = issuedBy.FileName;
            updateIssuedBy.FileExtension = issuedBy.FileExtension;
            updateIssuedBy.FileData = issuedBy.FileData;
            updateIssuedBy.CollectionReceipt_ID = issuedBy.CollectionReceipt_ID;

            _Localdb.SaveChanges();
            return Ok(updateIssuedBy);
        }

        [HttpPut("DeleteLocalIssuedBy")]
        public IActionResult DeleteLocalIssuedBy(int id)
        {
            var issuedBy = _Localdb.IssuedBy.FirstOrDefault(x => x.FileID == id);
            if (issuedBy == null)
            {
                return NotFound();
            }
            _Localdb.Remove(issuedBy);
            _Localdb.SaveChanges();
            return NoContent();
        }

        [HttpGet("GetCloudIssuedBy")]
        public List<IssuedBy> GetAllCloudIssuedBy()
        {
            return _CloudDb.IssuedBy.ToList();
        }

        [HttpGet("GetCloudIssuedByID")]
        public ActionResult<IssuedBy> GetCloudIssuedByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var issuedBy = _CloudDb.IssuedBy.FirstOrDefault(x => x.FileID == id);
            if (issuedBy == null)
            {
                return NotFound();
            }
            return Ok(issuedBy);
        }

        [HttpPost("AddCloudIssuedBy")]
        public ActionResult<IssuedBy> AddCloudIssuedBy(IssuedBy issuedBy)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(issuedBy);
            _CloudDb.SaveChanges();
            return Ok(issuedBy);
        }

        [HttpPost("UpdateCloudIssuedBy")]
        public ActionResult<IssuedBy> UpdateCloudIssuedBy(Int32 Id, [FromBody] IssuedBy issuedBy)
        {
            if (issuedBy == null)
            {
                return BadRequest(issuedBy);
            }
            var updateIssuedBy = _CloudDb.IssuedBy.FirstOrDefault(x => x.FileID == Id);
            if (updateIssuedBy == null)
            {
                return NotFound();
            }
            updateIssuedBy.FileID = issuedBy.FileID;
            updateIssuedBy.FileName = issuedBy.FileName;
            updateIssuedBy.FileExtension = issuedBy.FileExtension;
            updateIssuedBy.FileData = issuedBy.FileData;
            updateIssuedBy.CollectionReceipt_ID = issuedBy.CollectionReceipt_ID;

            _CloudDb.SaveChanges();
            return Ok(updateIssuedBy);
        }

        [HttpPut("DeleteCloudIssuedBy")]
        public IActionResult DeleteCloudIssuedBy(int id)
        {
            var issuedBy = _CloudDb.IssuedBy.FirstOrDefault(x => x.FileID == id);
            if (issuedBy == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(issuedBy);
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // ------------------------------- ISSUED BY ---------------------------

        [Authorize(Policy = "admin")]   // adjust to your admin policy name
        [HttpPost("EncryptExistingFiles")]
        public IActionResult EncryptExistingFiles()
        {
            var result = new
            {
                PurchaseOrder = EncryptTable(_Localdb.PurchaseOrder, x => x.FileID, x => x.FileData, (x, d) => x.FileData = d),
                Graphics = EncryptTable(_Localdb.Graphics, x => x.FileID, x => x.FileData, (x, d) => x.FileData = d),
                DeliveryFiles = EncryptTable(_Localdb.DeliveryFiles, x => x.File_ID, x => x.File_Data, (x, d) => x.File_Data = d),
                CollectionReceipt = EncryptTable(_Localdb.CollectionReceipt, x => x.CollectionReceipt_ID, x => x.FileData, (x, d) => x.FileData = d)
            };
            return Ok(result);
        }

        private int EncryptTable<T>(
            DbSet<T> set,
            System.Linq.Expressions.Expression<Func<T, int>> idSelector,
            Func<T, byte[]?> get,
            Action<T, byte[]> put) where T : class
        {
            int count = 0;
            var ids = set.AsNoTracking().Select(idSelector).ToList();

            foreach (var id in ids)
            {
                var row = set.Find(id);
                var data = row == null ? null : get(row);

                if (row != null && data is { Length: > 0 } && !EncryptionService.IsProtected(data))
                {
                    put(row, _encryptionService.Encrypt(data));
                    _Localdb.SaveChanges();
                    count++;
                }
                _Localdb.ChangeTracker.Clear();
            }
            return count;
        }

    }
}
