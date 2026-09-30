using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class Materials
    {
        [Key]
        public int Material_ID { get; set; }
        public string Code_Name { get; set; }
        public string Item_Name { get; set; }
        public string Category { get; set; }
        public string Unit { get; set; }
    }
}
