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
                .FirstOrDefaultAsync(u => u.Email == request.Email);
            var identifier = request.Username?.Trim() ?? "";
                });
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == identifier || u.Email == identifier);
                    request.Password,
            // Same message for every failure so attackers can't tell which part was wrong
            if (user == null || !user.IsActive ||
                !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))

            // 4. Password is incorrect
            if (!passwordValid)
            {
            var role = await _context.Roles
            }

            // 5. Get the user's role
            var role = await _Localdb.Roles
            if (role == null || department == null)
                return Unauthorized(new { message = "Account is not fully configured." });

            var token = _jwtService.GenerateToken(
                user.User_ID, user.Username, role.Role_Name, department.Department_Name);

            var modules = ModuleAccess.GetModules(role.Role_Name, department.Department_Name);

            // 6. Get the user's department
            var department = await _Localdb.Departments
                .FirstOrDefaultAsync(d => d.Department_ID == user.Department_ID);

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