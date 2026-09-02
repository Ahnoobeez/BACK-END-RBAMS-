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
        private ApplicationDbContext _db;

        public ClientsController(ApplicationDbContext context)
        {
            _db = context;
        }

        [HttpGet] //Kinukuha lahat ng nasa Database
        public List<ClientEntity> GetAllClients()
        {
            return _db.Clients.ToList();
        }

        [HttpGet("GetClientsById")] // Kinukuha ang client details base sa ID
        public ActionResult<ClientEntity> GetClientDetails(Int32 Id)
        {
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var ClientDetails = _db.Clients.FirstOrDefault(x => x.Client_ID == Id);
            if (ClientDetails == null)
            {
                return NotFound("Client not found.");
            }
            return ClientDetails;
        }

        [HttpPost] // Nagdadagdag ng bagong client sa database
        public ActionResult<ClientEntity> AddClient([FromBody] ClientEntity ClientDetails)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _db.Clients.Add(ClientDetails);
            _db.SaveChanges();
            return Ok(ClientDetails);
        }

        [HttpPost("UpdateClientDetails")] //Nagbabago ng client details sa database base sa ID
        public ActionResult<ClientEntity> UpdateClient(Int32 Id, [FromBody] ClientEntity ClientDetails)
        {
            if (ClientDetails == null)
            {
                return BadRequest(ClientDetails);
            }

            var clientDetails = _db.Clients.FirstOrDefault(x => x.Client_ID == Id);
            if (clientDetails == null)
            {
                return NotFound();
            }

            clientDetails.Client_Name = ClientDetails.Client_Name;
            clientDetails.Client_Telephone = ClientDetails.Client_Telephone;
            clientDetails.Client_Address = ClientDetails.Client_Address;
            clientDetails.TIN = ClientDetails.TIN;
            clientDetails.Payment_Terms = ClientDetails.Payment_Terms;

            _db.SaveChanges();


            return Ok(ClientDetails);
        }


        [HttpPut("DeleteClients")] // Nagdedelete ng clients sa database
        public ActionResult<ClientEntity> DeleteClient(Int32 Id)
        {

            var clientDetails = _db.Clients.FirstOrDefault(x => x.Client_ID == Id);
            if (clientDetails == null)
            {
                return NotFound();
            }
            _db.Remove(clientDetails);
            _db.SaveChanges();


            return NoContent();
        }

        
        [HttpGet("GetAuditTrail")]
        public List<AuditTrail> GetAllAuditTrail()
        {
            return _db.AuditTrail.ToList();
        }

        [HttpGet("GetMaterialRequesitionSlips")]
        public List<MaterialRequesition_Slip> GetAllMaterialRequesitionSlips()
        {
            return _db.MaterailsRequisition_Slip.ToList();
        }

        [HttpGet("GetMaterials")]
        public List<Materials> GetAllMaterials()
        {
            return _db.Materials.ToList();
        }

        [HttpGet("GetStockTransferSlips")]
        public List<StockTransfer_Slip> GetAllStockTransferSlips()
        {
            return _db.StockTransfer_Slip.ToList();
        }

        [HttpGet("GetTransmittalSlips")]
        public List<Transmittal_Slip> GetAllTransmittalSlips()
        {
            return _db.Transmittal_Slip.ToList();
        }

    }
}
