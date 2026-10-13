using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
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
        public ClientsController(LocalDbContext Localcontext, CloudDbContext Cloudcontext)
        {
            _Localdb = Localcontext;
            _Clouddb = Cloudcontext;
        }

        private static IQueryable<ClientEntity> ClientsWithTelephoneAsText(DbSet<ClientEntity> clients)
        {
            return clients.FromSqlRaw(
                "SELECT [Client_ID], COALESCE([Client_Name], N'') AS [Client_Name], COALESCE(CONVERT(nvarchar(16), [Client_Telephone]), N'') AS [Client_Telephone], [Contact_Person], [Client_EmailAddress], COALESCE([TermsAndCondition], N'') AS [TermsAndCondition], [Company_Address], COALESCE([TIN], N'') AS [TIN], [Payment_Terms] FROM [dbo].[Clients]");
        }

        [HttpGet("GetLocalClients")] 
        public List<ClientEntity> GetAllLocalClients()
        {
            return ClientsWithTelephoneAsText(_Localdb.Clients).ToList();
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
            return ClientDetails;
        }

        [HttpPost ("AddLocalClients")] 
        public ActionResult<ClientEntity> AddLocalClient([FromBody] ClientEntity ClientDetails)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Clients.Add(ClientDetails);
            _Localdb.SaveChanges();
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
            clientDetails.Client_EmailAddress = ClientDetails.Client_EmailAddress;
            clientDetails.TermsAndCondition = ClientDetails.TermsAndCondition;
            clientDetails.Company_Address = ClientDetails.Company_Address;
            clientDetails.TIN = ClientDetails.TIN;
            clientDetails.Payment_Terms = ClientDetails.Payment_Terms;

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
            return ClientsWithTelephoneAsText(_Clouddb.Clients).ToList();
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
            return ClientDetails;
        }

        [HttpPost("AddCloudClients")]
        public ActionResult<ClientEntity> AddCloudClient([FromBody] ClientEntity ClientDetails)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Clouddb.Clients.Add(ClientDetails);
            _Clouddb.SaveChanges();
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
            clientDetails.Client_EmailAddress = ClientDetails.Client_EmailAddress;
            clientDetails.TermsAndCondition = ClientDetails.TermsAndCondition;
            clientDetails.Company_Address = ClientDetails.Company_Address;
            clientDetails.TIN = ClientDetails.TIN;
            clientDetails.Payment_Terms = ClientDetails.Payment_Terms;

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
