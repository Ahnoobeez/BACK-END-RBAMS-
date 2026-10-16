using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class UserSignature
    {
        [Key]
        public int User_ID { get; set; }
        public required byte[] SignatureData { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
