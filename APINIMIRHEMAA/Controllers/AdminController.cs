using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO.Admin;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize(Policy = "admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private LocalDbContext _Localdb;
        private CloudDbContext _Clouddb;
        private readonly AccessPermissionService _accessPermissions;

        public AdminController(
            LocalDbContext Localcontext,
            CloudDbContext Cloudcontext,
            AccessPermissionService accessPermissions)
        {
            _Localdb = Localcontext;
            _Clouddb = Cloudcontext;
            _accessPermissions = accessPermissions;
        }

        [HttpGet("GetLocalAuditTrail")]
        public List<AuditTrail> GetAllLocalAuditTrail()
        {
            return _Localdb.AuditTrail.ToList();
        }

        [HttpGet("GetLocalAuditTrailByID")]
        public ActionResult<AuditTrail> GetLocalAuditTrailByID(int id)
        {
            if (id == 0)
            {
                return BadRequest("Invalid Audit Trail ID.");
            }
            var auditTrail = _Localdb.AuditTrail.FirstOrDefault(x => x.AuditTrail_ID == id);
            if (auditTrail == null)
            {
                return NotFound();
            }
            return Ok(auditTrail);
        }

        [HttpPost("AddLocalAuditTrail")]
        public ActionResult<AuditTrail> AddLocalAuditTrail(AuditTrail auditTrail)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _Localdb.Add(auditTrail);
            _Localdb.SaveChanges();
            return Ok(auditTrail);
        }


        [HttpGet("GetLocalEmployees")]
        public List<Employee> GetAllLocalEmployee()
        {
            return _Localdb.Employee.ToList();
        }

        [HttpPost("AddLocalEmployee")]
        public async Task<ActionResult<Employee>> AddLocalEmployee(
            [FromBody] Employee? employee,
            CancellationToken cancellationToken)
        {
            if (employee == null)
            {
                return BadRequest(new { message = "Employee details are required." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (employee.Employee_Id < 0)
            {
                return BadRequest(new { message = "Employee ID cannot be negative." });
            }

            if (string.IsNullOrWhiteSpace(employee.FirstName) ||
                string.IsNullOrWhiteSpace(employee.LastName) ||
                string.IsNullOrWhiteSpace(employee.status))
            {
                return BadRequest(new { message = "First name, last name, and status are required." });
            }

            if (!employee.Department_ID.HasValue ||
                !await _Localdb.Departments.AnyAsync(
                    department => department.Department_ID == employee.Department_ID.Value,
                    cancellationToken))
            {
                return BadRequest(new { message = "Select a valid department." });
            }

            if (!employee.Role_ID.HasValue ||
                !await _Localdb.Roles.AnyAsync(
                    role => role.Role_ID == employee.Role_ID.Value,
                    cancellationToken))
            {
                return BadRequest(new { message = "Select a valid role." });
            }

            await using var transaction = await _Localdb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            if (employee.Employee_Id == 0)
            {
                var lastEmployeeId = await _Localdb.Employee
                    .MaxAsync(record => (int?)record.Employee_Id, cancellationToken) ?? 0;
                employee.Employee_Id = checked(lastEmployeeId + 1);
            }
            else if (await _Localdb.Employee.AnyAsync(
                         record => record.Employee_Id == employee.Employee_Id,
                         cancellationToken))
            {
                return Conflict(new { message = $"Employee ID {employee.Employee_Id} is already in use." });
            }

            employee.FirstName = employee.FirstName.Trim();
            employee.LastName = employee.LastName.Trim();
            employee.MiddleName = string.IsNullOrWhiteSpace(employee.MiddleName)
                ? null
                : employee.MiddleName.Trim();
            employee.status = employee.status.Trim();
            employee.ContactNumber = string.IsNullOrWhiteSpace(employee.ContactNumber)
                ? null
                : employee.ContactNumber.Trim();
            employee.SSS_Number = string.IsNullOrWhiteSpace(employee.SSS_Number)
                ? null
                : employee.SSS_Number.Trim();
            employee.Philhealth_Number = string.IsNullOrWhiteSpace(employee.Philhealth_Number)
                ? null
                : employee.Philhealth_Number.Trim();
            employee.Pagibig_Number = string.IsNullOrWhiteSpace(employee.Pagibig_Number)
                ? null
                : employee.Pagibig_Number.Trim();

            _Localdb.Employee.Add(employee);
            await _Localdb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(employee);
        }

        [HttpGet("GetLocalRoles")]
        public List<Roles> GetAllLocalRoles()
        {
            return _Localdb.Roles.ToList();
        }

        [HttpGet("GetLocalDeparments")]
        public List<Departments> GetAllLocalDepartments()
        {
            return _Localdb.Departments.ToList();
        }

        [HttpGet("AccessPermissions")]
        public async Task<ActionResult<List<AccessPermissionValueDto>>> GetAccessPermissions(
            [FromQuery] int? roleId,
            [FromQuery] int? departmentId,
            [FromQuery] int? userId,
            CancellationToken cancellationToken)
        {
            if (userId.HasValue)
            {
                if (!await _Localdb.Users.AnyAsync(user => user.User_ID == userId.Value, cancellationToken))
                    return NotFound(new { message = "The selected user account could not be found." });
            }
            else if (!roleId.HasValue && !departmentId.HasValue)
            {
                return BadRequest(new { message = "Select a role, department, or user." });
            }

            if (roleId.HasValue &&
                !await _Localdb.Roles.AnyAsync(role => role.Role_ID == roleId.Value, cancellationToken))
                return BadRequest(new { message = "The selected role could not be found." });
            if (departmentId.HasValue &&
                !await _Localdb.Departments.AnyAsync(department => department.Department_ID == departmentId.Value, cancellationToken))
                return BadRequest(new { message = "The selected department could not be found." });

            return Ok(await _accessPermissions.GetEffectivePermissionsAsync(
                userId, roleId, departmentId, cancellationToken));
        }

        [HttpPut("AccessPermissions")]
        public async Task<IActionResult> SaveAccessPermissions(
            [FromBody] SaveAccessPermissionsDto request,
            CancellationToken cancellationToken)
        {
            if (request.User_ID.HasValue && (request.Role_ID.HasValue || request.Department_ID.HasValue))
                return BadRequest(new { message = "User-specific changes cannot update role or department permissions." });
            if (!request.User_ID.HasValue && !request.Role_ID.HasValue && !request.Department_ID.HasValue)
                return BadRequest(new { message = "Select a role, department, or user before saving permissions." });

            if (request.User_ID.HasValue &&
                !await _Localdb.Users.AnyAsync(user => user.User_ID == request.User_ID.Value, cancellationToken))
                return NotFound(new { message = "The selected user account could not be found." });
            if (request.Role_ID.HasValue &&
                !await _Localdb.Roles.AnyAsync(role => role.Role_ID == request.Role_ID.Value, cancellationToken))
                return BadRequest(new { message = "The selected role could not be found." });
            if (request.Department_ID.HasValue &&
                !await _Localdb.Departments.AnyAsync(department => department.Department_ID == request.Department_ID.Value, cancellationToken))
                return BadRequest(new { message = "The selected department could not be found." });

            var permissions = request.Permissions ?? new List<AccessPermissionValueDto>();
            var expectedCount = AccessPermissionCatalog.Services.Length * AccessPermissionCatalog.Actions.Length;
            var distinctKeys = permissions
                .Select(permission => AccessPermissionService.Key(permission.ServiceKey, permission.ActionKey))
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (permissions.Count != expectedCount ||
                distinctKeys != expectedCount ||
                permissions.Any(permission =>
                    !AccessPermissionCatalog.IsValid(permission.ServiceKey, permission.ActionKey)))
            {
                return BadRequest(new { message = "The permission matrix is incomplete or contains an unknown service or action." });
            }

            await using var transaction = await _Localdb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var existing = await _Localdb.AccessPermissions
                .Where(permission =>
                    permission.User_ID == request.User_ID &&
                    permission.Role_ID == request.Role_ID &&
                    permission.Department_ID == request.Department_ID)
                .ToListAsync(cancellationToken);
            _Localdb.AccessPermissions.RemoveRange(existing);
            _Localdb.AccessPermissions.AddRange(permissions.Select(permission => new AccessPermission
            {
                User_ID = request.User_ID,
                Role_ID = request.User_ID.HasValue ? null : request.Role_ID,
                Department_ID = request.User_ID.HasValue ? null : request.Department_ID,
                ServiceKey = permission.ServiceKey,
                ActionKey = permission.ActionKey,
                IsAllowed = permission.Allowed
            }));
            await _Localdb.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new { message = "Access permissions saved successfully." });
        }
    }
}
