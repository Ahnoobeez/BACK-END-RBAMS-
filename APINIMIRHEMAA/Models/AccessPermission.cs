using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the access permission record stored or used by the application.
    public class AccessPermission
    {
        [Key]
        // Identifies the related access permission record.
        public int AccessPermission_ID { get; set; }
        // Identifies the related role record.
        public int? Role_ID { get; set; }
        // Identifies the related department record.
        public int? Department_ID { get; set; }
        // Identifies the related user record.
        public int? User_ID { get; set; }
        [MaxLength(80)]
        // Stores service key on this access permission.
        public string ServiceKey { get; set; } = "";
        [MaxLength(20)]
        // Stores action key on this access permission.
        public string ActionKey { get; set; } = "";
        // Indicates whether allowed applies to this access permission.
        public bool IsAllowed { get; set; }
    }
}
