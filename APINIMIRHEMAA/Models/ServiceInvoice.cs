using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class ServiceInvoice
    {
        [Key]
        public int Quantity { get; set; }
        public int Unit { get; set; }
        public string Description { get; set; }
        public int Unit_Price { get; set; }
        public int Amount { get; set; }
        public string Status { get; set; }
    }
}
