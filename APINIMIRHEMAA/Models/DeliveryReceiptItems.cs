using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class DeliveryReceiptItems : ICreatedByRecord
    {
        public string? CreatedBy { get; set; }
        [Key]
        public int DeliveryReceiptItems_ID { get; set; }
        public string? Requirement { get; set; }
        public string? Description { get; set; }
        public string? Store { get; set; }
        public int? Quantity { get; set; }
        public int Delivery_ID { get; set; }

    }
}
