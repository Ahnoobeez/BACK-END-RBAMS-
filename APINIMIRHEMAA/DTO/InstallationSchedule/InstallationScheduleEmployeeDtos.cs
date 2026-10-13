using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.DTO.InstallationSchedule
{
    public sealed class AddInstallationScheduleEmployeeRequest
    {
        [Range(1, int.MaxValue)]
        public int Installation_ID { get; set; }

        [Range(1, int.MaxValue)]
        public int Employee_Id { get; set; }
    }

    public sealed class InstallationScheduleEmployeeOptionDto
    {
        public int Employee_Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }

    public sealed class InstallationScheduleEmployeeAssignmentDto
    {
        public int Installation_ID { get; set; }
        public int Employee_Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}
