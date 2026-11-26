using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO.ClientQuotationRequests;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize(Policy = "marketing")]
    [ApiController]
    [Route("api/[controller]")]
    public sealed class ClientQuotationRequestsController : ControllerBase
    {
        private const decimal PricePerSquareInch = 5.76m;
        private const decimal MaximumDatabaseCurrency = 9999999999999999.99m;
        private const int MaximumItemsPerRequest = 100;
        private const int MaximumGraphicsPerRequest = 5;
        private const long MaximumGraphicFileBytes = 10 * 1024 * 1024;
        private const long MaximumGraphicsTotalBytes = 50 * 1024 * 1024;
        private const long MaximumMultipartRequestBytes = 52 * 1024 * 1024;

        private readonly LocalDbContext _localDb;
        private readonly EncryptionService _encryptionService;

        public ClientQuotationRequestsController(
            LocalDbContext localDb,
            EncryptionService encryptionService)
        {
            _localDb = localDb;
            _encryptionService = encryptionService;
        }

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Submit(
            [FromBody] CreateClientQuotationRequestDto request,
            CancellationToken cancellationToken)
        {
            return await CreateRequestAsync(
                request,
                Array.Empty<IFormFile>(),
                requireGraphics: false,
                cancellationToken: cancellationToken);
        }

        [AllowAnonymous]
        [HttpPost("with-graphics")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(MaximumMultipartRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaximumMultipartRequestBytes)]
        public async Task<IActionResult> SubmitWithGraphics(
            [FromForm] CreateClientQuotationRequestFormDto form,
            CancellationToken cancellationToken)
        {
            List<ClientQuotationRequestItemDto>? items;
            try
            {
                items = JsonSerializer.Deserialize<List<ClientQuotationRequestItemDto>>(
                    form.ItemsJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                return BadRequest("The submitted quotation items are invalid.");
            }

            var request = new CreateClientQuotationRequestDto
            {
                CompanyName = form.CompanyName,
                Telephone = form.Telephone,
                PaymentTerms = form.PaymentTerms,
                BusinessStyle = form.BusinessStyle,
                ContactPerson = form.ContactPerson,
                EmailAddress = form.EmailAddress,
                Tin = form.Tin,
                CompanyAddress = form.CompanyAddress,
                TermsAndCondition = form.TermsAndCondition,
                ProjectTitle = form.ProjectTitle,
                Items = items ?? new List<ClientQuotationRequestItemDto>()
            };

            return await CreateRequestAsync(
                request,
                form.Graphics,
                requireGraphics: true,
                cancellationToken: cancellationToken);
        }

        private async Task<IActionResult> CreateRequestAsync(
            CreateClientQuotationRequestDto request,
            IReadOnlyCollection<IFormFile> uploadedFiles,
            bool requireGraphics,
            CancellationToken cancellationToken)
        {
            if (!TryValidateModel(request))
            {
                return ValidationProblem(ModelState);
            }

            if (request.Items == null ||
                request.Items.Count == 0 ||
                request.Items.Count > MaximumItemsPerRequest ||
                request.Items.Any(item => item == null))
            {
                return BadRequest($"A quotation must contain between 1 and {MaximumItemsPerRequest} items.");
            }

            if (requireGraphics && uploadedFiles.Count == 0)
            {
                return BadRequest("Upload at least one graphic with your quotation request.");
            }
            if (uploadedFiles.Count > MaximumGraphicsPerRequest)
            {
                return BadRequest($"Upload no more than {MaximumGraphicsPerRequest} graphics.");
            }

            long totalFileBytes = 0;
            var requestGraphics = new List<ClientQuotationRequestGraphic>();
            foreach (var file in uploadedFiles)
            {
                if (file == null || file.Length <= 0 || file.Length > MaximumGraphicFileBytes)
                {
                    return BadRequest("Each graphic must be non-empty and no larger than 10 MB.");
                }
                totalFileBytes += file.Length;
                if (totalFileBytes > MaximumGraphicsTotalBytes)
                {
                    return BadRequest("The combined graphics size must not exceed 50 MB.");
                }

                var safeFileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
                if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName.Length > 255)
                {
                    return BadRequest("A graphic file name is invalid or too long.");
                }
                var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
                await using var memoryStream = new MemoryStream((int)file.Length);
                await file.CopyToAsync(memoryStream, cancellationToken);
                var fileData = memoryStream.ToArray();
                if (!IsValidGraphicContent(extension, fileData))
                {
                    return BadRequest($"The file '{safeFileName}' is not a valid supported graphic.");
                }

                requestGraphics.Add(new ClientQuotationRequestGraphic
                {
                    FileName = safeFileName,
                    FileExtension = extension,
                    ContentType = GetGraphicContentType(extension),
                    FileData = _encryptionService.Encrypt(fileData)
                });
            }

            var items = request.Items.Select(item => new ClientQuotationRequestItemDto
            {
                Branch = item.Branch.Trim(),
                Requirement = item.Requirement.Trim(),
                Description = item.Description?.Trim() ?? string.Empty,
                Width = item.Width,
                Height = item.Height,
                Measurement = item.Measurement.Trim().ToLowerInvariant(),
                Quantity = item.Quantity
            }).ToList();

            if (!TryCalculateItems(items, out _, out var subtotal, out var calculationError))
            {
                return BadRequest(calculationError);
            }

            var vat = decimal.Round(subtotal * 0.12m, 2, MidpointRounding.AwayFromZero);
            var overallTotal = subtotal + vat;
            if (overallTotal > MaximumDatabaseCurrency)
            {
                return BadRequest("The quotation total is too large to save.");
            }

            var submittedAtUtc = DateTime.UtcNow;
            var requestRecord = new ClientQuotationRequest
            {
                Client_Name = request.CompanyName.Trim(),
                Client_Telephone = request.Telephone.Trim(),
                Contact_Person = request.ContactPerson.Trim(),
                Business_Style = request.BusinessStyle.Trim(),
                Client_EmailAddress = request.EmailAddress.Trim(),
                TermsAndCondition = request.TermsAndCondition.Trim(),
                Company_Address = request.CompanyAddress.Trim(),
                TIN = request.Tin.Trim(),
                Payment_Terms = request.PaymentTerms.Trim(),
                Project_Title = request.ProjectTitle.Trim(),
                Items_Json = JsonSerializer.Serialize(items),
                Sub_Total = subtotal,
                Delivery_Charge = 0,
                Installation_Charge = 0,
                VAT = vat,
                Overall_Total = overallTotal,
                SubmittedAtUtc = submittedAtUtc,
                Status = "Pending"
            };

            await using var transaction = await _localDb.Database.BeginTransactionAsync(cancellationToken);
            _localDb.ClientQuotationRequests.Add(requestRecord);
            await _localDb.SaveChangesAsync(cancellationToken);
            foreach (var graphic in requestGraphics)
            {
                graphic.Request_ID = requestRecord.Request_ID;
            }
            if (requestGraphics.Count > 0)
            {
                _localDb.ClientQuotationRequestGraphics.AddRange(requestGraphics);
                await _localDb.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);

            return StatusCode(StatusCodes.Status201Created, new
            {
                requestId = requestRecord.Request_ID,
                submittedAtUtc
            });
        }

        [HttpGet]
        public async Task<ActionResult<List<ClientQuotationRequestResponseDto>>> GetRequests(
            CancellationToken cancellationToken)
        {
            var currentUserId = _localDb.CurrentUserId;
            if (!currentUserId.HasValue)
            {
                return Unauthorized();
            }

            var query = _localDb.ClientQuotationRequests
                .AsNoTracking()
                .AsQueryable();

            if (!_localDb.IsDepartmentManager && !_localDb.HasGlobalRecordAccess)
            {
                query = query.Where(request =>
                    !request.ClaimedByUserId.HasValue ||
                    request.ClaimedByUserId == currentUserId.Value);
            }

            var requests = await query
                .OrderByDescending(request => request.SubmittedAtUtc)
                .ToListAsync(cancellationToken);

            return Ok(requests.Select(ToResponse).ToList());
        }

        [HttpPost("{requestId:int}/accept")]
        public async Task<IActionResult> AcceptRequest(
            int requestId,
            CancellationToken cancellationToken)
        {
            if (requestId <= 0)
            {
                return BadRequest("A valid client quotation request ID is required.");
            }

            var userId = _localDb.CurrentUserId;
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            await using var transaction = await _localDb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            var request = await _localDb.ClientQuotationRequests
                .FirstOrDefaultAsync(record => record.Request_ID == requestId, cancellationToken);
            if (request == null)
            {
                return NotFound("The client quotation request was not found.");
            }

            if (request.ClaimedByUserId == userId &&
                request.Client_ID.HasValue &&
                request.Quotation_ID.HasValue)
            {
                await transaction.CommitAsync(cancellationToken);
                return Ok(new
                {
                    requestId,
                    clientId = request.Client_ID,
                    quotationId = request.Quotation_ID,
                    status = request.Status
                });
            }

            if (!string.Equals(request.Status, "Pending", StringComparison.OrdinalIgnoreCase) ||
                request.ClaimedByUserId.HasValue)
            {
                return Conflict("This request has already been accepted by another employee.");
            }

            var requestItems = JsonSerializer.Deserialize<List<ClientQuotationRequestItemDto>>(request.Items_Json);
            if (requestItems == null)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "The saved quotation items could not be read.");
            }
            if (!TryCalculateItems(
                requestItems,
                out var itemResponses,
                out var subtotal,
                out var calculationError))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, calculationError);
            }

            var vat = decimal.Round(subtotal * 0.12m, 2, MidpointRounding.AwayFromZero);
            var overallTotal = subtotal + vat;
            if (overallTotal > MaximumDatabaseCurrency)
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "The saved quotation total is too large to create a project.");
            }

            var normalizedTin = NormalizeTin(request.TIN);
            var existingClients = await _localDb.Clients
                .IgnoreQueryFilters()
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            var client = existingClients.FirstOrDefault(record =>
                NormalizeTin(record.TIN) == normalizedTin);

            if (client == null)
            {
                client = new ClientEntity
                {
                    Client_Name = request.Client_Name,
                    Client_Telephone = request.Client_Telephone,
                    Contact_Person = request.Contact_Person,
                    Business_Style = request.Business_Style,
                    Client_EmailAddress = request.Client_EmailAddress,
                    TermsAndCondition = request.TermsAndCondition,
                    Company_Address = request.Company_Address,
                    TIN = request.TIN,
                    Payment_Terms = request.Payment_Terms,
                    Price_Per_Square_Inch = PricePerSquareInch
                };
                _localDb.Clients.Add(client);
                await _localDb.SaveChangesAsync(cancellationToken);
            }
            else if (!await _localDb.ClientAccessGrants.AnyAsync(
                grant => grant.Client_ID == client.Client_ID && grant.User_ID == userId.Value,
                cancellationToken))
            {
                _localDb.ClientAccessGrants.Add(new ClientAccessGrant
                {
                    Client_ID = client.Client_ID,
                    User_ID = userId.Value,
                    GrantedAtUtc = DateTime.UtcNow
                });
                await _localDb.SaveChangesAsync(cancellationToken);
            }

            var now = DateTime.UtcNow;
            var quotation = new Quotation
            {
                Client_ID = client.Client_ID,
                Client_Subject = _encryptionService.Encrypt($"CQR-{request.Request_ID:D6}"),
                Title = request.Project_Title,
                Sub_Total = subtotal,
                Delivery_Charge = 0,
                Installation_Charge = 0,
                VAT = vat,
                Overall_Total = overallTotal,
                Status = "Pending",
                Terms_Condition = request.TermsAndCondition,
                Start_Date = DateOnly.FromDateTime(now),
                Time = TimeOnly.FromDateTime(now)
            };
            _localDb.Quotation.Add(quotation);
            await _localDb.SaveChangesAsync(cancellationToken);

            var storesByBranch = new Dictionary<string, Quotation_Store>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in requestItems)
            {
                if (storesByBranch.ContainsKey(item.Branch))
                {
                    continue;
                }

                var store = new Quotation_Store
                {
                    Store = item.Branch.Trim(),
                    Quotation_ID = quotation.Quotation_ID
                };
                storesByBranch.Add(item.Branch, store);
                _localDb.Quotation_Store.Add(store);
            }
            await _localDb.SaveChangesAsync(cancellationToken);

            foreach (var item in itemResponses)
            {
                var store = storesByBranch[item.Branch];
                _localDb.Quotation_Items.Add(new Quotation_Items
                {
                    Requirements = item.Requirement,
                    Description = item.Description,
                    Width = item.Width,
                    Height = item.Height,
                    Measurement_Unit = item.Measurement,
                    Price_Per_Square_Inch = PricePerSquareInch,
                    Quantity = item.Quantity,
                    Unit_Price = item.UnitPrice,
                    Total_Amount = item.TotalAmount,
                    QS_ID = store.QS_ID
                });
            }

            var requestGraphics = await _localDb.ClientQuotationRequestGraphics
                .Where(graphic => graphic.Request_ID == requestId)
                .ToListAsync(cancellationToken);
            foreach (var requestGraphic in requestGraphics)
            {
                _localDb.Graphics.Add(new Graphics
                {
                    FileName = requestGraphic.FileName,
                    FileExtension = requestGraphic.FileExtension,
                    FileData = requestGraphic.FileData,
                    Quotation_ID = quotation.Quotation_ID
                });
            }
            if (requestGraphics.Count > 0)
            {
                _localDb.ClientQuotationRequestGraphics.RemoveRange(requestGraphics);
            }

            request.ClaimedByUserId = userId.Value;
            request.ClaimedAtUtc = now;
            request.Status = "Accepted";
            request.Client_ID = client.Client_ID;
            request.Quotation_ID = quotation.Quotation_ID;
            await _localDb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new
            {
                requestId,
                clientId = client.Client_ID,
                quotationId = quotation.Quotation_ID,
                status = request.Status
            });
        }

        private static bool IsValidGraphicContent(string extension, byte[] fileData)
        {
            switch (extension)
            {
                case ".pdf":
                    return fileData.Length >= 5 &&
                        Encoding.ASCII.GetString(fileData, 0, 5) == "%PDF-";
                case ".png":
                    return fileData.Length >= 8 &&
                        fileData.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
                case ".jpg":
                case ".jpeg":
                    return fileData.Length >= 3 &&
                        fileData[0] == 0xFF &&
                        fileData[1] == 0xD8 &&
                        fileData[2] == 0xFF;
                case ".svg":
                    return IsValidSvg(fileData);
                default:
                    return false;
            }
        }

        private static bool IsValidSvg(byte[] fileData)
        {
            try
            {
                using var stream = new MemoryStream(fileData);
                using var reader = XmlReader.Create(stream, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null
                });
                var root = XDocument.Load(reader).Root;
                if (root == null || !string.Equals(root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return !root.DescendantsAndSelf().Any(element =>
                    string.Equals(element.Name.LocalName, "script", StringComparison.OrdinalIgnoreCase) ||
                    element.Attributes().Any(attribute =>
                        attribute.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase) ||
                        attribute.Value.TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)));
            }
            catch (XmlException)
            {
                return false;
            }
        }

        private static string GetGraphicContentType(string extension)
        {
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
        }

        private static ClientQuotationRequestResponseDto ToResponse(ClientQuotationRequest request)
        {
            var items = JsonSerializer.Deserialize<List<ClientQuotationRequestItemDto>>(request.Items_Json)
                ?? new List<ClientQuotationRequestItemDto>();
            if (!TryCalculateItems(items, out var itemResponses, out _, out var error))
            {
                throw new InvalidOperationException(error ?? "A saved quotation request contains invalid items.");
            }

            return new ClientQuotationRequestResponseDto
            {
                RequestId = request.Request_ID,
                CompanyName = request.Client_Name,
                Telephone = request.Client_Telephone,
                ContactPerson = request.Contact_Person ?? string.Empty,
                BusinessStyle = request.Business_Style ?? string.Empty,
                EmailAddress = request.Client_EmailAddress ?? string.Empty,
                Tin = request.TIN ?? string.Empty,
                CompanyAddress = request.Company_Address ?? string.Empty,
                PaymentTerms = request.Payment_Terms ?? string.Empty,
                TermsAndCondition = request.TermsAndCondition ?? string.Empty,
                ProjectTitle = request.Project_Title,
                Items = itemResponses,
                Subtotal = request.Sub_Total,
                DeliveryCharge = request.Delivery_Charge,
                InstallationCharge = request.Installation_Charge,
                Vat = request.VAT,
                OverallTotal = request.Overall_Total,
                SubmittedAtUtc = request.SubmittedAtUtc,
                Status = request.Status,
                ClaimedByUserId = request.ClaimedByUserId,
                ClaimedAtUtc = request.ClaimedAtUtc
            };
        }

        private static bool TryCalculateItems(
            IReadOnlyCollection<ClientQuotationRequestItemDto> items,
            out List<ClientQuotationRequestItemResponseDto> calculatedItems,
            out decimal subtotal,
            out string? error)
        {
            calculatedItems = new List<ClientQuotationRequestItemResponseDto>();
            subtotal = 0;
            error = null;

            if (items.Count < 1 || items.Count > MaximumItemsPerRequest)
            {
                error = $"A quotation must contain between 1 and {MaximumItemsPerRequest} items.";
                return false;
            }

            foreach (var item in items)
            {
                if (item == null ||
                    string.IsNullOrWhiteSpace(item.Branch) ||
                    string.IsNullOrWhiteSpace(item.Requirement) ||
                    item.Width <= 0 ||
                    item.Width > 100000 ||
                    item.Height <= 0 ||
                    item.Height > 100000)
                {
                    error = "Each quotation item must include a branch, requirement, and positive dimensions.";
                    return false;
                }

                var inchesPerUnit = GetInchesPerUnit(item.Measurement);
                if (inchesPerUnit == 0)
                {
                    error = "Each quotation item must use cm, inch, ft, or m.";
                    return false;
                }
                if (item.Quantity <= 0 ||
                    item.Quantity > 100000 ||
                    item.Quantity != decimal.Truncate(item.Quantity))
                {
                    error = "Each quotation item must have a positive whole-number quantity.";
                    return false;
                }

                var areaInSquareInches = item.Width * item.Height * inchesPerUnit * inchesPerUnit;
                var unitPrice = decimal.Round(
                    areaInSquareInches * PricePerSquareInch,
                    2,
                    MidpointRounding.AwayFromZero);
                var totalAmount = decimal.Round(
                    unitPrice * item.Quantity,
                    2,
                    MidpointRounding.AwayFromZero);
                if (unitPrice > MaximumDatabaseCurrency || totalAmount > MaximumDatabaseCurrency)
                {
                    error = "A quotation item total is too large to save.";
                    return false;
                }

                subtotal += totalAmount;
                calculatedItems.Add(new ClientQuotationRequestItemResponseDto
                {
                    Branch = item.Branch.Trim(),
                    Requirement = item.Requirement.Trim(),
                    Description = item.Description?.Trim() ?? string.Empty,
                    Width = item.Width,
                    Height = item.Height,
                    Measurement = item.Measurement.Trim().ToLowerInvariant(),
                    Quantity = item.Quantity,
                    UnitPrice = unitPrice,
                    TotalAmount = totalAmount
                });
            }

            if (subtotal > MaximumDatabaseCurrency)
            {
                error = "The quotation subtotal is too large to save.";
                return false;
            }

            return true;
        }

        private static decimal GetInchesPerUnit(string measurement) =>
            measurement.Trim().ToLowerInvariant() switch
            {
                "cm" => 1m / 2.54m,
                "inch" => 1m,
                "ft" => 12m,
                "m" => 100m / 2.54m,
                _ => 0
            };

        private static string NormalizeTin(string? tin) =>
            new((tin ?? string.Empty).Where(char.IsDigit).ToArray());
    }
}
