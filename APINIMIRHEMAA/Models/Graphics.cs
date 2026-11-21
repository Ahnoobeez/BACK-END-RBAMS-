using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the graphics record stored or used by the application.
    public class Graphics : ICreatedByRecord
    {
        // Stores created by on this graphics.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Stores file id on this graphics.
        public int FileID { get; set; }
        // Supplies the file name used to identify the related record.
        public string FileName { get; set; }
        // File or image file extension associated with this graphics.
        public string FileExtension { get; set; }
        // File or image file data associated with this graphics.
        public byte[] FileData { get; set; }
        // Identifies the related quotation record.
        public int Quotation_ID { get; set; }
        [MaxLength(2048)]
        // Stores link on this graphics.
        public string? Link { get; set; }
    }
}
