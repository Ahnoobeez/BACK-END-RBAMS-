using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace APINIMIRHEMAA.DTO
{
    public sealed class AnalyzeQuotationDocumentRequest
    {
        [Required, MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(32)]
        public string ContentType { get; set; } = string.Empty;

        [Required, MaxLength(2_800_000)]
        public string FileData { get; set; } = string.Empty;
    }

    public sealed class QuotationDocumentAnalysisDto
    {
        public string? ProjectTitle { get; set; }

        public string? StoreBranch { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal? DeliveryCharge { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal? InstallationCharge { get; set; }

        public List<QuotationDocumentRequirementDto> Requirements { get; set; } = new();

        public List<string> Warnings { get; set; } = new();
    }

    public sealed class QuotationDocumentRequirementDto
    {
        public string? StoreBranch { get; set; }

        public string? Requirement { get; set; }

        public string? Description { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal? Width { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal? Height { get; set; }

        public string? MeasurementUnit { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal? Quantity { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public decimal? UnitPrice { get; set; }
    }
}
