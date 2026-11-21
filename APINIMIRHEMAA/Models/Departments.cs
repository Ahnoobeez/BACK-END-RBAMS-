using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the departments record stored or used by the application.
    public class Departments
    {
        [Key]
        // Identifies the related department record.
        public int Department_ID { get; set; }
        // Supplies the department name used to identify the related record.
        public string Department_Name { get; set; }

    }
}
