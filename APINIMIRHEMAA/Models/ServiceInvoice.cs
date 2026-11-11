using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    public class ServiceInvoice : ICreatedByRecord
    {
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        public int ServiceInvoice_ID { get; set; }
        public int? JobOrder_ID { get; set; }
        public DateOnly Invoice_Date { get; set; }
        public int? Quantity { get; set; }
        public string? Unit { get; set; }
        public string? Description { get; set; }
        public decimal? Unit_Price { get; set; }
        public decimal? Amount { get; set; }
        public string? Status { get; set; }

        [NotMapped]
        public string Invoice_Number => $"SI-{ServiceInvoice_ID:D6}";
    }
}
