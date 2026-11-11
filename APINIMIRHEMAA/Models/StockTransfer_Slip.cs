using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class StockTransfer_Slip : ICreatedByRecord
    {
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        public int Control_Number { get; set; }
        public DateOnly Date { get; set; }
        public string Fromwho { get; set; }
        public string Towho { get; set; }
        public int Quantity { get; set; }
        public string Material_Name { get; set; }
        public string Stock_Width { get; set; }
        public string Stock_Height { get; set; }


    }
}
