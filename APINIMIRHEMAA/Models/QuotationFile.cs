using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace APINIMIRHEMAA.Models
{
    public class QuotationFile : ICreatedByRecord
    {
        [Key]
        public int QuotationFile_ID { get; set; }

        [Required, MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required, MaxLength(16)]
        public string FileExtension { get; set; } = string.Empty;

        [Required]
        public byte[] FileData { get; set; } = Array.Empty<byte>();

        public int Quotation_ID { get; set; }

        [JsonIgnore]
        public string? CreatedBy { get; set; }

        [JsonIgnore]
        public int? CreatedByUserId { get; set; }

        [JsonIgnore]
        public int? CreatedByDepartmentId { get; set; }
    }
}
