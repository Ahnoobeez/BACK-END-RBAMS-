using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APINIMIRHEMAA.Controllers
{
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

        [HttpGet("GetLocalClients")] 
        public List<ClientEntity> GetAllClients()
        {
            return _Localdb.Clients.ToList();
        }

        [HttpGet("CLOUD CLIENTS GET")] //Kinukuha lahat ng nasa Database
        public List<ClientEntity> GetAllCloudClients()
        {
            return _Clouddb.Clients.ToList();
        }

        [HttpGet("GetClientsById")] // Kinukuha ang client details base sa ID
        public ActionResult<ClientEntity> GetClientDetails(Int32 Id)
        {
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var ClientDetails = _Localdb.Clients.FirstOrDefault(x => x.Client_ID == Id);
            if (ClientDetails == null)
            {
                return NotFound("Client not found.");
            }
            return ClientDetails;
        }

        [HttpPost ("AddLocalClients")] 
        public ActionResult<ClientEntity> AddClient([FromBody] ClientEntity ClientDetails)
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
        public ActionResult<ClientEntity> UpdateClient(Int32 Id, [FromBody] ClientEntity ClientDetails)
        {
            if (ClientDetails == null)
            {
                return BadRequest(ClientDetails);
            }

            var clientDetails = _Localdb.Clients.FirstOrDefault(x => x.Client_ID == Id);
            if (clientDetails == null)
            {
                return NotFound();
            }

            clientDetails.Client_Name = ClientDetails.Client_Name;
            clientDetails.Client_Telephone = ClientDetails.Client_Telephone;
            clientDetails.Client_Address = ClientDetails.Client_Address;
            clientDetails.TIN = ClientDetails.TIN;
            clientDetails.Payment_Terms = ClientDetails.Payment_Terms;

            _Localdb.SaveChanges();


            return Ok(ClientDetails);
        }

        [HttpPut("DeleteLocalClients")] 
        public ActionResult<ClientEntity> DeleteClient(Int32 Id)
        {

            var clientDetails = _Localdb.Clients.FirstOrDefault(x => x.Client_ID == Id);
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
            return _Localdb.AuditTrail.ToList();
        }

        [HttpGet("GetMaterialRequesitionSlips")]
        public List<MaterialRequesition_Slip> GetAllMaterialRequesitionSlips()
        {
            return _Localdb.MaterailsRequisition_Slip.ToList();
        }

        [HttpGet("GetMaterials")]
        public List<Materials> GetAllMaterials()
        {
            return _Localdb.Materials.ToList();
        }

        [HttpGet("GetStockTransferSlips")]
        public List<StockTransfer_Slip> GetAllStockTransferSlips()
        {
            return _Localdb.StockTransfer_Slip.ToList();
        }

        [HttpGet("GetTransmittalSlips")]
        public List<Transmittal_Slip> GetAllTransmittalSlips()
        {
            return _Localdb.Transmittal_Slip.ToList();
        }

    }
}
