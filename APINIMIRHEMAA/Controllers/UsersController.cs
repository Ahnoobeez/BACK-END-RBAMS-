using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APINIMIRHEMAA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private LocalDbContext _Localdb;
        private CloudDbContext _CloudDb;
        public UsersController(LocalDbContext Localcontext, CloudDbContext Cloudcontext)
        {
            _Localdb = Localcontext;
            _CloudDb = Cloudcontext;
        }

        [HttpGet("GetLocalUsers")]
        public List<Users> GetAllLocalUsers()
        {
            return _Localdb.Users.ToList();
        }

        [HttpPost("AddLocalUsers")]
        public ActionResult<Users> AddLocalUsers([FromBody] Users users)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            users.PasswordHash =
                BCrypt.Net.BCrypt.HashPassword(users.PasswordHash);

            _Localdb.Users.Add(users);
            _Localdb.SaveChanges();

            return Ok(users);
        }

        [HttpPost("UpdateLocalUsers")]
        public ActionResult<Users> UpdateLocalUsers(Int32 Id, [FromBody] Users users)
        {
            if (users == null)
            {
                return BadRequest(users);
            }

            var _users = _Localdb.Users.FirstOrDefault(x => x.User_ID == Id);
            if (users == null)
            {
                return NotFound();
            }

            _users.User_ID = users.User_ID;
            _users.Employee_ID = users.Employee_ID;
            _users.Username = users.Username;
            _users.Employee_ID = users.Employee_ID;
            _users.Email = users.Email;
            _users.Role_ID = users.Role_ID;
            _users.Department_ID = users.Department_ID;
            _users.IsActive = users.IsActive;


            _Localdb.SaveChanges();


            return Ok(users);
        }

        [HttpPut("DeleteLocalUsers")]
        public ActionResult<Users> DeleteLocalUsers(Int32 Id)
        {

            var users = _Localdb.Users.FirstOrDefault(x => x.User_ID == Id);
            if (users == null)
            {
                return NotFound();
            }
            _Localdb.Remove(users);
            _Localdb.SaveChanges();


            return NoContent();
        }


        [HttpGet("GetLocalRoles")]
        public List<Roles> GetAllLocalRoles()
        {
            return _Localdb.Roles.ToList();
        }
        [HttpGet("GetLocalDepartments")]
        public List<Departments> GetAllLocalDepartments()
        {
            return _Localdb.Departments.ToList();
        }

    }
}
