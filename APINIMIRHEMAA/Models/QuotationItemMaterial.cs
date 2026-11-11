using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class QuotationItemMaterial : ICreatedByRecord
    {
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        public int QuotationItemMaterial_ID { get; set; }
        public int QI_ID { get; set; }
        public int Material_ID { get; set; }
        public string Code_Name { get; set; } = "";
        public string Item_Name { get; set; } = "";
        public string Unit { get; set; } = "";
    }
}
