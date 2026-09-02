using System.ComponentModel.DataAnnotations;

namespace Mirhema.Models
{
    public class ClientEntity
    {
        [Key]
        public int Client_ID { get; set; }
        [Required]
        public string Client_Name { get; set; }
        [Required]
        public int Client_Telephone { get; set; }
        [Required]
        public string Client_Address { get; set; }
        [Required]
        public string TIN { get; set; }
        public string Payment_Terms { get; set; }
    }
}
