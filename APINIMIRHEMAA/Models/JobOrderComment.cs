using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class JobOrderComment : ICreatedByRecord
    {
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        public int JobOrderComment_ID { get; set; }
        public int JobOrder_ID { get; set; }
        [Required, MaxLength(200)]
        public string Employee_Name { get; set; } = "";
        [Required]
        public string Comment { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}
