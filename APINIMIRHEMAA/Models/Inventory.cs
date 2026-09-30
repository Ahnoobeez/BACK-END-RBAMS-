using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class Inventory
    {
        [Key]
        public int Inventory_ID { get; set; }
        public int Beginning_Balance { get; set; }
        public int Buffer { get; set; }
        public int Purchased { get; set; }
        public int Request { get; set; }
        public int Returned { get; set; }
        public int Balance { get; set; }
        public int Available_Balance { get; set; }
        public string Status { get; set; }
        public int Material_ID { get; set; }

    }
}
