using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    [Table("Installation_Report")]
    // Represents the installation report record stored or used by the application.
    public sealed class InstallationReport : ICreatedByRecord
    {
        // Stores created by on this installation report.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Primary key for this installation report.
        public int InstallationReport_ID { get; set; }

        [Required]
        // Identifies the related job order record.
        public int JobOrder_ID { get; set; }

        [MaxLength(200)]
        // Supplies the client name used to identify the related record.
        public string? ClientName { get; set; }

        [MaxLength(200)]
        // Supplies the project name used to identify the related record.
        public string? ProjectName { get; set; }

        [MaxLength(500)]
        // Stores location on this installation report.
        public string? Location { get; set; }

        // Records the report date for this operation.
        public DateOnly? ReportDate { get; set; }
        // Records the time started for this operation.
        public TimeOnly? TimeStarted { get; set; }
        // Records the time ended for this operation.
        public TimeOnly? TimeEnded { get; set; }

        [MaxLength(1000)]
        // Stores installers on this installation report.
        public string? Installers { get; set; }

        [MaxLength(3)]
        // Indicates whether the installation was completed on schedule (Yes/No).
        public string? OnTime { get; set; }

        [MaxLength(3)]
        // Indicates whether uniform and identification requirements were met (Yes/No).
        public string? UniformAndId { get; set; }

        [MaxLength(3)]
        // Stores graphics properly installed on this installation report.
        public string? GraphicsProperlyInstalled { get; set; }

        [MaxLength(3)]
        // Stores problems occurred on this installation report.
        public string? ProblemsOccurred { get; set; }

        [MaxLength(2000)]
        // Free-text problems note that records context or explanations for this installation report.
        public string? ProblemsNote { get; set; }

        [MaxLength(3)]
        // Stores area clean on this installation report.
        public string? AreaClean { get; set; }

        [MaxLength(3)]
        // Stores installers mannered on this installation report.
        public string? InstallersMannered { get; set; }

        [MaxLength(2000)]
        // Free-text client remarks that records context or explanations for this installation report.
        public string? ClientRemarks { get; set; }

        [MaxLength(3)]
        // Stores obstruction on this installation report.
        public string? Obstruction { get; set; }

        [MaxLength(2000)]
        // Free-text obstruction details that records context or explanations for this installation report.
        public string? ObstructionDetails { get; set; }

        [MaxLength(3)]
        // Stores graphics damaged on this installation report.
        public string? GraphicsDamaged { get; set; }

        [MaxLength(2000)]
        // Free-text damage details that records context or explanations for this installation report.
        public string? DamageDetails { get; set; }

        [MaxLength(2000)]
        // Free-text installer remarks that records context or explanations for this installation report.
        public string? InstallerRemarks { get; set; }

        [MaxLength(200)]
        // File or image client signature associated with this installation report.
        public string? ClientSignature { get; set; }

        [MaxLength(200)]
        // Stores prepared by on this installation report.
        public string? PreparedBy { get; set; }

        [MaxLength(200)]
        // Stores approved by on this installation report.
        public string? ApprovedBy { get; set; }

        [NotMapped]
        // File or image signature files associated with this installation report.
        public List<InstallationReportFile> SignatureFiles { get; set; } = new();
    }
}
