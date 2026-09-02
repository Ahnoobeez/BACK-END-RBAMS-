using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class DeliveryReceipt
    {
        [Key]
        public int Delivery_ID { get; set; }
        public string Title { get; set; }
        public int Quantity { get; set; }
        public string Unit { get; set; }
        public string Description { get; set; }

    }
}
