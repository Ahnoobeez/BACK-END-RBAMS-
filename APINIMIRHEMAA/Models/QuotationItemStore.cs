using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the quotation item store record stored or used by the application.
    public class QuotationItemStore : ICreatedByRecord
    {
        // Stores created by on this quotation item store.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        // Identifies the related qi record.
        public int QI_ID { get; set; }
        // Identifies the related qs record.
        public int QS_ID { get; set; }
    }
}
