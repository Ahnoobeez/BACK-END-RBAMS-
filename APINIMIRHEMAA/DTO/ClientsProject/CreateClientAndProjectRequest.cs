using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.DTO.ClientsProject
{
    public sealed class CreateClientAndProjectRequest
    {
        [Required]
        public CreateClientDetailsRequest? Client { get; set; }

        [Required]
        public ProjectRequestDetails? Project { get; set; }
    }

    public sealed class CreateClientDetailsRequest
    {
        [Required]
        public string? Client_Name { get; set; }

        [Required]
        [MaxLength(16)]
        [RegularExpression(@"^\+63[1-9]\d{8,9}$", ErrorMessage = "Telephone number must start with +63 and contain 9 or 10 more digits.")]
        public string? Client_Telephone { get; set; }

        [Required]
        [MaxLength(200)]
        public string? Contact_Person { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(254)]
        public string? Client_EmailAddress { get; set; }

        [Required]
        public string? TermsAndCondition { get; set; }

        public string? Company_Address { get; set; }

        [Required]
        [MaxLength(15)]
        [RegularExpression(@"^\d{3}-\d{3}-\d{3}(-\d{3})?$", ErrorMessage = "TIN must be formatted as XXX-XXX-XXX or XXX-XXX-XXX-XXX.")]
        public string? TIN { get; set; }

        [Required]
        public string? Payment_Terms { get; set; }
    }

    public sealed class ProjectRequestDetails
    {
        [Required]
        public string? Attention { get; set; }

        [Required]
        public string? Business_Style { get; set; }

        [Required]
        public string? Client_Subject { get; set; }

        [Required]
        public string? Representative { get; set; }

        [Required]
        public string? Account_Executive { get; set; }
    }
}
