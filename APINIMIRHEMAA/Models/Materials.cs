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
        public int Beginning_Balance { get; set; }
        public int Buffer { get; set; }
        public int Purchased { get; set; }
        public int Request { get; set; }
        public int Returned { get; set; }
        public int Balance { get; set; }
        public int Available_Balance { get; set; }
        public string Item_Description { get; set; }
        public DateOnly Date { get; set; }


    }
}
