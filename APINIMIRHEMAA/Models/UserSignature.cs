using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the user signature record stored or used by the application.
    public class UserSignature
    {
        [Key]
        // Identifies the related user record.
        public int User_ID { get; set; }
        // File or image signature data associated with this user signature.
        public required byte[] SignatureData { get; set; }
        // Date associated with updated at for this user signature.
        public DateTime UpdatedAt { get; set; }
    }
}
