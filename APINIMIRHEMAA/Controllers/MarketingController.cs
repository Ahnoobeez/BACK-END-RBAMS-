using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APINIMIRHEMAA.Controllers
{
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

        // ------------------------------ CLIENTS PROJECT ------------------------------

        [HttpGet("GetLocalClientsProject")]
        public List<ClientsProject> GetAllLocalClientsProject()
        {
            var projects = _Localdb.ClientsProject.ToList();

            foreach (var project in projects)
            {
                project.Attention = _encryptionService.Decrypt(project.Attention);
                project.Business_Style = _encryptionService.Decrypt(project.Business_Style);
                project.Client_Subject = _encryptionService.Decrypt(project.Client_Subject);
                project.Representative = _encryptionService.Decrypt(project.Representative);
                project.Contact_Person = _encryptionService.Decrypt(project.Contact_Person);
                project.Account_Executive = _encryptionService.Decrypt(project.Account_Executive);
            }

            return projects;
        }

        [HttpGet("GetLocalClientsProjectById")]
        public ActionResult<ClientsProject> GetLocalClientProjectDetails(Int32 Id)
        {
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var ClientProjectDetails = _Localdb.ClientsProject.FirstOrDefault(x => x.Project_ID == Id);
            if (ClientProjectDetails == null)
            {
                return NotFound("Client not found.");
            }
            return ClientProjectDetails;
        }

        [HttpPost("AddLocalClientsProject")] 
        public ActionResult<ClientsProject> AddLocalClientsProject([FromBody] ClientsProject ClientProjectDetails)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            ClientProjectDetails.Attention = _encryptionService.Encrypt(ClientProjectDetails.Attention);
            ClientProjectDetails.Business_Style = _encryptionService.Encrypt(ClientProjectDetails.Business_Style);
            ClientProjectDetails.Client_Subject = _encryptionService.Encrypt(ClientProjectDetails.Client_Subject);
            ClientProjectDetails.Representative = _encryptionService.Encrypt(ClientProjectDetails.Representative);
            ClientProjectDetails.Contact_Person = _encryptionService.Encrypt(ClientProjectDetails.Contact_Person);
            ClientProjectDetails.Account_Executive = _encryptionService.Encrypt(ClientProjectDetails.Account_Executive);
            _Localdb.ClientsProject.Add(ClientProjectDetails);
            _Localdb.SaveChanges();
            return Ok(ClientProjectDetails);
        }

        [HttpPost("UpdateLocalClientsProjectDetails")]
        public ActionResult<ClientsProject> UpdateLocalClientsProject(Int32 Id, [FromBody] ClientsProject UpdateClientsProject)
        {
            if (UpdateClientsProject == null)
            {
                return BadRequest(UpdateClientsProject);
            }

            var updateClientsProject = _Localdb.ClientsProject.FirstOrDefault(x => x.Project_ID == Id);
            if (updateClientsProject == null)
            {
                return NotFound();
            }

            updateClientsProject.Project_ID = UpdateClientsProject.Project_ID;
            updateClientsProject.Client_ID = UpdateClientsProject.Client_ID;
            updateClientsProject.Attention = UpdateClientsProject.Attention;
            updateClientsProject.Business_Style = UpdateClientsProject.Business_Style;
            updateClientsProject.Client_Subject = UpdateClientsProject.Client_Subject;
            updateClientsProject.Representative = UpdateClientsProject.Representative;
            updateClientsProject.Contact_Person = UpdateClientsProject.Contact_Person;
            updateClientsProject.Account_Executive = UpdateClientsProject.Account_Executive;
            updateClientsProject.Date = UpdateClientsProject.Date;
            updateClientsProject.Time = UpdateClientsProject.Time;
            updateClientsProject.Status = UpdateClientsProject.Status;


            _Localdb.SaveChanges();


            return Ok(UpdateClientsProject);
        }

        [HttpPut("DeleteLocalClientsProject")]
        public ActionResult<ClientsProject> DeleteLocalClientProject(Int32 Id)
        {

            var clientProjectDetails = _Localdb.ClientsProject.FirstOrDefault(x => x.Project_ID == Id);
            if (clientProjectDetails == null)
            {
                return NotFound();
            }
            _Localdb.Remove(clientProjectDetails);
            _Localdb.SaveChanges();


            return NoContent();
        }


        [HttpGet("GetCloudClientsProject")]
        public List<ClientsProject> GetAllCloudClientsProject()
        {
            return _CloudDb.ClientsProject.ToList();
        }

        [HttpGet("GetCloudClientsProjectById")]
        public ActionResult<ClientsProject> GetCloudClientProjectDetails(Int32 Id)
        {
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var ClientProjectDetails = _CloudDb.ClientsProject.FirstOrDefault(x => x.Project_ID == Id);
            if (ClientProjectDetails == null)
            {
                return NotFound("Client not found.");
            }
            return ClientProjectDetails;
        }

        [HttpPost("AddCloudClientsProject")]
        public ActionResult<ClientsProject> AddCloudClientsProject([FromBody] ClientsProject ClientProjectDetails)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.ClientsProject.Add(ClientProjectDetails);
            _CloudDb.SaveChanges();
            return Ok(ClientProjectDetails);
        }

        [HttpPost("UpdateCloudClientsProjectDetails")]
        public ActionResult<ClientsProject> UpdateCloudClientsProject(Int32 Id, [FromBody] ClientsProject UpdateClientsProject)
        {
            if (UpdateClientsProject == null)
            {
                return BadRequest(UpdateClientsProject);
            }

            var updateClientsProject = _CloudDb.ClientsProject.FirstOrDefault(x => x.Project_ID == Id);
            if (updateClientsProject == null)
            {
                return NotFound();
            }

            updateClientsProject.Project_ID = UpdateClientsProject.Project_ID;
            updateClientsProject.Client_ID = UpdateClientsProject.Client_ID;
            updateClientsProject.Attention = UpdateClientsProject.Attention;
            updateClientsProject.Business_Style = UpdateClientsProject.Business_Style;
            updateClientsProject.Client_Subject = UpdateClientsProject.Client_Subject;
            updateClientsProject.Representative = UpdateClientsProject.Representative;
            updateClientsProject.Contact_Person = UpdateClientsProject.Contact_Person;
            updateClientsProject.Account_Executive = UpdateClientsProject.Account_Executive;
            updateClientsProject.Date = UpdateClientsProject.Date;
            updateClientsProject.Time = UpdateClientsProject.Time;


            _CloudDb.SaveChanges();


            return Ok(UpdateClientsProject);
        }

        [HttpPut("DeleteCloudClientsProject")]
        public ActionResult<ClientsProject> DeleteCloudClientProject(Int32 Id)
        {

            var clientProjectDetails = _CloudDb.ClientsProject.FirstOrDefault(x => x.Project_ID == Id);
            if (clientProjectDetails == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(clientProjectDetails);
            _CloudDb.SaveChanges();


            return NoContent();
        }

        // ------------------------------- CLIENTS PROJECT -------------------------------

        // ------------------------------- QUOTATION -------------------------------

        [HttpGet("GetLocalQuotations")]
        public List<Quotation> GetAllLocalQuotations()
        {
            return _Localdb.Quotation.ToList();
        }

        [HttpGet("GetLocalQuotationsByID")]
        public ActionResult<Quotation> GetLocalQuotationDetails(Int32 Id)
        {
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var QuotationId = _Localdb.Quotation.FirstOrDefault(x => x.Project_ID == Id);
            if (QuotationId == null)
            {
                return NotFound("Client not found.");
            }
            return QuotationId;
        }

        [HttpPost("AddLocalQuotations")]
        public ActionResult<Quotation> AddLocalQuotations([FromBody] Quotation QuotationDetails)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Quotation.Add(QuotationDetails);
            _Localdb.SaveChanges();
            return Ok(QuotationDetails);
        }

        [HttpPost("UpdateLocalQuotations")]
        public ActionResult<Quotation> UpdateLocalQuotations(Int32 Id, [FromBody] Quotation UpdateQuotation)
        {
            if (UpdateQuotation == null)
            {
                return BadRequest(UpdateQuotation);
            }
            var updateQuotation = _Localdb.Quotation.FirstOrDefault(x => x.Project_ID == Id);
            if (updateQuotation == null)
            {
                return NotFound();
            }
            updateQuotation.Quotation_ID = UpdateQuotation.Quotation_ID;
            updateQuotation.Project_ID = UpdateQuotation.Project_ID;
            updateQuotation.Title = UpdateQuotation.Title;
            updateQuotation.Sub_Total = UpdateQuotation.Sub_Total;
            updateQuotation.Less_Discount = UpdateQuotation.Less_Discount;
            updateQuotation.Ingress_and_Engress = UpdateQuotation.Ingress_and_Engress;
            updateQuotation.Overall_Total = UpdateQuotation.Overall_Total;
            updateQuotation.Status = UpdateQuotation.Status;
            updateQuotation.Comment = UpdateQuotation.Comment;
            updateQuotation.Terms_Condition = UpdateQuotation.Terms_Condition;
            updateQuotation.Start_Date = UpdateQuotation.Start_Date;
            updateQuotation.Time = UpdateQuotation.Time;
            updateQuotation.End_Date = UpdateQuotation.End_Date;

            _Localdb.SaveChanges();
            return Ok(UpdateQuotation);
        }

        [HttpPut ("DeleteQuotation")]
        public ActionResult<Quotation> DeleteLocalQuotations(Int32 Id)
        {
            var quotationDetails = _Localdb.Quotation.FirstOrDefault(x => x.Project_ID == Id);
            if (quotationDetails == null)
            {
                return NotFound();
            }
            _Localdb.Remove(quotationDetails);
            _Localdb.SaveChanges();
            return NoContent();
        }

        [HttpGet("GetCloudQuotations")]
        public List<Quotation> GetAllCloudQuotations()
        {
            return _CloudDb.Quotation.ToList();
        }

        [HttpGet("GetCloudQuotationsByID")]
        public ActionResult<Quotation> GetCloudQuotationDetails(Int32 Id)
        {
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var QuotationId = _CloudDb.Quotation.FirstOrDefault(x => x.Project_ID == Id);
            if (QuotationId == null)
            {
                return NotFound("Client not found.");
            }
            return QuotationId;
        }

        [HttpPost("AddCloudQuotations")]
        public ActionResult<Quotation> AddCloudQuotations([FromBody] Quotation QuotationDetails)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Quotation.Add(QuotationDetails);
            _CloudDb.SaveChanges();
            return Ok(QuotationDetails);
        }

        [HttpPost("UpdateCloudQuotations")]
        public ActionResult<Quotation> UpdateCloudQuotations(Int32 Id, [FromBody] Quotation UpdateQuotation)
        {
            if (UpdateQuotation == null)
            {
                return BadRequest(UpdateQuotation);
            }
            var updateQuotation = _CloudDb.Quotation.FirstOrDefault(x => x.Project_ID == Id);
            if (updateQuotation == null)
            {
                return NotFound();
            }
            updateQuotation.Quotation_ID = UpdateQuotation.Quotation_ID;
            updateQuotation.Project_ID = UpdateQuotation.Project_ID;
            updateQuotation.Sub_Total = UpdateQuotation.Sub_Total;
            updateQuotation.Less_Discount = UpdateQuotation.Less_Discount;
            updateQuotation.Ingress_and_Engress = UpdateQuotation.Ingress_and_Engress;
            updateQuotation.Overall_Total = UpdateQuotation.Overall_Total;
            updateQuotation.Status = UpdateQuotation.Status;
            updateQuotation.Comment = UpdateQuotation.Comment;
            updateQuotation.Terms_Condition = UpdateQuotation.Terms_Condition;
            updateQuotation.Start_Date = UpdateQuotation.Start_Date;
            updateQuotation.Time = UpdateQuotation.Time;
            updateQuotation.End_Date = UpdateQuotation.End_Date;

            _CloudDb.SaveChanges();
            return Ok(UpdateQuotation);
        }

        [HttpPut("DeleteCloudQuotation")]
        public ActionResult<Quotation> DeleteCloudQuotations(Int32 Id)
        {
            var quotationDetails = _CloudDb.Quotation.FirstOrDefault(x => x.Project_ID == Id);
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
            return _Localdb.Quotation_Items.ToList();
        }

        [HttpGet("GetLocalQuotationItemsByID")]
        public ActionResult<Quotation_Items> GetLocalQuotationItemDetails(Int32 Id)
        {
            if (Id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var QI_Id = _Localdb.Quotation_Items.FirstOrDefault(x => x.QI_ID == Id);
            if (QI_Id == null)
            {
                return NotFound("Client not found.");
            }
            return QI_Id;
        }

        [HttpPost("AddLocalQuotationItems")]
        public ActionResult<Quotation_Items> AddLocalQuotationsItems([FromBody] Quotation_Items QuotationItemDetails)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Quotation_Items.Add(QuotationItemDetails);
            _Localdb.SaveChanges();
            return Ok(QuotationItemDetails);
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
            updateQuotationItems.Quantity = UpdateQuotationItems.Quantity;
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
            var conforme = _Localdb.Conforme.ToList();

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
            var conforme = _Localdb.Conforme.FirstOrDefault(x => x.FileID == id);
            if (conforme == null)
            {
                return NotFound();
            }
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

        [HttpPost ("UpdateLocalConforme")]
        public ActionResult<Quotation> UpdateLocalConforme(Int32 Id, [FromBody] Conforme _conforme)
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
            updateConforme.FileName = _conforme.FileName;
            updateConforme.FileExtension = _conforme.FileExtension;
            updateConforme.FIleData = _conforme.FIleData;
            updateConforme.Quotation_ID = _conforme.Quotation_ID;
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
            return _Localdb.JobOrder.ToList();
        }

        [HttpGet("GetLocalJobOrderByID")]
        public ActionResult<JobOrder> GetLocalJobOrderByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var jobOrder = _Localdb.JobOrder.FirstOrDefault(x => x.JobOrder_ID == id);
            if (jobOrder == null)
            {
                return NotFound();
            }
            return Ok(jobOrder);
        }

        [HttpPost("AddLocalJobOrder")]
        public ActionResult<JobOrder> AddLocalJobOrder(JobOrder jobOrder)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(jobOrder);
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
            updateJobOrder.JobOrder_ID = _jobOrder.JobOrder_ID;
            updateJobOrder.Conforme_FileID = _jobOrder.Conforme_FileID;
            updateJobOrder.Title = _jobOrder.Title;
            updateJobOrder.Quantity = _jobOrder.Quantity;
            updateJobOrder.Width = _jobOrder.Width;
            updateJobOrder.Length = _jobOrder.Length;
            updateJobOrder.Artist_Initial = _jobOrder.Artist_Initial;
            updateJobOrder.Production_Initial = _jobOrder.Production_Initial;
            updateJobOrder.Remarks = _jobOrder.Remarks;
            updateJobOrder.Installation_Date = _jobOrder.Installation_Date;
            updateJobOrder.Target_Delivery = _jobOrder.Target_Delivery;
            updateJobOrder.Date_Delivered = _jobOrder.Date_Delivered;
            updateJobOrder.Tiling = _jobOrder.Tiling;
            updateJobOrder.Eyelet = _jobOrder.Eyelet;
            updateJobOrder.Bleeding = _jobOrder.Bleeding;
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
            _CloudDb.Add(jobOrder);
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
            updateJobOrder.JobOrder_ID = _jobOrder.JobOrder_ID;
            updateJobOrder.Conforme_FileID = _jobOrder.Conforme_FileID;
            updateJobOrder.Title = _jobOrder.Title;
            updateJobOrder.Quantity = _jobOrder.Quantity;
            updateJobOrder.Width = _jobOrder.Width;
            updateJobOrder.Length = _jobOrder.Length;
            updateJobOrder.Artist_Initial = _jobOrder.Artist_Initial;
            updateJobOrder.Production_Initial = _jobOrder.Production_Initial;
            updateJobOrder.Remarks = _jobOrder.Remarks;
            updateJobOrder.Installation_Date = _jobOrder.Installation_Date;
            updateJobOrder.Target_Delivery = _jobOrder.Target_Delivery;
            updateJobOrder.Date_Delivered = _jobOrder.Date_Delivered;
            updateJobOrder.Tiling = _jobOrder.Tiling;
            updateJobOrder.Eyelet = _jobOrder.Eyelet;
            updateJobOrder.Bleeding = _jobOrder.Bleeding;


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

        // ------------------------------- PURCHASE ORDER -------------------------------

        [HttpGet("GetLocalPurchaseOrder")]
        public List<PurchaseOrder> GetAllPurchaseOrder()
        {
            return _Localdb.PurchaseOrder.ToList();
        }

        [HttpGet("GetLocalPurchaseOrderByID")]
        public ActionResult<PurchaseOrder> GetLocalPurchaseOrderByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var purchaseOrder = _Localdb.PurchaseOrder.FirstOrDefault(x => x.FileID == id);
            if (purchaseOrder == null)
            {
                return NotFound();
            }
            return Ok(purchaseOrder);
        }

        [HttpPost("AddLocalPurchaseOrder")]
        public ActionResult<PurchaseOrder> AddLocalPurchaseOrder(PurchaseOrder purchaseOrder)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(purchaseOrder);
            _Localdb.SaveChanges();
            return Ok(purchaseOrder);
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
            updatePurchaseOrder.Conforme_FileID = _purchaseOrder.Conforme_FileID;
            updatePurchaseOrder.FileName = _purchaseOrder.FileName;
            updatePurchaseOrder.FileExtension = _purchaseOrder.FileExtension;
            updatePurchaseOrder.FileData = _purchaseOrder.FileData;
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
            return _Localdb.Graphics.ToList();
        }

        [HttpGet("GetLocalGraphicsByID")]
        public ActionResult<Graphics> GetLocalGraphicsByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var graphics = _Localdb.Graphics.FirstOrDefault(x => x.FileID == id);
            if (graphics == null)
            {
                return NotFound();
            }
            return Ok(graphics);
        }

        [HttpPost("AddLocalGraphics")]
        public ActionResult<Graphics> AddLocalGraphics(Graphics graphics)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(graphics);
            _Localdb.SaveChanges();
            return Ok(graphics);
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
            updateGraphics.Quotation_ID = _graphics.FileID;
            updateGraphics.FileName = _graphics.FileName;
            updateGraphics.FileExtension = _graphics.FileExtension;
            updateGraphics.FileData = _graphics.FileData;
            updateGraphics.Quotation_ID = _graphics.Quotation_ID;

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
        public ActionResult<Graphics> GetLocalServiceInvoiceID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var graphics = _Localdb.Graphics.FirstOrDefault(x => x.FileID == id);
            if (graphics == null)
            {
                return NotFound();
            }
            return Ok(graphics);
        }

        [HttpPost("AddLocalServiceInvoice")]
        public ActionResult<Graphics> AddLocalServiceInvoice(Graphics graphics)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(graphics);
            _Localdb.SaveChanges();
            return Ok(graphics);
        }

        [HttpPost("UpdateLocalServiceInvoice")]
        public ActionResult<Graphics> UpdateLocalServiceInvoice(Int32 Id, [FromBody] ServiceInvoice serviceInvoice)
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
            updateServiceInvoice.Quantity = serviceInvoice.Quantity;
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
        public ActionResult<Graphics> GetCloudServiceInvoiceID(int id)
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

        [HttpPost("AddCloudServiceInvoice")]
        public ActionResult<Graphics> AddCloudServiceInvoice(Graphics graphics)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(graphics);
            _CloudDb.SaveChanges();
            return Ok(graphics);
        }

        [HttpPost("UpdateCloudServiceInvoice")]
        public ActionResult<Graphics> UpdateCloudServiceInvoice(Int32 Id, [FromBody] ServiceInvoice serviceInvoice)
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
            updateServiceInvoice.Quantity = serviceInvoice.Quantity;
            updateServiceInvoice.Unit = serviceInvoice.Unit;
            updateServiceInvoice.Description = serviceInvoice.Description;
            updateServiceInvoice.Unit_Price = serviceInvoice.Unit_Price;
            updateServiceInvoice.Amount = serviceInvoice.Amount;
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

        [HttpPost("AddLocalInstallation_Schedule")]
        public ActionResult<Installation_Schedule> AddLocalInstallationSchedule(Installation_Schedule installationSchedule)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(installationSchedule);
            _Localdb.SaveChanges();
            return Ok(installationSchedule);
        }

        [HttpPost("UpdateLocalInstallation_Schedule")]
        public ActionResult<Installation_Schedule> UpdateLocalInstallationSchedule(Int32 Id, [FromBody] Installation_Schedule installationSchedule)
        {
            if (installationSchedule == null)
            {
                return BadRequest(installationSchedule);
            }
            var updateInstallationSchedule = _Localdb.Installation_Schedule.FirstOrDefault(x => x.Installation_ID == Id);
            if (updateInstallationSchedule == null)
            {
                return NotFound();
            }
            updateInstallationSchedule.Installation_ID = installationSchedule.Installation_ID;
            updateInstallationSchedule.Time = installationSchedule.Time;
            updateInstallationSchedule.Remarks = installationSchedule.Remarks;
            updateInstallationSchedule.Mall_Hours = installationSchedule.Mall_Hours;
            updateInstallationSchedule.Date = installationSchedule.Date;
            _Localdb.SaveChanges();
            return Ok(updateInstallationSchedule);
        }

        [HttpPut ("DeleteLocalInstallationSchedule")]
        public ActionResult DeleteLocalInstallationSchedule(int id)
        {
            var installationSchedule = _Localdb.Installation_Schedule.FirstOrDefault(x => x.Installation_ID == id);
            if (installationSchedule == null)
            {
                return NotFound();
            }
            _Localdb.Remove(installationSchedule);
            _Localdb.SaveChanges();
            return NoContent();
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

        [HttpPost("AddCloudInstallation_Schedule")]
        public ActionResult<Installation_Schedule> AddCloudInstallationSchedule(Installation_Schedule installationSchedule)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(installationSchedule);
            _CloudDb.SaveChanges();
            return Ok(installationSchedule);
        }

        [HttpPost("UpdateCloudInstallation_Schedule")]
        public ActionResult<Installation_Schedule> UpdateCloudInstallationSchedule(Int32 Id, [FromBody] Installation_Schedule installationSchedule)
        {
            if (installationSchedule == null)
            {
                return BadRequest(installationSchedule);
            }
            var updateInstallationSchedule = _CloudDb.Installation_Schedule.FirstOrDefault(x => x.Installation_ID == Id);
            if (updateInstallationSchedule == null)
            {
                return NotFound();
            }
            updateInstallationSchedule.Installation_ID = installationSchedule.Installation_ID;
            updateInstallationSchedule.Time = installationSchedule.Time;
            updateInstallationSchedule.Remarks = installationSchedule.Remarks;
            updateInstallationSchedule.Mall_Hours = installationSchedule.Mall_Hours;
            updateInstallationSchedule.Date = installationSchedule.Date;
            _CloudDb.SaveChanges();
            return Ok(updateInstallationSchedule);
        }

        [HttpPut("DeleteCloudInstallationSchedule")]
        public ActionResult DeleteCloudInstallationSchedule(int id)
        {
            var installationSchedule = _CloudDb.Installation_Schedule.FirstOrDefault(x => x.Installation_ID == id);
            if (installationSchedule == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(installationSchedule);
            _CloudDb.SaveChanges();
            return NoContent();
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
            updateDeliveryReceipt.Delivery_ID = deliveryReceipt.Delivery_ID;
            updateDeliveryReceipt.Title = deliveryReceipt.Title;
            updateDeliveryReceipt.Quantity = deliveryReceipt.Quantity;
            updateDeliveryReceipt.Description = deliveryReceipt.Description;
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
            updateDeliveryReceipt.Delivery_ID = deliveryReceipt.Delivery_ID;
            updateDeliveryReceipt.Title = deliveryReceipt.Title;
            updateDeliveryReceipt.Quantity = deliveryReceipt.Quantity;
            updateDeliveryReceipt.Description = deliveryReceipt.Description;
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

        // ------------------------------- DELIVERY FILES ---------------------------

        [HttpGet("GetLocalDeliveryFiles")]
        public List<DeliveryFiles> GetAllDeliveryFiles()
        {
            return _Localdb.DeliveryFiles.ToList();
        }

        [HttpGet("GetLocalDeliveryFilesByID")]
        public ActionResult<DeliveryFiles> GetLocalDeliveryFilesByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var deliveryFiles = _Localdb.DeliveryFiles.FirstOrDefault(x => x.File_ID == id);
            if (deliveryFiles == null)
            {
                return NotFound();
            }
            return Ok(deliveryFiles);
        }

        [HttpPost("AddLocalDeliveryFiles")]
        public ActionResult<DeliveryFiles> AddLocalDeliveryFiles(DeliveryFiles deliveryFiles)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(deliveryFiles);
            _Localdb.SaveChanges();
            return Ok(deliveryFiles);
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
            updateDeliveryFiles.File_ID = deliveryFiles.File_ID;
            updateDeliveryFiles.Delivery_ID = deliveryFiles.Delivery_ID;
            updateDeliveryFiles.File_Role = deliveryFiles.File_Role;
            updateDeliveryFiles.File_Name = deliveryFiles.File_Name;
            updateDeliveryFiles.File_Extension = deliveryFiles.File_Extension;
            updateDeliveryFiles.File_Data = deliveryFiles.File_Data;
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
            updateCollectionReceipt.CollectionReceipt_ID = collectionReceipt.CollectionReceipt_ID;
            updateCollectionReceipt.FileName = collectionReceipt.FileName;
            updateCollectionReceipt.FileExtension = collectionReceipt.FileExtension;
            updateCollectionReceipt.FileData = collectionReceipt.FileData;
            updateCollectionReceipt.JobOrderID = collectionReceipt.JobOrderID;
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

    }
}
