using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class ServiceInvoiceItem
    {
        [Key]
        public int ServiceInvoiceItem_ID { get; set; }
        public int ServiceInvoice_ID { get; set; }
        public int Quantity { get; set; }
        [Required]
        [MaxLength(50)]
        public string Unit { get; set; } = string.Empty;
        [Required]
        public string Description { get; set; } = string.Empty;
        public decimal Unit_Price { get; set; }
        public decimal Amount { get; set; }
    }
}
