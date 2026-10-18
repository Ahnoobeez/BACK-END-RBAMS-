using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class JobOrderComment
    {
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
