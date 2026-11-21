using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    // Represents the client entity record stored or used by the application.
    public class ClientEntity : ICreatedByRecord
    {
        // Stores created by on this client entity.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related client record.
        public int Client_ID { get; set; }
        [Required]
        // Supplies the client name used to identify the related record.
        public string Client_Name { get; set; }
        [Required]
        [MaxLength(16)]
        [RegularExpression(@"^\+63[1-9]\d{8,9}$", ErrorMessage = "Telephone number must start with +63 and contain 9 or 10 more digits.")]
        // Records the Yes/No answer for client telephone.
        public string Client_Telephone { get; set; }
        [MaxLength(200)]
        // Stores contact person on this client entity.
        public string? Contact_Person { get; set; }
        [Required]
        // Stores business style on this client entity.
        public string? Business_Style { get; set; }
        [EmailAddress]
        [MaxLength(254)]
        // Stores client email address on this client entity.
        public string? Client_EmailAddress { get; set; }
        [Required]
        // Stores terms and condition on this client entity.
        public string? TermsAndCondition { get; set; }
        // Stores company address on this client entity.
        public string? Company_Address { get; set; }
        [Required]
        [MaxLength(15)]
        [RegularExpression(@"^\d{3}-\d{3}-\d{3}(-\d{3})?$", ErrorMessage = "TIN must be formatted as XXX-XXX-XXX or XXX-XXX-XXX-XXX.")]
        // Records the Yes/No answer for tin.
        public string? TIN { get; set; }
        // Financial payment terms recorded for this client entity.
        public string? Payment_Terms { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        [Range(typeof(decimal), "0", "99999999999999.9999")]
        // Financial price per square inch recorded for this client entity.
        public decimal Price_Per_Square_Inch { get; set; }
    }
}
