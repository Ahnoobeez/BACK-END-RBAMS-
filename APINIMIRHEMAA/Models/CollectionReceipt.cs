using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    
    public class CollectionReceipt : ICreatedByRecord
    {
        public string? CreatedBy { get; set; }
        [Key]
        public int CollectionReceipt_ID { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public byte[] FileData { get; set; }
        public int JobOrderID { get; set; }
        public string Status { get; set; }

    }
}
