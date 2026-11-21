namespace APINIMIRHEMAA.Models
{
    // Represents the user profile photo record stored or used by the application.
    public class UserProfilePhoto
    {
        // Identifies the related user record.
        public int User_ID { get; set; }
        // File or image photo data associated with this user profile photo.
        public required byte[] PhotoData { get; set; }
        // Stores content type on this user profile photo.
        public required string ContentType { get; set; }
        // Date associated with updated at for this user profile photo.
        public DateTime UpdatedAt { get; set; }
    }
}
