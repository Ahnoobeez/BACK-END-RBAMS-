using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class IssuedBy : ICreatedByRecord
    {
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        public int FileID { get; set; }
        public string FileName { get; set; }
        public string FileExtension { get; set; }
        public byte[] FileData{ get; set; }
        public int CollectionReceipt_ID { get; set; }
    }
}
