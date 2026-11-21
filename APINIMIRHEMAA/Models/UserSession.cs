using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{

    public sealed class UserSession
    {
        [Key]
        [MaxLength(64)]

        public string Session_ID { get; set; } = "";


        public int User_ID { get; set; }

        public DateTime LastSeenUtc { get; set; }
    }
}
