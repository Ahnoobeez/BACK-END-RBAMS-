using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the purchase order record stored or used by the application.
    public class PurchaseOrder : ICreatedByRecord
    {
        // Stores created by on this purchase order.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Stores file id on this purchase order.
        public int FileID { get; set; }
        // Supplies the file name used to identify the related record.
        public string FileName { get; set; }
        // File or image file extension associated with this purchase order.
        public string FileExtension { get; set; }
        // File or image file data associated with this purchase order.
        public byte[] FileData { get; set; }
        // Stores conforme file id on this purchase order.
        public int Conforme_FileID { get; set; }
        // Current workflow status for this purchase order.
        public string Status { get; set; }
    }
}
