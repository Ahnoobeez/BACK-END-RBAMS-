using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO.InstallationSchedule;
using APINIMIRHEMAA.DTO.JobOrders;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize(Policy = "production")]
    [Route("api/[controller]")]
    [ApiController]
    public class ProductionController : ControllerBase
    {
        private LocalDbContext _Localdb;
        private CloudDbContext _CloudDb;
        private readonly EncryptionService _encryptionService;

        public ProductionController(LocalDbContext Localcontext, CloudDbContext Cloudcontext, EncryptionService encryptionService)
        {
            _Localdb = Localcontext;
            _CloudDb = Cloudcontext;
            _encryptionService = encryptionService;
        }

        // ----------------------------------- JOB ORDER -----------------------------------
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

        [HttpPost("AddLocalJobOrder")]
        public ActionResult<JobOrder> AddLocalJobOrder(JobOrder jobOrder)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            jobOrder.Comments = new List<JobOrderComment>();
            _Localdb.Add(jobOrder);
            foreach (var purchaseOrder in _Localdb.PurchaseOrder.Where(
                record => record.Conforme_FileID == jobOrder.Conforme_FileID))
            {
                purchaseOrder.Status = "Completed";
            }
            _Localdb.SaveChanges();
            return Ok(jobOrder);
        }

        [HttpPost("CompleteLocalJobOrder")]
        public async Task<ActionResult<JobOrder>> CompleteLocalJobOrder(int jobOrderId)
        {
            if (jobOrderId <= 0)
            {
                return BadRequest("A valid Job Order ID is required.");
            }

            var updatedRows = await _Localdb.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE [dbo].[JobOrder] SET [Production_Completed] = 1, [Status] = CASE WHEN [Technical_Completed] = 1 THEN N'Completed' ELSE N'In Progress' END WHERE [JobOrder_ID] = {jobOrderId}");
            if (updatedRows == 0)
            {
                return NotFound("Job Order not found.");
            }

            var jobOrder = await _Localdb.JobOrder
                .AsNoTracking()
                .FirstAsync(record => record.JobOrder_ID == jobOrderId);
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

            updateJobOrder.Installation_Date = _jobOrder.Installation_Date;
            updateJobOrder.Target_Delivery = _jobOrder.Target_Delivery;
            updateJobOrder.Date_Delivered = _jobOrder.Date_Delivered;


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

        // ----------------------------------- JOB ORDER -----------------------------------

        // ----------------------------------- GRAPHICS -----------------------------------

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

        // ----------------------------------- GRAPHICS -----------------------------------

        // ----------------------------------- INSTALLATION SCHEDULE -----------------------------------

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
        public ActionResult<Installation_Schedule> AddLocalInstallationSchedule([FromBody] Installation_Schedule installationSchedule)
        {
            if (!ModelState.IsValid || installationSchedule == null ||
                installationSchedule.JobOrder_ID <= 0 ||
                installationSchedule.Date == default ||
                string.IsNullOrWhiteSpace(installationSchedule.Mall_Hours))
            {
                return BadRequest("A valid job order, date, and mall-hours group are required.");
            }
            if (!_Localdb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == installationSchedule.JobOrder_ID))
            {
                return BadRequest("The selected Job Order was not found.");
            }

            installationSchedule.Installation_ID = 0;
            _Localdb.Installation_Schedule.Add(installationSchedule);
            _Localdb.SaveChanges();
            return Ok(installationSchedule);
        }

        [HttpPost("UpdateLocalInstallation_Schedule")]
        public ActionResult<Installation_Schedule> UpdateLocalInstallationSchedule(int Id, [FromBody] Installation_Schedule installationSchedule)
        {
            if (Id <= 0 || installationSchedule == null ||
                installationSchedule.JobOrder_ID <= 0 ||
                installationSchedule.Date == default ||
                string.IsNullOrWhiteSpace(installationSchedule.Mall_Hours))
            {
                return BadRequest("A valid schedule row, job order, date, and mall-hours group are required.");
            }
            if (!_Localdb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == installationSchedule.JobOrder_ID))
            {
                return BadRequest("The selected Job Order was not found.");
            }

            var existingSchedule = _Localdb.Installation_Schedule.FirstOrDefault(schedule => schedule.Installation_ID == Id);
            if (existingSchedule == null)
            {
                return NotFound();
            }

            existingSchedule.Time = installationSchedule.Time;
            existingSchedule.Remarks = installationSchedule.Remarks;
            existingSchedule.Mall_Hours = installationSchedule.Mall_Hours;
            existingSchedule.Date = installationSchedule.Date;
            existingSchedule.JobOrder_ID = installationSchedule.JobOrder_ID;
            _Localdb.SaveChanges();
            return Ok(existingSchedule);
        }

        [HttpPut("DeleteLocalInstallationSchedule")]
        public async Task<IActionResult> DeleteLocalInstallationSchedule(int id)
        {
            if (id <= 0)
            {
                return BadRequest("A valid schedule row is required.");
            }

            await using var transaction = await _Localdb.Database.BeginTransactionAsync();
            await _Localdb.Installation_Schedule_Employee
                .Where(assignment => assignment.Installation_ID == id)
                .ExecuteDeleteAsync();

            var deletedSchedules = await _Localdb.Installation_Schedule
                .Where(schedule => schedule.Installation_ID == id)
                .ExecuteDeleteAsync();
            if (deletedSchedules == 0)
            {
                await transaction.CommitAsync();
                return NoContent();
            }

            var now = DateTime.Now;
            _Localdb.AuditTrail.Add(new AuditTrail
            {
                Name = _Localdb.GetCurrentEmployeeName(),
                Date = DateOnly.FromDateTime(now),
                Time = TimeOnly.FromDateTime(now),
                Action = $"Installation_Schedule# {id} has been deleted",
                Department = "JobOrder"
            });
            await _Localdb.SaveChangesAsync();
            await transaction.CommitAsync();
            return NoContent();
        }

        [HttpGet("GetLocalInstallationScheduleEmployees")]
        public ActionResult<List<InstallationScheduleEmployeeOptionDto>> GetLocalInstallationScheduleEmployees()
        {
            var employees = _Localdb.Employee
                .AsNoTracking()
                .Where(employee => employee.Date_Resigned == null)
                .OrderBy(employee => employee.LastName)
                .ThenBy(employee => employee.FirstName)
                .Select(employee => new InstallationScheduleEmployeeOptionDto
                {
                    Employee_Id = employee.Employee_Id,
                    FirstName = employee.FirstName,
                    LastName = employee.LastName
                })
                .ToList();

            return Ok(employees);
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

        [HttpPost("AddLocalInstallationScheduleEmployee")]
        public ActionResult<InstallationScheduleEmployeeAssignmentDto> AddLocalInstallationScheduleEmployee(
            [FromBody] AddInstallationScheduleEmployeeRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!_Localdb.Installation_Schedule.Any(schedule => schedule.Installation_ID == request.Installation_ID))
            {
                return NotFound("The selected installation schedule row was not found.");
            }

            var employee = _Localdb.Employee.FirstOrDefault(candidate =>
                candidate.Employee_Id == request.Employee_Id && candidate.Date_Resigned == null);
            if (employee == null)
            {
                return NotFound("The selected active employee was not found.");
            }

            if (_Localdb.Installation_Schedule_Employee.Any(assignment =>
                    assignment.Installation_ID == request.Installation_ID &&
                    assignment.Employee_Id == request.Employee_Id))
            {
                return Conflict("This employee is already assigned to this schedule row.");
            }

            _Localdb.Installation_Schedule_Employee.Add(new InstallationScheduleEmployee
            {
                Installation_ID = request.Installation_ID,
                Employee_Id = employee.Employee_Id
            });
            _Localdb.SaveChanges();

            return Ok(new InstallationScheduleEmployeeAssignmentDto
            {
                Installation_ID = request.Installation_ID,
                Employee_Id = employee.Employee_Id,
                FirstName = employee.FirstName,
                LastName = employee.LastName
            });
        }

        [HttpDelete("DeleteLocalInstallationScheduleEmployee")]
        public IActionResult DeleteLocalInstallationScheduleEmployee(int installationId, int employeeId)
        {
            if (installationId <= 0 || employeeId <= 0)
            {
                return BadRequest("A valid schedule row and employee are required.");
            }

            var assignment = _Localdb.Installation_Schedule_Employee.FirstOrDefault(candidate =>
                candidate.Installation_ID == installationId &&
                candidate.Employee_Id == employeeId);
            if (assignment == null)
            {
                return NotFound("The employee is not assigned to this schedule row.");
            }

            _Localdb.Installation_Schedule_Employee.Remove(assignment);
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
        public ActionResult<Installation_Schedule> AddCloudInstallationSchedule([FromBody] Installation_Schedule installationSchedule)
        {
            if (!ModelState.IsValid || installationSchedule == null ||
                installationSchedule.JobOrder_ID <= 0 ||
                installationSchedule.Date == default ||
                string.IsNullOrWhiteSpace(installationSchedule.Mall_Hours))
            {
                return BadRequest("A valid job order, date, and mall-hours group are required.");
            }
            if (!_CloudDb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == installationSchedule.JobOrder_ID))
            {
                return BadRequest("The selected Job Order was not found.");
            }

            installationSchedule.Installation_ID = 0;
            _CloudDb.Installation_Schedule.Add(installationSchedule);
            _CloudDb.SaveChanges();
            return Ok(installationSchedule);
        }

        [HttpPost("UpdateCloudInstallation_Schedule")]
        public ActionResult<Installation_Schedule> UpdateCloudInstallationSchedule(int Id, [FromBody] Installation_Schedule installationSchedule)
        {
            if (Id <= 0 || installationSchedule == null ||
                installationSchedule.JobOrder_ID <= 0 ||
                installationSchedule.Date == default ||
                string.IsNullOrWhiteSpace(installationSchedule.Mall_Hours))
            {
                return BadRequest("A valid schedule row, job order, date, and mall-hours group are required.");
            }
            if (!_CloudDb.JobOrder.Any(jobOrder => jobOrder.JobOrder_ID == installationSchedule.JobOrder_ID))
            {
                return BadRequest("The selected Job Order was not found.");
            }

            var existingSchedule = _CloudDb.Installation_Schedule.FirstOrDefault(schedule => schedule.Installation_ID == Id);
            if (existingSchedule == null)
            {
                return NotFound();
            }

            existingSchedule.Time = installationSchedule.Time;
            existingSchedule.Remarks = installationSchedule.Remarks;
            existingSchedule.Mall_Hours = installationSchedule.Mall_Hours;
            existingSchedule.Date = installationSchedule.Date;
            existingSchedule.JobOrder_ID = installationSchedule.JobOrder_ID;
            _CloudDb.SaveChanges();
            return Ok(existingSchedule);
        }

        [HttpPut("DeleteCloudInstallationSchedule")]
        public IActionResult DeleteCloudInstallationSchedule(int id)
        {
            if (id <= 0)
            {
                return BadRequest("A valid schedule row is required.");
            }

            var installationSchedule = _CloudDb.Installation_Schedule.FirstOrDefault(schedule => schedule.Installation_ID == id);
            if (installationSchedule == null)
            {
                return NotFound();
            }

            _CloudDb.Installation_Schedule.Remove(installationSchedule);
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // ----------------------------------- INSTALLATION SCHEDULE -----------------------------------

        // --------------------------- MATERIALS REQUESITION SLIP ---------------------------

        [HttpGet("GetLocalMaterialRequesitionSlips")]
        public List<MaterialRequesition_Slip> GetAllLocalMaterialRequesitionSlips()
        {
            return _Localdb.MaterialRequisition_Slip.ToList();
        }

        [HttpGet("GetLocalMaterialRequesitionSlipsByID")]
        public ActionResult<MaterialRequesition_Slip> GetLocalMaterialRequesition_SlipByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var materialRequesitionSlip = _Localdb.MaterialRequisition_Slip.FirstOrDefault(x => x.MR_ID == id);
            if (materialRequesitionSlip == null)
            {
                return NotFound();
            }
            return Ok(materialRequesitionSlip);
        }

        [HttpPost("AddLocalMaterialRequesitionSlips")]
        public ActionResult<MaterialRequesition_Slip> AddLocalMaterialRequesitionSlips(MaterialRequesition_Slip materialRequesitionSlip)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(materialRequesitionSlip);
            _Localdb.SaveChanges();
            return Ok(materialRequesitionSlip);
        }

        [HttpPost("UpdateLocalMaterialRequesition_Slip")]
        public ActionResult<MaterialRequesition_Slip> UpdateLocalMaterialRequesition_Slip(Int32 Id, [FromBody] MaterialRequesition_Slip materialRequesitionSlip)
        {
            if (materialRequesitionSlip == null)
            {
                return BadRequest(materialRequesitionSlip);
            }
            var updateMaterialRequesitionSlip = _Localdb.MaterialRequisition_Slip.FirstOrDefault(x => x.MR_ID == Id);
            if (updateMaterialRequesitionSlip == null)
            {
                return NotFound();
            }
            updateMaterialRequesitionSlip.Control_Number = materialRequesitionSlip.Control_Number;
            updateMaterialRequesitionSlip.Date = materialRequesitionSlip.Date;
            updateMaterialRequesitionSlip.FromWho = materialRequesitionSlip.FromWho;
            updateMaterialRequesitionSlip.ToWho = materialRequesitionSlip.ToWho;
            updateMaterialRequesitionSlip.Material_Name = materialRequesitionSlip.Material_Name;
            updateMaterialRequesitionSlip.Project_Name = materialRequesitionSlip.Project_Name;
            updateMaterialRequesitionSlip.Supplier = materialRequesitionSlip.Supplier;
            updateMaterialRequesitionSlip.Quantity = materialRequesitionSlip.Quantity;
            updateMaterialRequesitionSlip.Unit_Cost = materialRequesitionSlip.Unit_Cost;
            updateMaterialRequesitionSlip.Total_Cost = materialRequesitionSlip.Total_Cost;
            updateMaterialRequesitionSlip.Noted_By = materialRequesitionSlip.Noted_By;
            updateMaterialRequesitionSlip.Approved_By = materialRequesitionSlip.Approved_By;
            updateMaterialRequesitionSlip.Submmited_By = materialRequesitionSlip.Submmited_By;
            updateMaterialRequesitionSlip.Received_By = materialRequesitionSlip.Received_By;

            _Localdb.SaveChanges();
            return Ok(updateMaterialRequesitionSlip);
        }

        [HttpPut("DeleteLocalMaterialRequesitionSlip")]
        public ActionResult<MaterialRequesition_Slip> DeleteLocalMaterialRequesitionSlip(int id)
        {
            var materialRequesitionSlip = _Localdb.MaterialRequisition_Slip.FirstOrDefault(x => x.MR_ID == id);
            if (materialRequesitionSlip == null)
            {
                return NotFound();
            }
            _Localdb.Remove(materialRequesitionSlip);
            _Localdb.SaveChanges();
            return NoContent();
        }

        [HttpGet("GetCloudMaterialRequesitionSlips")]
        public List<MaterialRequesition_Slip> GetAllCloudMaterialRequesitionSlips()
        {
            return _CloudDb.MaterailsRequisition_Slip.ToList();
        }

        [HttpGet("GetCloudMaterialRequesitionSlipsByID")]
        public ActionResult<MaterialRequesition_Slip> GetCloudMaterialRequesition_SlipByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid client ID.");
            }
            var materialRequesitionSlip = _CloudDb.MaterailsRequisition_Slip.FirstOrDefault(x => x.MR_ID == id);
            if (materialRequesitionSlip == null)
            {
                return NotFound();
            }
            return Ok(materialRequesitionSlip);
        }

        [HttpPost("AddCloudMaterialRequesitionSlips")]
        public ActionResult<MaterialRequesition_Slip> AddCloudMaterialRequesitionSlips(MaterialRequesition_Slip materialRequesitionSlip)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _CloudDb.Add(materialRequesitionSlip);
            _CloudDb.SaveChanges();
            return Ok(materialRequesitionSlip);
        }

        [HttpPost("UpdateCloudMaterialRequesition_Slip")]
        public ActionResult<MaterialRequesition_Slip> UpdateCloudMaterialRequesition_Slip(Int32 Id, [FromBody] MaterialRequesition_Slip materialRequesitionSlip)
        {
            if (materialRequesitionSlip == null)
            {
                return BadRequest(materialRequesitionSlip);
            }
            var updateMaterialRequesitionSlip = _CloudDb.MaterailsRequisition_Slip.FirstOrDefault(x => x.MR_ID == Id);
            if (updateMaterialRequesitionSlip == null)
            {
                return NotFound();
            }
            updateMaterialRequesitionSlip.Control_Number = materialRequesitionSlip.Control_Number;
            updateMaterialRequesitionSlip.Date = materialRequesitionSlip.Date;
            updateMaterialRequesitionSlip.FromWho = materialRequesitionSlip.FromWho;
            updateMaterialRequesitionSlip.ToWho = materialRequesitionSlip.ToWho;
            updateMaterialRequesitionSlip.Material_Name = materialRequesitionSlip.Material_Name;
            updateMaterialRequesitionSlip.Project_Name = materialRequesitionSlip.Project_Name;
            updateMaterialRequesitionSlip.Supplier = materialRequesitionSlip.Supplier;
            updateMaterialRequesitionSlip.Quantity = materialRequesitionSlip.Quantity;
            updateMaterialRequesitionSlip.Unit_Cost = materialRequesitionSlip.Unit_Cost;
            updateMaterialRequesitionSlip.Total_Cost = materialRequesitionSlip.Total_Cost;
            updateMaterialRequesitionSlip.Noted_By = materialRequesitionSlip.Noted_By;
            updateMaterialRequesitionSlip.Approved_By = materialRequesitionSlip.Approved_By;
            updateMaterialRequesitionSlip.Submmited_By = materialRequesitionSlip.Submmited_By;
            updateMaterialRequesitionSlip.Received_By = materialRequesitionSlip.Received_By;

            _CloudDb.SaveChanges();
            return Ok(updateMaterialRequesitionSlip);
        }

        [HttpPut("DeleteCloudMaterialRequesitionSlip")]
        public ActionResult<MaterialRequesition_Slip> DeleteCloudMaterialRequesitionSlip(int id)
        {
            var materialRequesitionSlip = _CloudDb.MaterailsRequisition_Slip.FirstOrDefault(x => x.MR_ID == id);
            if (materialRequesitionSlip == null)
            {
                return NotFound();
            }
            _CloudDb.Remove(materialRequesitionSlip);
            _CloudDb.SaveChanges();
            return NoContent();
        }

        // --------------------------- MATERIALS REQUESITION SLIP ---------------------------

        [HttpGet("GetLocalQuotations")]
        public List<Quotation> GetAllLocalQuotations()
        {
            return _Localdb.Quotation.ToList();
        }

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

        [HttpGet("GetAllLocalClients")]
        public List<ClientEntity> GetAllLocalClients()
        {
            return _Localdb.Clients.ToList();
        }



    }
}
