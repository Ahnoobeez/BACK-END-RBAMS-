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
    // Coordinates the users controller API endpoints, including request handling and response generation.
    public class UsersController : ControllerBase
    {
        // Injected or cached localdb dependency/state used by this class.
        private LocalDbContext _Localdb;
        // Injected or cached cloud db dependency/state used by this class.
        private CloudDbContext _CloudDb;
        // Injected or cached passwords dependency/state used by this class.
        private readonly PasswordService _passwords;   // inject in the constructor
        // Initializes a new users controller instance.
        public UsersController(LocalDbContext Localcontext, CloudDbContext Cloudcontext, PasswordService Password)
        {
            _Localdb = Localcontext;
            _CloudDb = Cloudcontext;
            _passwords = Password;
        }

        /// <summary>Retrieves the local users.</summary>
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


        /// <summary>Adds the local users.</summary>
        [HttpPost("AddLocalUsers")]
        public async Task<ActionResult> AddLocalUsers(
            [FromBody] CreateUserDto dto,
            CancellationToken cancellationToken)
        {
            // Return HTTP 400 Bad Request when `string.IsNullOrWhiteSpace(dto.Username)` is true.
            if (string.IsNullOrWhiteSpace(dto.Username))
                return BadRequest("A username is required.");
            // Return HTTP 400 Bad Request when `!PasswordPolicy.IsValid(dto.Password)` is true.
            if (!PasswordPolicy.IsValid(dto.Password))
                return BadRequest(PasswordPolicy.RequirementMessage);

            // Return HTTP 400 Bad Request when `dto.Employee_ID <= 0` is true.
            if (dto.Employee_ID <= 0)
                return BadRequest("Select a valid employee.");

            await using var transaction = await _Localdb.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            // Return HTTP 409 Conflict when `await _Localdb.Users.AnyAsync(u => u.Username == dto.Username, cancellationToken)` is true.
            if (await _Localdb.Users.AnyAsync(u => u.Username == dto.Username, cancellationToken))
                return Conflict("That username already exists.");
            var employee = await _Localdb.Employee.AsNoTracking().FirstOrDefaultAsync(
                record => record.Employee_Id == dto.Employee_ID,
                cancellationToken);
            // Return HTTP 400 Bad Request when `employee == null` is true.
            if (employee == null)
            {
                return BadRequest("The selected employee could not be found.");
            }
            // Return HTTP 400 Bad Request when `employee.IsArchived || employee.Date_Resigned.HasValue` is true.
            if (employee.IsArchived || employee.Date_Resigned.HasValue)
            {
                return BadRequest("A resigned or archived employee cannot receive a user account.");
            }
            // Return HTTP 409 Conflict when `await _Localdb.Users.IgnoreQueryFilters().AnyAsync( user => user.Employee_ID == dto.Employee_ID, cancellationToken)` is true.
            if (await _Localdb.Users.IgnoreQueryFilters().AnyAsync(
                    user => user.Employee_ID == dto.Employee_ID,
                    cancellationToken))
            {
                return Conflict("This employee already has a user account.");
            }

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
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync(cancellationToken);

            // Enter this branch when `dto.Employee_ID > 0` is true.
            if (dto.Employee_ID > 0)
            {
                var employeePhoto = await _Localdb.EmployeeProfilePhotos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        photo => photo.Employee_ID == dto.Employee_ID,
                        cancellationToken);
                // Enter this branch when `employeePhoto != null` is true.
                if (employeePhoto != null)
                {
                    _Localdb.UserProfilePhotos.Add(new UserProfilePhoto
                    {
                        User_ID = user.User_ID,
                        PhotoData = employeePhoto.PhotoData,
                        ContentType = employeePhoto.ContentType,
                        UpdatedAt = DateTime.UtcNow
                    });
                    // Persist the pending entity changes to the database at this point.
                    await _Localdb.SaveChangesAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return Ok(new { user.User_ID });
        }

        /// <summary>Updates the local users.</summary>
        [HttpPost("UpdateLocalUsers")]
        public ActionResult UpdateLocalUsers(int id, [FromBody] UpdateUserDto dto)
        {
            var user = _Localdb.Users.FirstOrDefault(u => u.User_ID == id);
            // Return HTTP 404 Not Found when `user == null` is true.
            if (user == null) return NotFound();

            user.Username = dto.Username.Trim();
            user.Email = dto.Email;
            user.Role_ID = dto.Role_ID;
            user.Department_ID = dto.Department_ID;
            user.IsActive = dto.IsActive;          // PasswordHash is never touched here
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok();
        }

        /// <summary>Deletes the local users.</summary>
        [HttpPut("DeleteLocalUsers")]
        public async Task<IActionResult> DeleteLocalUsers([FromBody] DeleteUsersDto dto)
        {
            var adminIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Return HTTP 401 Unauthorized when `!int.TryParse(adminIdValue, out var adminId)` is true.
            if (!int.TryParse(adminIdValue, out var adminId))
            {
                return Unauthorized(new { message = "Your session is invalid. Sign in again." });
            }
            // Return HTTP 400 Bad Request when `dto.TargetUserIds == null || dto.TargetUserIds.Count == 0 || dto.TargetUserIds.Any(id => id <= 0) || dto.TargetUserIds.Distinct().Count() != dto.TargetUserIds.Count` is true.
            if (dto.TargetUserIds == null ||
                dto.TargetUserIds.Count == 0 ||
                dto.TargetUserIds.Any(id => id <= 0) ||
                dto.TargetUserIds.Distinct().Count() != dto.TargetUserIds.Count)
            {
                return BadRequest(new { message = "Select one or more valid user accounts to delete." });
            }
            // Return HTTP 400 Bad Request when `string.IsNullOrWhiteSpace(dto.AdminPassword)` is true.
            if (string.IsNullOrWhiteSpace(dto.AdminPassword))
            {
                return BadRequest(new { message = "Enter your current password to delete accounts." });
            }

            var admin = await _Localdb.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(user => user.User_ID == adminId);
            // Exit the current operation when `admin == null || !_passwords.Verify(admin.PasswordHash, dto.AdminPassword)` is true.
            if (admin == null || !_passwords.Verify(admin.PasswordHash, dto.AdminPassword))
            {
                return StatusCode(403, new { message = "Your password was not accepted." });
            }

            // Return HTTP 400 Bad Request when `dto.TargetUserIds.Contains(adminId)` is true.
            if (dto.TargetUserIds.Contains(adminId))
            {
                return BadRequest(new { message = "You cannot delete the account you are currently signed in with." });
            }

            await using var transaction = await _Localdb.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var targetIds = dto.TargetUserIds.ToArray();
            var users = await _Localdb.Users
                // Apply this predicate in the database query before materializing results.
                .Where(user => targetIds.Contains(user.User_ID))
                .ToListAsync();
            // Return HTTP 404 Not Found when `users.Count != targetIds.Length` is true.
            if (users.Count != targetIds.Length)
            {
                return NotFound(new { message = "One or more selected accounts could not be found." });
            }

            var protectedRoleIds = await _Localdb.Roles
                // Apply this predicate in the database query before materializing results.
                .Where(role => role.Role_Name == "Admin" || role.Role_Name == "Super Admin")
                .Select(role => role.Role_ID)
                .ToListAsync();
            // Enter this branch when `!protectedRoleIds.Contains(admin.Role_ID)` is true.
            if (!protectedRoleIds.Contains(admin.Role_ID))
            {
                protectedRoleIds.Add(admin.Role_ID);
            }
            // Enter this branch when `users.Any(user => user.IsActive && protectedRoleIds.Contains(user.Role_ID))` is true.
            if (users.Any(user => user.IsActive && protectedRoleIds.Contains(user.Role_ID)))
            {
                var remainingActiveAdmins = await _Localdb.Users.CountAsync(user =>
                    user.IsActive &&
                    protectedRoleIds.Contains(user.Role_ID) &&
                    !targetIds.Contains(user.User_ID));
                // Return HTTP 409 Conflict when `remainingActiveAdmins == 0` is true.
                if (remainingActiveAdmins == 0)
                {
                    return Conflict(new { message = "At least one active administrator account must remain." });
                }
            }

            _Localdb.Users.RemoveRange(users);
            // Persist the pending entity changes to the database at this point.
            await _Localdb.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { deletedCount = users.Count });
        }

        /// <summary>Resets the local user password.</summary>
        [HttpPost("ResetLocalUserPassword")]
        public ActionResult ResetLocalUserPassword([FromBody] ResetPasswordDto dto)
        {
            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var admin = _Localdb.Users.AsNoTracking().FirstOrDefault(u => u.User_ID == adminId);
            // Exit the current operation when `admin == null || !_passwords.Verify(admin.PasswordHash, dto.AdminPassword)` is true.
            if (admin == null || !_passwords.Verify(admin.PasswordHash, dto.AdminPassword))
                return StatusCode(403, new { message = "Your password was not accepted." });

            var target = _Localdb.Users.FirstOrDefault(u => u.User_ID == dto.TargetUserId);
            // Return HTTP 404 Not Found when `target == null` is true.
            if (target == null) return NotFound();

            target.PasswordHash = _passwords.Hash(PasswordPolicy.DefaultResetPassword);
            // Persist the pending entity changes to the database at this point.
            _Localdb.SaveChanges();
            return Ok(new { message = $"Password reset to the default password: {PasswordPolicy.DefaultResetPassword}" });
        }

        /// <summary>Retrieves the authenticated user's profile.</summary>
        [HttpGet("Me")]
        public ActionResult Me()
        {
            var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            // Apply this predicate in the database query before materializing results.
            var u = _Localdb.Users.AsNoTracking().Where(x => x.User_ID == id)
                .Select(x => new { x.User_ID, x.Username, x.Email, x.Employee_ID }).FirstOrDefault();
            return u == null ? NotFound() : Ok(u);
        }

        /// <summary>Retrieves all roles from the local database.</summary>
        [HttpGet("GetLocalRoles")]
        public List<Roles> GetAllLocalRoles()
        {
            return _Localdb.Roles.ToList();
        }
        /// <summary>Retrieves all departments from the local database.</summary>
        [HttpGet("GetLocalDepartments")]
        public List<Departments> GetAllLocalDepartments()
        {
            return _Localdb.Departments.ToList();
        }

    }
}
