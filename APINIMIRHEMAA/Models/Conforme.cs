using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class Conforme
    {
        [Key]
        public int FileID { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public Byte[] FIleData { get; set; }
        public int Quotation_ID { get; set; }
        public string Status { get; set; }

    }
}
