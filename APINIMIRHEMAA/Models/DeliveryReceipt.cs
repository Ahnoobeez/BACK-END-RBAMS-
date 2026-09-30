using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class DeliveryReceipt
    {
        [Key]
        public int Delivery_ID { get; set; }
        public int? JobOrderID { get; set; }
        public string? Title { get; set; }
        public string? Requirement { get; set; }
        public string? ClientName { get; set; }
        public string? Address { get; set; }
        public string? Terms { get; set; }
        public int? Quantity { get; set; }
        public string? Description { get; set; }
        public DateOnly? date { get; set; }
        public TimeOnly? time { get; set; }
        public string? Status { get; set; }

    }
}
