using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    [Table("Notification")]
    // Represents the notification record stored or used by the application.
    public class Notification
    {
        [Key]
        // Identifies the related notification record.
        public int Notification_ID { get; set; }

        [Required]
        // Identifies the related user record.
        public int User_ID { get; set; }

        [Required]
        [MaxLength(200)]
        // Supplies the title used to identify the related record.
        public string Title { get; set; } = "";

        [Required]
        // Stores message on this notification.
        public string Message { get; set; } = "";

        [Required]
        [MaxLength(50)]
        // Stores type on this notification.
        public string Type { get; set; } = "General";

        // Indicates whether read applies to this notification.
        public bool IsRead { get; set; }

        // Stores created at on this notification.
        public DateTime CreatedAt { get; set; }
    }
}
