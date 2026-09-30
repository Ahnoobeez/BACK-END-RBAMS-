using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class Departments
    {
        [Key]
        public int Department_ID { get; set; }
        public string Department_Name { get; set; }

    }
}
