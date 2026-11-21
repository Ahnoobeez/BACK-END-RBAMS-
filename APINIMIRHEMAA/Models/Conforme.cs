using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the conforme record stored or used by the application.
    public class Conforme : ICreatedByRecord
    {
        // Stores created by on this conforme.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Stores file id on this conforme.
        public int FileID { get; set; }
        // Supplies the file name used to identify the related record.
        public string FileName { get; set; }
        // File or image file extension associated with this conforme.
        public string FileExtension { get; set; }
        // File or image f ile data associated with this conforme.
        public Byte[] FIleData { get; set; }
        // Identifies the related quotation record.
        public int Quotation_ID { get; set; }
        // Current workflow status for this conforme.
        public string Status { get; set; }

    }
}
