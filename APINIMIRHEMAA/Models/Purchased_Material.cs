using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class Purchased_Material : ICreatedByRecord
    {
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        public int Purchased_ID { get; set; }
        public DateOnly Date_Received_Item { get; set; }
        public DateOnly SI_Date { get; set; }
        public int SI_No { get; set; }
        public string Supplier_Name { get; set; }
        public string Remarks { get; set; }
        public int Quantity { get; set; }
        public int Unit_price { get; set; }
        public int Amount { get; set; }
        public int Material_ID { get; set; }
    }
}
