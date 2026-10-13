using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.DTO.InstallationReport
{
    public sealed class InstallationReportJobOrderOption
    {
        public int JobOrderId { get; set; }
        public string? ClientName { get; set; }
        public string? ProjectName { get; set; }
        public string? Location { get; set; }
        public DateOnly? InstallationDate { get; set; }
    }

    public sealed class CreateInstallationReportRequest
    {
        [Range(1, int.MaxValue)]
        public int JobOrderId { get; set; }

        [MaxLength(200)]
        public string? ClientName { get; set; }

        [MaxLength(200)]
        public string? ProjectName { get; set; }

        [MaxLength(500)]
        public string? Location { get; set; }

        public DateOnly? ReportDate { get; set; }
        public TimeOnly? TimeStarted { get; set; }
        public TimeOnly? TimeEnded { get; set; }

        [MaxLength(1000)]
        public string? Installers { get; set; }

        [RegularExpression("^(Yes|No)$")]
        public string? OnTime { get; set; }

        [RegularExpression("^(Yes|No)$")]
        public string? UniformAndId { get; set; }

        [RegularExpression("^(Yes|No)$")]
        public string? GraphicsProperlyInstalled { get; set; }

        [RegularExpression("^(Yes|No)$")]
        public string? ProblemsOccurred { get; set; }

        [MaxLength(2000)]
        public string? ProblemsNote { get; set; }

        [RegularExpression("^(Yes|No)$")]
        public string? AreaClean { get; set; }

        [RegularExpression("^(Yes|No)$")]
        public string? InstallersMannered { get; set; }

        [MaxLength(2000)]
        public string? ClientRemarks { get; set; }

        [RegularExpression("^(Yes|No)$")]
        public string? Obstruction { get; set; }

        [MaxLength(2000)]
        public string? ObstructionDetails { get; set; }

        [RegularExpression("^(Yes|No)$")]
        public string? GraphicsDamaged { get; set; }

        [MaxLength(2000)]
        public string? DamageDetails { get; set; }

        [MaxLength(2000)]
        public string? InstallerRemarks { get; set; }

        [MaxLength(200)]
        public string? ClientSignature { get; set; }

        [MaxLength(200)]
        public string? PreparedBy { get; set; }

        [MaxLength(200)]
        public string? ApprovedBy { get; set; }
    }
}
