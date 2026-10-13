using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    [Table("Installation_Report")]
    public sealed class InstallationReport
    {
        [Key]
        public int InstallationReport_ID { get; set; }

        [Required]
        public int JobOrder_ID { get; set; }

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

        [MaxLength(3)]
        public string? OnTime { get; set; }

        [MaxLength(3)]
        public string? UniformAndId { get; set; }

        [MaxLength(3)]
        public string? GraphicsProperlyInstalled { get; set; }

        [MaxLength(3)]
        public string? ProblemsOccurred { get; set; }

        [MaxLength(2000)]
        public string? ProblemsNote { get; set; }

        [MaxLength(3)]
        public string? AreaClean { get; set; }

        [MaxLength(3)]
        public string? InstallersMannered { get; set; }

        [MaxLength(2000)]
        public string? ClientRemarks { get; set; }

        [MaxLength(3)]
        public string? Obstruction { get; set; }

        [MaxLength(2000)]
        public string? ObstructionDetails { get; set; }

        [MaxLength(3)]
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
