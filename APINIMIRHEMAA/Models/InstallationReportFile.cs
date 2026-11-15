using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    [Table("InstallationReportFiles")]
    public sealed class InstallationReportFile : ICreatedByRecord
    {
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByDepartmentId { get; set; }

        [Key]
        public int InstallationReportFile_ID { get; set; }

        [Required]
        public int InstallationReport_ID { get; set; }

        [Required, MaxLength(20)]
        public string File_Role { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string File_Name { get; set; } = string.Empty;

        [Required, MaxLength(10)]
        public string File_Extension { get; set; } = string.Empty;

        [Required]
        public byte[] File_Data { get; set; } = Array.Empty<byte>();
    }
}
