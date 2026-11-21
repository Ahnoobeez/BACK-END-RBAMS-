using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the users record stored or used by the application.
    public class Users
    {
        [Key]
        // Identifies the related user record.
        public int User_ID { get; set; }
        // Identifies the related employee record.
        public int Employee_ID { get; set; }
        // Stores username on this users.
        public string Username { get; set; }
        // Stores password hash on this users.
        public string PasswordHash { get; set; }
        // Stores email on this users.
        public string Email { get; set; }
        // Identifies the related role record.
        public int Role_ID { get; set; }
        // Identifies the related department record.
        public int Department_ID { get; set; }
        // Indicates whether active applies to this users.
        public bool IsActive { get; set; }

    }
}
