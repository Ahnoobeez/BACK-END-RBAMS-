using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class Graphics : ICreatedByRecord
    {
        public string? CreatedBy { get; set; }
        [Key]
        public int FileID { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public byte[] FileData { get; set; }
        public int Quotation_ID { get; set; }
    }
}
