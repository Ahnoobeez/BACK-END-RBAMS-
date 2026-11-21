using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the roles record stored or used by the application.
    public class Roles
    {
        [Key]
        // Identifies the related role record.
        public int Role_ID { get; set; }
        // Supplies the role name used to identify the related record.
        public string Role_Name { get; set; }


    }
}
