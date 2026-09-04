using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APINIMIRHEMAA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MarketingController : ControllerBase
    {
        private LocalDbContext _db;
        
        public MarketingController(LocalDbContext context)
        {
            _db = context;
        }
        // ------------------------------ CLIENTS PROJECT ------------------------------
        [HttpGet("GetClientsProject")]
        public List<ClientsProject> GetAllClientsProject()
        {
            return _db.ClientsProject.ToList();
        }

        [HttpGet("GetClientsProjectById")]
        public ActionResult<ClientsProject> GetClientProjectDetails(Int32 Id)
        {
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var ClientProjectDetails = _db.ClientsProject.FirstOrDefault(x => x.Project_ID == Id);
            if (ClientProjectDetails == null)
            {
                return NotFound("Client not found.");
            }
            return ClientProjectDetails;
        }

        [HttpPost("InputClientsProject")] 
        public ActionResult<ClientsProject> AddClientsProject([FromBody] ClientsProject ClientProjectDetails)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _db.ClientsProject.Add(ClientProjectDetails);
            _db.SaveChanges();
            return Ok(ClientProjectDetails);
        }

        [HttpPost("UpdateClientsProjectDetails")]
        public ActionResult<ClientsProject> UpdateClientsProject(Int32 Id, [FromBody] ClientsProject UpdateClientsProject)
        {
            if (UpdateClientsProject == null)
            {
                return BadRequest(UpdateClientsProject);
            }

            var updateClientsProject = _db.ClientsProject.FirstOrDefault(x => x.Project_ID == Id);
            if (updateClientsProject == null)
            {
                return NotFound();
            }

            updateClientsProject.Project_ID = UpdateClientsProject.Project_ID;
            updateClientsProject.Client_ID = UpdateClientsProject.Client_ID;
            updateClientsProject.Attention = UpdateClientsProject.Attention;
            updateClientsProject.Business_Style = UpdateClientsProject.Business_Style;
            updateClientsProject.Client_Subject = UpdateClientsProject.Client_Subject;
            updateClientsProject.FileID = UpdateClientsProject.FileID;
            updateClientsProject.Representative = UpdateClientsProject.Representative;
            updateClientsProject.Contact_Person = UpdateClientsProject.Contact_Person;
            updateClientsProject.Account_Executive = UpdateClientsProject.Account_Executive;
            updateClientsProject.Date = UpdateClientsProject.Date;
            updateClientsProject.Time = UpdateClientsProject.Time;


            _db.SaveChanges();


            return Ok(UpdateClientsProject);
        }

        [HttpPut("DeleteClientsProject")]
        public ActionResult<ClientsProject> DeleteClientProject(Int32 Id)
        {

            var clientProjectDetails = _db.ClientsProject.FirstOrDefault(x => x.Project_ID == Id);
            if (clientProjectDetails == null)
            {
                return NotFound();
            }
            _db.Remove(clientProjectDetails);
            _db.SaveChanges();


            return NoContent();
        }

        // ------------------------------- CLIENTS PROJECT -------------------------------

        // ------------------------------- QUOTATION -------------------------------
        [HttpGet("GetQuotations")]
        public List<Quotation> GetAllQuotation()
        {
            return _db.Quotation.ToList();
        }

        // ------------------------------- QUOTATION -------------------------------

        // ------------------------------- CONFORME -------------------------------

        [HttpGet("GetConforme")]
        public List<Conforme> GetAllConforme()
        {
            return _db.Conforme.ToList();
        }

        // ------------------------------- CONFORME -------------------------------

        // ------------------------------- JOB ORDER -------------------------------

        [HttpGet("GetJobOrder")]
        public List<JobOrder> GetAllJobOrder()
        {
            return _db.JobOrder.ToList();
        }

        // ------------------------------- JOB ORDER -------------------------------

        // ------------------------------- PURCHASE ORDER -------------------------------

        [HttpGet("GetPurchaseOrders")]
        public List<PurchaseOrder> GetAllPurchaseOrders()
        {
            return _db.PurchaseOrder.ToList();
        }

        // ------------------------------- PURCHASE ORDER -------------------------------

        // ------------------------------- GRAPHICS -------------------------------

        [HttpGet("GetGraphics")]
        public List<Graphics> GetAllGraphics()
        {
            return _db.Graphics.ToList();
        }

        // ------------------------------- GRAPHICS -------------------------------

        // ------------------------------- SERVICE INVOICE ---------------------------

        [HttpGet("GetServiceInvoices")]
        public List<ServiceInvoice> GetAllServiceInvoices()
        {
            return _db.ServiceInvoice.ToList();
        }

        // ------------------------------- SERVICE INVOICE ---------------------------

        // ------------------------------- INSTALLATION SCHEDULE ---------------------------

        [HttpGet("GetInstallationSchedule")]
        public List<Installation_Schedule> GetAllInstallationSchedule()
        {
            return _db.Installation_Schedule.ToList();
        }

        // ------------------------------- INSTALLATION SCHEDULE ---------------------------

        // ------------------------------- DELIVERY RECEIPT ---------------------------

        [HttpGet("GetDeliveryReceipts")]
        public List<DeliveryReceipt> GetAllDeliveryReceipts()
        {
            return _db.DeliveryReceipt.ToList();
        }

        // ------------------------------- DELIVERY RECEIPT ---------------------------

        // ------------------------------- DELIVERY FILES ---------------------------

        [HttpGet("GetDeliveryFiles")]
        public List<DeliveryFiles> GetAllDeliveryFiles()
        {
            return _db.DeliveryFiles.ToList();
        }

        // ------------------------------- DELIVERY FILES ---------------------------

        // ------------------------------- COLLECTION RECEIPT ---------------------------

        [HttpGet("GetCollectionReceipts")]
        public List<CollectionReceipt> GetAllCollectionReceipts()
        {
            return _db.CollectionReceipt.ToList();
        }

        // ------------------------------- COLLECTION RECEIPT ---------------------------

        // ------------------------------- ISSUED BY ---------------------------

        [HttpGet("GetIssuedBy")]
        public List<IssuedBy> GetAllIssuedBy()
        {
            return _db.IssuedBy.ToList();
        }

        // ------------------------------- ISSUED BY ---------------------------

    }
}
