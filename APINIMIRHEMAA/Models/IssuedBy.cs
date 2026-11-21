using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the issued by record stored or used by the application.
    public class IssuedBy : ICreatedByRecord
    {
        // Stores created by on this issued by.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Stores file id on this issued by.
        public int FileID { get; set; }
        // Supplies the file name used to identify the related record.
        public string FileName { get; set; }
        // File or image file extension associated with this issued by.
        public string FileExtension { get; set; }
        // File or image file data associated with this issued by.
        public byte[] FileData{ get; set; }
        // Identifies the related collection receipt record.
        public int CollectionReceipt_ID { get; set; }
    }
}
