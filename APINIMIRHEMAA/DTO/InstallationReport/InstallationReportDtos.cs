using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.DTO.InstallationReport
{
    // Defines the installation report job order response or transfer shape exchanged through the API.
    public sealed class InstallationReportJobOrderOption
    {
        // Identifies the related job order record.
        public int JobOrderId { get; set; }
        // Supplies the client name used to identify the related record.
        public string? ClientName { get; set; }
        // Supplies the project name used to identify the related record.
        public string? ProjectName { get; set; }
        // Carries location for the related API operation. This value is optional.
        public string? Location { get; set; }
        // Records the installation date for this operation.
        public DateOnly? InstallationDate { get; set; }
    }

    // Defines the create installation report request body exchanged through the API.
    public sealed class CreateInstallationReportRequest
    {
        [Range(1, int.MaxValue)]
        // Identifies the related job order record.
        public int JobOrderId { get; set; }

        [MaxLength(200)]
        // Supplies the client name used to identify the related record.
        public string? ClientName { get; set; }

        [MaxLength(200)]
        // Supplies the project name used to identify the related record.
        public string? ProjectName { get; set; }

        [MaxLength(500)]
        // Carries location for the related API operation. This value is optional. The maximum-length annotation limits accepted text length.
        public string? Location { get; set; }

        // Records the report date for this operation.
        public DateOnly? ReportDate { get; set; }
        // Records the time started for this operation.
        public TimeOnly? TimeStarted { get; set; }
        // Records the time ended for this operation.
        public TimeOnly? TimeEnded { get; set; }

        [MaxLength(1000)]
        // Carries installers for the related API operation. This value is optional. The maximum-length annotation limits accepted text length.
        public string? Installers { get; set; }

        [RegularExpression("^(Yes|No)$")]
        // Indicates whether the installation was completed on schedule (Yes/No).
        public string? OnTime { get; set; }

        [RegularExpression("^(Yes|No)$")]
        // Indicates whether uniform and identification requirements were met (Yes/No).
        public string? UniformAndId { get; set; }

        [RegularExpression("^(Yes|No)$")]
        // Indicates whether graphics were installed properly (Yes/No).
        public string? GraphicsProperlyInstalled { get; set; }

        [RegularExpression("^(Yes|No)$")]
        // Indicates whether problems occurred (Yes/No).
        public string? ProblemsOccurred { get; set; }

        [MaxLength(2000)]
        // Free-text problems note that records context or explanations for this create installation report request.
        public string? ProblemsNote { get; set; }

        [RegularExpression("^(Yes|No)$")]
        // Indicates whether the work area was left clean (Yes/No).
        public string? AreaClean { get; set; }

        [RegularExpression("^(Yes|No)$")]
        // Indicates whether installers behaved professionally (Yes/No).
        public string? InstallersMannered { get; set; }

        [MaxLength(2000)]
        // Free-text client remarks that records context or explanations for this create installation report request.
        public string? ClientRemarks { get; set; }

        [RegularExpression("^(Yes|No)$")]
        // Indicates whether an obstruction was present (Yes/No).
        public string? Obstruction { get; set; }

        [MaxLength(2000)]
        // Free-text obstruction details that records context or explanations for this create installation report request.
        public string? ObstructionDetails { get; set; }

        [RegularExpression("^(Yes|No)$")]
        // Indicates whether graphics were damaged (Yes/No).
        public string? GraphicsDamaged { get; set; }

        [MaxLength(2000)]
        // Free-text damage details that records context or explanations for this create installation report request.
        public string? DamageDetails { get; set; }

        [MaxLength(2000)]
        // Free-text installer remarks that records context or explanations for this create installation report request.
        public string? InstallerRemarks { get; set; }

        [MaxLength(200)]
        // File or image client signature associated with this create installation report request.
        public string? ClientSignature { get; set; }

        [MaxLength(200)]
        // Carries prepared by for the related API operation. This value is optional. The maximum-length annotation limits accepted text length.
        public string? PreparedBy { get; set; }

        // File or image signatures associated with this create installation report request.
        public List<CreateInstallationReportSignatureRequest>? Signatures { get; set; }
    }

    // Defines the create installation report signature request body exchanged through the API.
    public sealed class CreateInstallationReportSignatureRequest
    {
        [Required, RegularExpression("^(PreparedBy|ClientSignature)$")]
        // Records the Yes/No answer for file role.
        public string FileRole { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        // Supplies the file name used to identify the related record.
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(10)]
        // File or image file extension associated with this create installation report signature request.
        public string FileExtension { get; set; } = string.Empty;

        [Required]
        // File or image file data associated with this create installation report signature request.
        public string FileData { get; set; } = string.Empty;
    }
}
