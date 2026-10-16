using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize(Policy = "clients")]
    [Route("api/[controller]")]
    [ApiController]
    
    public class ClientsController : ControllerBase
    {
        private LocalDbContext _Localdb;
        private CloudDbContext _Clouddb;
        private readonly EncryptionService _encryptionService;
        public ClientsController(LocalDbContext Localcontext, CloudDbContext Cloudcontext, EncryptionService encryptionService)
        {
            _Localdb = Localcontext;
            _Clouddb = Cloudcontext;
            _encryptionService = encryptionService;
        }

        private static IQueryable<ClientEntity> ClientsWithTelephoneAsText(DbSet<ClientEntity> clients)
        {
            return clients.FromSqlRaw(
                "SELECT [Client_ID], [CreatedBy], COALESCE([Client_Name], N'') AS [Client_Name], COALESCE(CONVERT(nvarchar(16), [Client_Telephone]), N'') AS [Client_Telephone], [Contact_Person], [Business_Style], [Client_EmailAddress], COALESCE([TermsAndCondition], N'') AS [TermsAndCondition], [Company_Address], COALESCE([TIN], N'') AS [TIN], [Payment_Terms], COALESCE([Price_Per_Square_Inch], CONVERT(decimal(18,4), 0)) AS [Price_Per_Square_Inch] FROM [dbo].[Clients]");
        }

        private ClientEntity PrepareClientForResponse(ClientEntity client)
        {
            if (!string.IsNullOrWhiteSpace(client.Business_Style) &&
                client.Business_Style.StartsWith("CfDJ8", StringComparison.Ordinal))
            {
                client.Business_Style = _encryptionService.TryDecrypt(client.Business_Style);
            }

            return client;
        }

        [HttpGet("GetLocalClients")] 
        public List<ClientEntity> GetAllLocalClients()
        {
            return ClientsWithTelephoneAsText(_Localdb.Clients)
                .ToList()
                .Select(PrepareClientForResponse)
                .ToList();
        }

        [HttpGet("GetLocalClientsById")]
        public ActionResult<ClientEntity> GetLocalClientDetails(Int32 Id)
        {
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var ClientDetails = ClientsWithTelephoneAsText(_Localdb.Clients).FirstOrDefault(x => x.Client_ID == Id);
            if (ClientDetails == null)
            {
                return NotFound("Client not found.");
            }
            return PrepareClientForResponse(ClientDetails);
        }

        [HttpPost ("AddLocalClients")] 
        public ActionResult<ClientEntity> AddLocalClient([FromBody] ClientEntity ClientDetails)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var businessStyle = ClientDetails.Business_Style;
            ClientDetails.Business_Style = _encryptionService.Encrypt(businessStyle);
            _Localdb.Clients.Add(ClientDetails);
            _Localdb.SaveChanges();
            _Localdb.Entry(ClientDetails).State = EntityState.Detached;
            ClientDetails.Business_Style = businessStyle;
            return Ok(ClientDetails);
        }

        [HttpPost("UpdateLocalClientDetails")] 
        public ActionResult<ClientEntity> UpdateLocalClient(Int32 Id, [FromBody] ClientEntity ClientDetails)
        {
            if (ClientDetails == null)
            {
                return BadRequest(ClientDetails);
            }

            var clientDetails = ClientsWithTelephoneAsText(_Localdb.Clients).FirstOrDefault(x => x.Client_ID == Id);
            if (clientDetails == null)
            {
                return NotFound();
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

            _Localdb.SaveChanges();


            return Ok(ClientDetails);
        }

        [HttpPut("DeleteLocalClients")] 
        public ActionResult<ClientEntity> DeleteLocalClient(Int32 Id)
        {

            var clientDetails = ClientsWithTelephoneAsText(_Localdb.Clients).FirstOrDefault(x => x.Client_ID == Id);
            if (clientDetails == null)
            {
                return NotFound();
            }
            _Localdb.Remove(clientDetails);
            _Localdb.SaveChanges();


            return NoContent();
        }

        [HttpGet("GetCloudClients")]
        public List<ClientEntity> GetAllCloudClients()
        {
            return ClientsWithTelephoneAsText(_Clouddb.Clients)
                .ToList()
                .Select(PrepareClientForResponse)
                .ToList();
        }

        [HttpGet("GetCloudClientsById")]
        public ActionResult<ClientEntity> GetCloudClientDetails(Int32 Id)
        {
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var ClientDetails = ClientsWithTelephoneAsText(_Clouddb.Clients).FirstOrDefault(x => x.Client_ID == Id);
            if (ClientDetails == null)
            {
                return NotFound("Client not found.");
            }
            return PrepareClientForResponse(ClientDetails);
        }

        [HttpPost("AddCloudClients")]
        public ActionResult<ClientEntity> AddCloudClient([FromBody] ClientEntity ClientDetails)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var businessStyle = ClientDetails.Business_Style;
            ClientDetails.Business_Style = _encryptionService.Encrypt(businessStyle);
            _Clouddb.Clients.Add(ClientDetails);
            _Clouddb.SaveChanges();
            _Clouddb.Entry(ClientDetails).State = EntityState.Detached;
            ClientDetails.Business_Style = businessStyle;
            return Ok(ClientDetails);
        }

        [HttpPost("UpdateCloudClientDetails")]
        public ActionResult<ClientEntity> UpdateCloudClient(Int32 Id, [FromBody] ClientEntity ClientDetails)
        {
            if (ClientDetails == null)
            {
                return BadRequest(ClientDetails);
            }

            var clientDetails = ClientsWithTelephoneAsText(_Clouddb.Clients).FirstOrDefault(x => x.Client_ID == Id);
            if (clientDetails == null)
            {
                return NotFound();
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

            _Clouddb.SaveChanges();


            return Ok(ClientDetails);
        }

        [HttpPut("DeleteCloudClients")]
        public ActionResult<ClientEntity> DeleteCloudClient(Int32 Id)
        {

            var clientDetails = ClientsWithTelephoneAsText(_Clouddb.Clients).FirstOrDefault(x => x.Client_ID == Id);
            if (clientDetails == null)
            {
                return NotFound();
            }
            _Clouddb.Remove(clientDetails);
            _Clouddb.SaveChanges();


            return NoContent();
        }

            
    }
}
