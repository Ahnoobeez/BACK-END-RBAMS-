using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class QuotationItemMaterial
    {
        [Key]
        public int QuotationItemMaterial_ID { get; set; }
        public int QI_ID { get; set; }
        public int Material_ID { get; set; }
        public string Code_Name { get; set; } = "";
        public string Item_Name { get; set; } = "";
        public string Unit { get; set; } = "";
    }
}
