using Microsoft.AspNetCore.Http;

namespace APINIMIRHEMAA.DTO
{
    // Defines the profile update request body exchanged through the API.
    public class ProfileUpdateForm
    {
        // Supplies the first name used to identify the related record.
        public string? FirstName { get; set; }
        // Supplies the last name used to identify the related record.
        public string? LastName { get; set; }
        // Carries email for the related API operation. This value is optional.
        public string? Email { get; set; }
        // Supplies the mobile number used to identify the related record.
        public string? MobileNumber { get; set; }
        // File or image remove photo associated with this profile update form.
        public bool RemovePhoto { get; set; }
        // File or image photo associated with this profile update form.
        public IFormFile? Photo { get; set; }
    }
}
