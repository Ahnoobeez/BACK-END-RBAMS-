using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    [Table("InstallationReportFiles")]
    // Represents the installation report file record stored or used by the application.
    public sealed class InstallationReportFile : ICreatedByRecord
    {
        // Stores created by on this installation report file.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }

        [Key]
        // Identifies the related installation report file record.
        public int InstallationReportFile_ID { get; set; }

        [Required]
        // Identifies the related installation report record.
        public int InstallationReport_ID { get; set; }

        [Required, MaxLength(20)]
        // Stores file role on this installation report file.
        public string File_Role { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        // Supplies the file name used to identify the related record.
        public string File_Name { get; set; } = string.Empty;

        [Required, MaxLength(10)]
        // File or image file extension associated with this installation report file.
        public string File_Extension { get; set; } = string.Empty;

        [Required]
        // Stores file data on this installation report file.
        public byte[] File_Data { get; set; } = Array.Empty<byte>();
    }
}
