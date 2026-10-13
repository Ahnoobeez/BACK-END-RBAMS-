using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO;
using APINIMIRHEMAA.DTO.Login;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace APINIMIRHEMAA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly LocalDbContext _context;
        private readonly JwtService _jwtService;
        private readonly PasswordService _passwords;

        public AuthController(LocalDbContext context, JwtService jwtService, PasswordService passwords)
        {
            _context = context;
            _jwtService = jwtService;
            _passwords = passwords;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null ||
                !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid username or password." });
            }
            if (!user.IsActive)
                return Unauthorized(new { message = "Account Inactive please see the admin." });

            var role = await _context.Roles
                .FirstOrDefaultAsync(r => r.Role_ID == user.Role_ID);
            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.Department_ID == user.Department_ID);

            if (role == null || department == null)
                return Unauthorized(new { message = "Account is not fully configured." });

            var token = _jwtService.GenerateToken(
                user.User_ID, user.Username, role.Role_Name, department.Department_Name);

            var modules = ModuleAccess.GetModules(role.Role_Name, department.Department_Name);

            return Ok(new
            {
                token,
                username = user.Username,
                role = role.Role_Name,
                department = department.Department_Name,
                modules
            });
        }

        [Authorize]
        [HttpPost("change-password")]
        public IActionResult ChangePassword([FromBody] ChangePasswordDto request)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                string.IsNullOrWhiteSpace(request.NewPassword) ||
                string.IsNullOrWhiteSpace(request.ConfirmNewPassword))
            {
                return BadRequest(new { message = "Enter your current password and both new password fields." });
            }
            if (request.NewPassword.Length < 8)
                return BadRequest(new { message = "Your new password must be at least 8 characters long." });
            if (request.NewPassword != request.ConfirmNewPassword)
                return BadRequest(new { message = "The new passwords do not match." });
            if (request.NewPassword == request.CurrentPassword)
                return BadRequest(new { message = "Choose a password different from your current password." });

            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
                return Unauthorized(new { message = "Your session is invalid. Please sign in again." });

            var user = _context.Users.FirstOrDefault(account => account.User_ID == userId);
            if (user == null)
                return Unauthorized(new { message = "Your account could not be found. Please sign in again." });
            if (!_passwords.Verify(user.PasswordHash, request.CurrentPassword))
                return BadRequest(new { message = "Your current password is incorrect." });

            user.PasswordHash = _passwords.Hash(request.NewPassword);
            _context.SaveChanges();
            return Ok(new { message = "Your password was changed successfully." });
        }
    }
}