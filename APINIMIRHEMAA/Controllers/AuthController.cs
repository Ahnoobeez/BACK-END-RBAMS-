using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO.Login;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly LocalDbContext _context;
        private readonly JwtService _jwtService;

        public AuthController(LocalDbContext context, JwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            // Same message for every failure so attackers can't tell which part was wrong
            if (user == null || !user.IsActive ||
                !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Unauthorized(new { message = "Invalid username or password." });
            }

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
    }
}