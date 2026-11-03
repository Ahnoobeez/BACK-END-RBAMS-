using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class ServiceInvoiceFile : ICreatedByRecord
    {
        [Key]
        public int ServiceInvoiceFile_ID { get; set; }
        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;
        [Required]
        [MaxLength(16)]
        public string FileExtension { get; set; } = string.Empty;
        [Required]
        public byte[] FileData { get; set; } = Array.Empty<byte>();
        public int JobOrderID { get; set; }
        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";
        public string? CreatedBy { get; set; }
    }
}
