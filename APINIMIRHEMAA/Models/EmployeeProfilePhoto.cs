namespace APINIMIRHEMAA.Models
{
    // Represents the employee profile photo record stored or used by the application.
    public class EmployeeProfilePhoto
    {
        // Identifies the related employee record.
        public int Employee_ID { get; set; }
        // File or image photo data associated with this employee profile photo.
        public required byte[] PhotoData { get; set; }
        // Stores content type on this employee profile photo.
        public required string ContentType { get; set; }
        // Date associated with updated at for this employee profile photo.
        public DateTime UpdatedAt { get; set; }
    }
}
