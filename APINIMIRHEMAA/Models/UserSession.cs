using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the user session record stored or used by the application.
    public sealed class UserSession
    {
        [Key]
        [MaxLength(64)]
        // Identifies the related session record.
        public string Session_ID { get; set; } = "";

        // Identifies the related user record.
        public int User_ID { get; set; }
        // Time value for last seen utc, using the UTC/time-zone convention indicated by its name.
        public DateTime LastSeenUtc { get; set; }
    }
}
