namespace APINIMIRHEMAA.Models
{
    public class UserProfilePhoto
    {
        public int User_ID { get; set; }
        public required byte[] PhotoData { get; set; }
        public required string ContentType { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
