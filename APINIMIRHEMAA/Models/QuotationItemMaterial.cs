using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the quotation item material record stored or used by the application.
    public class QuotationItemMaterial : ICreatedByRecord
    {
        // Stores created by on this quotation item material.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related quotation item material record.
        public int QuotationItemMaterial_ID { get; set; }
        // Identifies the related qi record.
        public int QI_ID { get; set; }
        // Identifies the related material record.
        public int Material_ID { get; set; }
        // Supplies the code name used to identify the related record.
        public string Code_Name { get; set; } = "";
        // Supplies the item name used to identify the related record.
        public string Item_Name { get; set; } = "";
        // Stores unit on this quotation item material.
        public string Unit { get; set; } = "";
    }
}
