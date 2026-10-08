using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize(Policy = "admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private LocalDbContext _Localdb;
        private CloudDbContext _Clouddb;

        public AdminController(LocalDbContext Localcontext, CloudDbContext Cloudcontext)
        {
            _Localdb = Localcontext;
            _Clouddb = Cloudcontext;
        }

        [HttpGet("GetLocalAuditTrail")]
        public List<AuditTrail> GetAllLocalAuditTrail()
        {
            return _Localdb.AuditTrail.ToList();
        }

        [HttpGet("GetCloudAuditTrail")]
        public List<AuditTrail> GetAllCloudAuditTrail()
        {
            return _Clouddb.AuditTrail.ToList();
        }

        [HttpGet("GetLocalEmployees")]
        public List<Employee> GetAllLocalEmployee()
        {
            return _Localdb.Employee.ToList();
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
    }
}
