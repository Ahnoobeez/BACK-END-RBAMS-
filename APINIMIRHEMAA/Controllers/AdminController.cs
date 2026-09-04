using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APINIMIRHEMAA.Controllers
{
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
    }
}
