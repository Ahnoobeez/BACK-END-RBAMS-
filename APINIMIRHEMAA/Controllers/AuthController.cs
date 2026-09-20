using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly LocalDbContext _Localdb;

        public AuthController(LocalDbContext Localcontext)
        {
            _Localdb = Localcontext;
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            // 1. Find the user
            var user = await _Localdb.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            // 2. User doesn't exist
            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }

            // 3. Check the password HERE
            bool passwordValid =
                BCrypt.Net.BCrypt.Verify(
                    request.Password,
                    user.PasswordHash
                );

            // 4. Password is incorrect
            if (!passwordValid)
            {
                return Unauthorized(new
                {
                    message = "Invalid email or password."
                });
            }

            // 5. Get the user's role
            var role = await _Localdb.Roles
                .FirstOrDefaultAsync(r => r.Role_ID == user.Role_ID);

            // 6. Get the user's department
            var department = await _Localdb.Departments
                .FirstOrDefaultAsync(d => d.Department_ID == user.Department_ID);

            // 7. Login successful
            return Ok(new
            {
                message = "Login successful",

                user = new
                {
                    userId = user.User_ID,
                    employeeId = user.Employee_ID,
                    username = user.Username,
                    email = user.Email,

                    roleId = user.Role_ID,
                    role = role?.Role_Name,

                    departmentId = user.Department_ID,
                    department = department?.Department_Name
                }
            });
        }
    }
}