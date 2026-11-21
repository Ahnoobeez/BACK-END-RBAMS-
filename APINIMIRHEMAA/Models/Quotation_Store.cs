using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the quotation store record stored or used by the application.
    public class Quotation_Store : ICreatedByRecord
    {
        // Stores created by on this quotation store.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related qs record.
        public int QS_ID { get; set; }
        // Stores store on this quotation store.
        public string? Store { get; set; }
        // Identifies the related quotation record.
        public int Quotation_ID { get; set; }

    }
}
