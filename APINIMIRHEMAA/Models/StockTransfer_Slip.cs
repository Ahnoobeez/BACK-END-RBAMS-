using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the stock transfer slip record stored or used by the application.
    public class StockTransfer_Slip : ICreatedByRecord
    {
        // Stores created by on this stock transfer slip.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Supplies the control number used to identify the related record.
        public int Control_Number { get; set; }
        // Records the date for this operation.
        public DateOnly Date { get; set; }
        // Stores fromwho on this stock transfer slip.
        public string Fromwho { get; set; }
        // Stores towho on this stock transfer slip.
        public string Towho { get; set; }
        // Stores quantity on this stock transfer slip.
        public int Quantity { get; set; }
        // Supplies the material name used to identify the related record.
        public string Material_Name { get; set; }
        // Stores stock width on this stock transfer slip.
        public string Stock_Width { get; set; }
        // Stores stock height on this stock transfer slip.
        public string Stock_Height { get; set; }


    }
}
