using Microsoft.AspNetCore.Http;

namespace APINIMIRHEMAA.DTO
{
    public class ProfileUpdateForm
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? MobileNumber { get; set; }
        public bool RemovePhoto { get; set; }
        public IFormFile? Photo { get; set; }
    }
}
