using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public sealed class ClientQuotationRequestGraphic
    {
        [Key]
        public int RequestGraphic_ID { get; set; }

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(16)]
        public string FileExtension { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string ContentType { get; set; } = string.Empty;

        [Required]
        public byte[] FileData { get; set; } = Array.Empty<byte>();

        public int Request_ID { get; set; }
    }
}
