using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;
using System.Text.Json;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize(Policy = "admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private LocalDbContext _Localdb;
        private CloudDbContext _CloudDb;
        private readonly PasswordService _passwords;   // inject in the constructor
        public UsersController(LocalDbContext Localcontext, CloudDbContext Cloudcontext, PasswordService Password)
        {
            _Localdb = Localcontext;
            _CloudDb = Cloudcontext;
            _passwords = Password;
        }

        [HttpGet("GetLocalUsers")]
        public ActionResult<List<UserListDto>> GetLocalUsers() =>
        _Localdb.Users.AsNoTracking().Select(u => new UserListDto
        {
            User_ID = u.User_ID,
            Username = u.Username,
            Email = u.Email,
            Employee_ID = u.Employee_ID,
            Role_ID = u.Role_ID,
            Department_ID = u.Department_ID,
            IsActive = u.IsActive
        }).ToList();


        [HttpPost("AddLocalUsers")]
        public ActionResult AddLocalUsers([FromBody] CreateUserDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || dto.Password.Length < 8)
                return BadRequest("Username and a password of at least 8 characters are required.");
            if (_Localdb.Users.Any(u => u.Username == dto.Username))
                return Conflict("That username already exists.");

            var user = new Users
            {
                Username = dto.Username.Trim(),
                Email = dto.Email,
                PasswordHash = _passwords.Hash(dto.Password),
                Employee_ID = dto.Employee_ID,
                Role_ID = dto.Role_ID,
                Department_ID = dto.Department_ID,
                IsActive = true
            };
            _Localdb.Users.Add(user);
            _Localdb.SaveChanges();
            return Ok(new { user.User_ID });
        }

        [HttpPost("UpdateLocalUsers")]
        public ActionResult UpdateLocalUsers(int id, [FromBody] UpdateUserDto dto)
        {
            var user = _Localdb.Users.FirstOrDefault(u => u.User_ID == id);
            if (user == null) return NotFound();

            user.Username = dto.Username.Trim();
            user.Email = dto.Email;
            user.Role_ID = dto.Role_ID;
            user.Department_ID = dto.Department_ID;
            user.IsActive = dto.IsActive;          // PasswordHash is never touched here
            _Localdb.SaveChanges();
            return Ok();
        }

        [HttpPut("DeleteLocalUsers")]
        public async Task<IActionResult> DeleteLocalUsers([FromBody] DeleteUsersDto dto)
        {
            var adminIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(adminIdValue, out var adminId))
            {
                return Unauthorized(new { message = "Your session is invalid. Sign in again." });
            }
            if (dto.TargetUserIds == null ||
                dto.TargetUserIds.Count == 0 ||
                dto.TargetUserIds.Any(id => id <= 0) ||
                dto.TargetUserIds.Distinct().Count() != dto.TargetUserIds.Count)
            {
                return BadRequest(new { message = "Select one or more valid user accounts to delete." });
            }
            if (string.IsNullOrWhiteSpace(dto.AdminPassword))
            {
                return BadRequest(new { message = "Enter your current password to delete accounts." });
            }

            var admin = await _Localdb.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.User_ID == adminId);
            if (admin == null || !_passwords.Verify(admin.PasswordHash, dto.AdminPassword))
            {
                return StatusCode(403, new { message = "Your password was not accepted." });
            }

            if (dto.TargetUserIds.Contains(adminId))
            {
                return BadRequest(new { message = "You cannot delete the account you are currently signed in with." });
            }

            await using var transaction = await _Localdb.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var targetIds = dto.TargetUserIds.ToArray();
            var users = await _Localdb.Users
                .Where(user => targetIds.Contains(user.User_ID))
                .ToListAsync();
            if (users.Count != targetIds.Length)
            {
                return NotFound(new { message = "One or more selected accounts could not be found." });
            }

            var protectedRoleIds = await _Localdb.Roles
                .Where(role => role.Role_Name == "Admin" || role.Role_Name == "Super Admin")
                .Select(role => role.Role_ID)
                .ToListAsync();
            if (!protectedRoleIds.Contains(admin.Role_ID))
            {
                protectedRoleIds.Add(admin.Role_ID);
            }
            if (users.Any(user => user.IsActive && protectedRoleIds.Contains(user.Role_ID)))
            {
                var remainingActiveAdmins = await _Localdb.Users.CountAsync(user =>
                    user.IsActive &&
                    protectedRoleIds.Contains(user.Role_ID) &&
                    !targetIds.Contains(user.User_ID));
                if (remainingActiveAdmins == 0)
                {
                    return Conflict(new { message = "At least one active administrator account must remain." });
                }
            }

            _Localdb.Users.RemoveRange(users);
            await _Localdb.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { deletedCount = users.Count });
        }

        [HttpPost("ResetLocalUserPassword")]
        public ActionResult ResetLocalUserPassword([FromBody] ResetPasswordDto dto)
        {
            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var admin = _Localdb.Users.AsNoTracking().FirstOrDefault(u => u.User_ID == adminId);
            if (admin == null || !_passwords.Verify(admin.PasswordHash, dto.AdminPassword))
                return StatusCode(403, new { message = "Your password was not accepted." });

            var target = _Localdb.Users.FirstOrDefault(u => u.User_ID == dto.TargetUserId);
            if (target == null) return NotFound();

            target.PasswordHash = _passwords.Hash("mirhemaG123");
            _Localdb.SaveChanges();
            return Ok(new { message = "Password reset to the default password." });
        }

        [HttpGet("Me")]
        public ActionResult Me()
        {
            var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var u = _Localdb.Users.AsNoTracking().Where(x => x.User_ID == id)
                .Select(x => new { x.User_ID, x.Username, x.Email, x.Employee_ID }).FirstOrDefault();
            return u == null ? NotFound() : Ok(u);
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
