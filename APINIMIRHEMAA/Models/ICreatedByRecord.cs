namespace APINIMIRHEMAA.Models
{
    // Represents the i created by record record stored or used by the application.
    public interface ICreatedByRecord
    {
        string? CreatedBy { get; set; }
        int? CreatedByUserId { get; set; }
        int? CreatedByDepartmentId { get; set; }
    }
}
