namespace APINIMIRHEMAA.Models
{
    public class EmployeeProfilePhoto
    {
        public int Employee_ID { get; set; }
        public required byte[] PhotoData { get; set; }
        public required string ContentType { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
