using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace APINIMIRHEMAA.Services
{
    // Provides reusable application operations for jwt service workflows.
    public class JwtService
    {
        // Injected or cached configuration dependency/state used by this class.
        private readonly IConfiguration _configuration;

        // Initializes a new jwt service instance.
        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Generates the token.
        public string GenerateToken(
            int userId,
            string username,
            string role,
            string department,
            int departmentId,
            string employeeName,
            string sessionId)
        {
            var claims = new List<Claim>
            {
                new Claim(
                    ClaimTypes.NameIdentifier,
                    userId.ToString()
                ),

                new Claim(
                    ClaimTypes.Name,
                    username
                ),

                new Claim(
                    "EmployeeName",
                    employeeName
                ),

                new Claim(
                    ClaimTypes.Role,
                    role
                ),

                new Claim(
                    "Department",
                    department
                ),

                new Claim(
                    "DepartmentId",
                    departmentId.ToString()
                ),

                new Claim(
                    UserSessionService.SessionIdClaimType,
                    sessionId
                )
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _configuration["Jwt:Key"]!
                )
            );

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    double.Parse(
                        _configuration["Jwt:ExpirationMinutes"]!
                    )
                ),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }
    }
}
