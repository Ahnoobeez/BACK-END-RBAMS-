using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class DeliveryFiles : ICreatedByRecord
    {
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        public int File_ID { get; set; }
        public int Delivery_ID { get; set; }
        public string File_Role { get; set; }
        public string File_Name { get; set; }
        public string File_Extension { get; set; }
        public byte[] File_Data { get; set; }
    }
}
