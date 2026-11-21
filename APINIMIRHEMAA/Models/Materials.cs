using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the materials record stored or used by the application.
    public class Materials : ICreatedByRecord
    {
        // Stores created by on this materials.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related material record.
        public int Material_ID { get; set; }
        // Supplies the code name used to identify the related record.
        public string Code_Name { get; set; }
        // Supplies the item name used to identify the related record.
        public string Item_Name { get; set; }
        // Stores category on this materials.
        public string Category { get; set; }
        // Stores unit on this materials.
        public string Unit { get; set; }
    }
}
