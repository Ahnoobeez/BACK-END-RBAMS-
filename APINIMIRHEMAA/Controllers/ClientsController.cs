using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize(Policy = "clients")]
    [Route("api/[controller]")]
    [ApiController]
    
    // Coordinates the clients controller API endpoints, including request handling and response generation.
    public class ClientsController : ControllerBase
    {
        // Injected or cached localdb dependency/state used by this class.
        private LocalDbContext _Localdb;
        // Injected or cached clouddb dependency/state used by this class.
        private CloudDbContext _Clouddb;
        // Encryption service used to protect sensitive persisted values.
        private readonly EncryptionService _encryptionService;
        // Initializes a new clients controller instance.
        public ClientsController(LocalDbContext Localcontext, CloudDbContext Cloudcontext, EncryptionService encryptionService)
        {
            _Localdb = Localcontext;
            _Clouddb = Cloudcontext;
            _encryptionService = encryptionService;
        }

        // Projects client telephone values as text for consistent database querying.
        internal static IQueryable<ClientEntity> ClientsWithTelephoneAsText(DbSet<ClientEntity> clients)
        {
            return clients.FromSqlRaw(
                "SELECT [Client_ID], [CreatedBy], [CreatedByUserId], [CreatedByDepartmentId], COALESCE([Client_Name], N'') AS [Client_Name], COALESCE(CONVERT(nvarchar(max), [Client_Telephone]), N'') AS [Client_Telephone], [Contact_Person], [Business_Style], [Client_EmailAddress], COALESCE([TermsAndCondition], N'') AS [TermsAndCondition], [Company_Address], COALESCE([TIN], N'') AS [TIN], [Payment_Terms], COALESCE(CONVERT(nvarchar(max), [Price_Per_Square_Inch]), N'0') AS [Price_Per_Square_Inch] FROM [dbo].[Clients]");
        }

        // Decrypts protected client fields before returning the client in an API response.
        private ClientEntity PrepareClientForResponse(ClientEntity client)
        {
            // Enter this branch when `!string.IsNullOrWhiteSpace(client.Business_Style) && client.Business_Style.StartsWith(, StringComparison.Ordinal)` is true.
            if (!string.IsNullOrWhiteSpace(client.Business_Style) &&
                client.Business_Style.StartsWith("CfDJ8", StringComparison.Ordinal))
            {
                client.Business_Style = _encryptionService.TryDecrypt(client.Business_Style);
            }

            return client;
        }

        // Checks whether the supplied client name is already in use.
        private static bool ClientNameExists(DbSet<ClientEntity> clients, string? clientName, int? excludingClientId = null)
        {
            var normalizedName = clientName?.Trim();
            // Exit the current operation when `string.IsNullOrWhiteSpace(normalizedName)` is true.
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                return false;
            }

            return clients
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(client => !excludingClientId.HasValue || client.Client_ID != excludingClientId.Value)
                .Select(client => client.Client_Name)
                .ToList()
                .Any(existingName =>
                    string.Equals(existingName?.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase));
        }

        // Checks whether the supplied tax identification number is already in use.
        private static bool ClientTinExists(DbSet<ClientEntity> clients, string? tin, int? excludingClientId = null)
        {
            var normalizedTin = NormalizeTin(tin);
            // Exit the current operation when `string.IsNullOrEmpty(normalizedTin)` is true.
            if (string.IsNullOrEmpty(normalizedTin))
            {
                return false;
            }

            return clients
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(client => !excludingClientId.HasValue || client.Client_ID != excludingClientId.Value)
                .Select(client => client.TIN)
                .ToList()
                .Any(existingTin => NormalizeTin(existingTin) == normalizedTin);
        }

        // Normalizes the tin for consistent use.
        private static string NormalizeTin(string? tin)
        {
            return string.IsNullOrWhiteSpace(tin)
                ? string.Empty
                // Apply this predicate in the database query before materializing results.
                : new string(tin.Where(char.IsDigit).ToArray());
        }

        /// <summary>Retrieves all clients from the local database.</summary>
        [HttpGet("GetLocalClients")] 
        public List<ClientEntity> GetAllLocalClients()
        {
            return ClientsWithTelephoneAsText(_Localdb.Clients)
                .ToList()
                .Select(PrepareClientForResponse)
                .ToList();
        }

        /// <summary>Retrieves the local client details.</summary>
        [HttpGet("GetLocalClientsById")]
        public ActionResult<ClientEntity> GetLocalClientDetails(Int32 Id)
        {
            // Return HTTP 400 Bad Request when `Id == 0` is true.
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var ClientDetails = ClientsWithTelephoneAsText(_Localdb.Clients).FirstOrDefault(x => x.Client_ID == Id);
            // Return HTTP 404 Not Found when `ClientDetails == null` is true.
            if (ClientDetails == null)
            {
                return NotFound("Client not found.");
            }
            return PrepareClientForResponse(ClientDetails);
        }

        /// <summary>Adds the local client.</summary>
        [HttpPost ("AddLocalClients")] 
        public ActionResult<ClientEntity> AddLocalClient([FromBody] ClientEntity ClientDetails)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            using var transaction = _Localdb.Database.BeginTransaction(IsolationLevel.Serializable);
            // Return HTTP 409 Conflict when `ClientNameExists(_Localdb.Clients, ClientDetails.Client_Name)` is true.
            if (ClientNameExists(_Localdb.Clients, ClientDetails.Client_Name))
            {
                return Conflict("A client with this company name already exists.");
            }
            // Return HTTP 409 Conflict when `ClientTinExists(_Localdb.Clients, ClientDetails.TIN)` is true.
            if (ClientTinExists(_Localdb.Clients, ClientDetails.TIN))
            {
                return Conflict("A client with this TIN number already exists.");
            }

            var businessStyle = ClientDetails.Business_Style;
            ClientDetails.Business_Style = _encryptionService.Encrypt(businessStyle);
            _Localdb.Clients.Add(ClientDetails);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            transaction.Commit();
            _Localdb.Entry(ClientDetails).State = EntityState.Detached;
            ClientDetails.Business_Style = businessStyle;
            return Ok(ClientDetails);
        }

        /// <summary>Updates the local client.</summary>
        [HttpPost("UpdateLocalClientDetails")] 
        public ActionResult<ClientEntity> UpdateLocalClient(Int32 Id, [FromBody] ClientEntity ClientDetails)
        {
            // Return HTTP 400 Bad Request when `ClientDetails == null` is true.
            if (ClientDetails == null)
            {
                return BadRequest(ClientDetails);
            }

            var clientDetails = ClientsWithTelephoneAsText(_Localdb.Clients).FirstOrDefault(x => x.Client_ID == Id);
            // Return HTTP 404 Not Found when `clientDetails == null` is true.
            if (clientDetails == null)
            {
                return NotFound();
            }
            using var transaction = _Localdb.Database.BeginTransaction(IsolationLevel.Serializable);
            // Return HTTP 409 Conflict when `ClientNameExists(_Localdb.Clients, ClientDetails.Client_Name, Id)` is true.
            if (ClientNameExists(_Localdb.Clients, ClientDetails.Client_Name, Id))
            {
                return Conflict("A client with this company name already exists.");
            }
            // Return HTTP 409 Conflict when `ClientTinExists(_Localdb.Clients, ClientDetails.TIN, Id)` is true.
            if (ClientTinExists(_Localdb.Clients, ClientDetails.TIN, Id))
            {
                return Conflict("A client with this TIN number already exists.");
            }

            clientDetails.Client_Name = ClientDetails.Client_Name;
            clientDetails.Client_Telephone = ClientDetails.Client_Telephone;
            clientDetails.Contact_Person = ClientDetails.Contact_Person;
            clientDetails.Business_Style = _encryptionService.Encrypt(ClientDetails.Business_Style);
            clientDetails.Client_EmailAddress = ClientDetails.Client_EmailAddress;
            clientDetails.TermsAndCondition = ClientDetails.TermsAndCondition;
            clientDetails.Company_Address = ClientDetails.Company_Address;
            clientDetails.TIN = ClientDetails.TIN;
            clientDetails.Payment_Terms = ClientDetails.Payment_Terms;
            clientDetails.Price_Per_Square_Inch = ClientDetails.Price_Per_Square_Inch;

            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            transaction.Commit();


            return Ok(ClientDetails);
        }

        /// <summary>Deletes the local client.</summary>
        [HttpPut("DeleteLocalClients")] 
        public ActionResult<ClientEntity> DeleteLocalClient(Int32 Id)
        {

            var clientDetails = ClientsWithTelephoneAsText(_Localdb.Clients).FirstOrDefault(x => x.Client_ID == Id);
            // Return HTTP 404 Not Found when `clientDetails == null` is true.
            if (clientDetails == null)
            {
                return NotFound();
            }
            _Localdb.Remove(clientDetails);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();


            return NoContent();
        }

        /// <summary>Retrieves all clients from the cloud database.</summary>
        [HttpGet("GetCloudClients")]
        public List<ClientEntity> GetAllCloudClients()
        {
            return ClientsWithTelephoneAsText(_Clouddb.Clients)
                .ToList()
                .Select(PrepareClientForResponse)
                .ToList();
        }

        /// <summary>Retrieves the cloud client details.</summary>
        [HttpGet("GetCloudClientsById")]
        public ActionResult<ClientEntity> GetCloudClientDetails(Int32 Id)
        {
            // Return HTTP 400 Bad Request when `Id == 0` is true.
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var ClientDetails = ClientsWithTelephoneAsText(_Clouddb.Clients).FirstOrDefault(x => x.Client_ID == Id);
            // Return HTTP 404 Not Found when `ClientDetails == null` is true.
            if (ClientDetails == null)
            {
                return NotFound("Client not found.");
            }
            return PrepareClientForResponse(ClientDetails);
        }

        /// <summary>Adds the cloud client.</summary>
        [HttpPost("AddCloudClients")]
        public ActionResult<ClientEntity> AddCloudClient([FromBody] ClientEntity ClientDetails)
        {
            // Return HTTP 400 Bad Request when `!ModelState.IsValid` is true.
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            using var transaction = _Clouddb.Database.BeginTransaction(IsolationLevel.Serializable);
            // Return HTTP 409 Conflict when `ClientNameExists(_Clouddb.Clients, ClientDetails.Client_Name)` is true.
            if (ClientNameExists(_Clouddb.Clients, ClientDetails.Client_Name))
            {
                return Conflict("A client with this company name already exists.");
            }
            // Return HTTP 409 Conflict when `ClientTinExists(_Clouddb.Clients, ClientDetails.TIN)` is true.
            if (ClientTinExists(_Clouddb.Clients, ClientDetails.TIN))
            {
                return Conflict("A client with this TIN number already exists.");
            }

            var businessStyle = ClientDetails.Business_Style;
            ClientDetails.Business_Style = _encryptionService.Encrypt(businessStyle);
            _Clouddb.Clients.Add(ClientDetails);
            // Persist the pending entity changes to the database at this point.
            _Clouddb.SaveChanges();
            transaction.Commit();
            _Clouddb.Entry(ClientDetails).State = EntityState.Detached;
            ClientDetails.Business_Style = businessStyle;
            return Ok(ClientDetails);
        }

        /// <summary>Updates the cloud client.</summary>
        [HttpPost("UpdateCloudClientDetails")]
        public ActionResult<ClientEntity> UpdateCloudClient(Int32 Id, [FromBody] ClientEntity ClientDetails)
        {
            // Return HTTP 400 Bad Request when `ClientDetails == null` is true.
            if (ClientDetails == null)
            {
                return BadRequest(ClientDetails);
            }

            var clientDetails = ClientsWithTelephoneAsText(_Clouddb.Clients).FirstOrDefault(x => x.Client_ID == Id);
            // Return HTTP 404 Not Found when `clientDetails == null` is true.
            if (clientDetails == null)
            {
                return NotFound();
            }
            using var transaction = _Clouddb.Database.BeginTransaction(IsolationLevel.Serializable);
            // Return HTTP 409 Conflict when `ClientNameExists(_Clouddb.Clients, ClientDetails.Client_Name, Id)` is true.
            if (ClientNameExists(_Clouddb.Clients, ClientDetails.Client_Name, Id))
            {
                return Conflict("A client with this company name already exists.");
            }
            // Return HTTP 409 Conflict when `ClientTinExists(_Clouddb.Clients, ClientDetails.TIN, Id)` is true.
            if (ClientTinExists(_Clouddb.Clients, ClientDetails.TIN, Id))
            {
                return Conflict("A client with this TIN number already exists.");
            }

            clientDetails.Client_Name = ClientDetails.Client_Name;
            clientDetails.Client_Telephone = ClientDetails.Client_Telephone;
            clientDetails.Contact_Person = ClientDetails.Contact_Person;
            clientDetails.Business_Style = _encryptionService.Encrypt(ClientDetails.Business_Style);
            clientDetails.Client_EmailAddress = ClientDetails.Client_EmailAddress;
            clientDetails.TermsAndCondition = ClientDetails.TermsAndCondition;
            clientDetails.Company_Address = ClientDetails.Company_Address;
            clientDetails.TIN = ClientDetails.TIN;
            clientDetails.Payment_Terms = ClientDetails.Payment_Terms;
            clientDetails.Price_Per_Square_Inch = ClientDetails.Price_Per_Square_Inch;

            // Persist the pending entity changes to the database at this point.
            _Clouddb.SaveChanges();
            transaction.Commit();


            return Ok(ClientDetails);
        }

        /// <summary>Deletes the cloud client.</summary>
        [HttpPut("DeleteCloudClients")]
        public ActionResult<ClientEntity> DeleteCloudClient(Int32 Id)
        {

            var clientDetails = ClientsWithTelephoneAsText(_Clouddb.Clients).FirstOrDefault(x => x.Client_ID == Id);
            // Return HTTP 404 Not Found when `clientDetails == null` is true.
            if (clientDetails == null)
            {
                return NotFound();
            }
            _Clouddb.Remove(clientDetails);
            // Persist the pending entity changes to the database at this point.
            _Clouddb.SaveChanges();


            return NoContent();
        }

            
    }
}
