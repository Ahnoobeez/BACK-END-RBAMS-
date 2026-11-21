using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    [Table("Notification")]

    public class Notification
    {
        [Key]

        public int Notification_ID { get; set; }

        [Required]

        public int User_ID { get; set; }

        [Required]
        [MaxLength(200)]

        public string Title { get; set; } = "";

        [Required]

        public string Message { get; set; } = "";

        [Required]
        [MaxLength(50)]

        public string Type { get; set; } = "General";


        public bool IsRead { get; set; }


        public DateTime CreatedAt { get; set; }
    }
}
