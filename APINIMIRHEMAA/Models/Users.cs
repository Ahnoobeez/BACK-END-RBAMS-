using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class Users
    {
        [Key]
        public int User_ID { get; set; }
        public int Employee_ID { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Email { get; set; }
        public int Role_ID { get; set; }
        public int Department_ID { get; set; }
        public bool IsActive { get; set; }

    }
}
