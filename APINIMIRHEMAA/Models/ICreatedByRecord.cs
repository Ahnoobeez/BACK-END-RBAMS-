namespace APINIMIRHEMAA.Models
{
    public interface ICreatedByRecord
    {
        string? CreatedBy { get; set; }
        int? CreatedByUserId { get; set; }
        int? CreatedByDepartmentId { get; set; }
    }
}
