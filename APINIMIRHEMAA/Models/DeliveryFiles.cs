using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the delivery files record stored or used by the application.
    public class DeliveryFiles : ICreatedByRecord
    {
        // Stores created by on this delivery files.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related file record.
        public int File_ID { get; set; }
        // Identifies the related delivery record.
        public int Delivery_ID { get; set; }
        // Stores file role on this delivery files.
        public string File_Role { get; set; }
        // Supplies the file name used to identify the related record.
        public string File_Name { get; set; }
        // File or image file extension associated with this delivery files.
        public string File_Extension { get; set; }
        // Stores file data on this delivery files.
        public byte[] File_Data { get; set; }
    }
}
