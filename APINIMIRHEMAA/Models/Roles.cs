using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class Roles
    {
        [Key]
        public int Role_ID { get; set; }
        public string Role_Name { get; set; }


    }
}
