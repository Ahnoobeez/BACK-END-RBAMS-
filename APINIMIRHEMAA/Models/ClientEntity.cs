using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
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
        public string? TermsAndCondition { get; set; }
        public string? Company_Address { get; set; }
        public string? TIN { get; set; }
        public string? Payment_Terms { get; set; }
    }
}
