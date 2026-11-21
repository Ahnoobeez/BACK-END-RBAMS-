using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the service invoice file record stored or used by the application.
    public class ServiceInvoiceFile : ICreatedByRecord
    {
        [Key]
        // Identifies the related service invoice file record.
        public int ServiceInvoiceFile_ID { get; set; }
        [Required]
        [MaxLength(255)]
        // Supplies the file name used to identify the related record.
        public string FileName { get; set; } = string.Empty;
        [Required]
        [MaxLength(16)]
        // File or image file extension associated with this service invoice file.
        public string FileExtension { get; set; } = string.Empty;
        [Required]
        // File or image file data associated with this service invoice file.
        public byte[] FileData { get; set; } = Array.Empty<byte>();
        // Stores job order id on this service invoice file.
        public int JobOrderID { get; set; }
        [Required]
        [MaxLength(50)]
        // Current workflow status for this service invoice file.
        public string Status { get; set; } = "Pending";
        // Stores created by on this service invoice file.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
    }
}
