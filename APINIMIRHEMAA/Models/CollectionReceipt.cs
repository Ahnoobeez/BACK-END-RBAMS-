using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    
    // Represents the collection receipt record stored or used by the application.
    public class CollectionReceipt : ICreatedByRecord
    {
        // Stores created by on this collection receipt.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related collection receipt record.
        public int CollectionReceipt_ID { get; set; }
        // Supplies the file name used to identify the related record.
        public string FileName { get; set; }
        // File or image file extension associated with this collection receipt.
        public string FileExtension { get; set; }
        // File or image file data associated with this collection receipt.
        public byte[] FileData { get; set; }
        // Stores job order id on this collection receipt.
        public int JobOrderID { get; set; }
        // Current workflow status for this collection receipt.
        public string Status { get; set; }

    }
}
