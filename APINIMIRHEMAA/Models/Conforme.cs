using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class Conforme : ICreatedByRecord
    {
        public string? CreatedBy { get; set; }
        [Key]
        public int FileID { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public Byte[] FIleData { get; set; }
        public int Quotation_ID { get; set; }
        public string Status { get; set; }

    }
}
