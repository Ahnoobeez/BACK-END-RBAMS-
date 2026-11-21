using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.DTO.InstallationSchedule
{
    // Defines the add installation schedule employee request body exchanged through the API.
    public sealed class AddInstallationScheduleEmployeeRequest
    {
        [Range(1, int.MaxValue)]
        // Identifies the related installation record.
        public int Installation_ID { get; set; }

        [Range(1, int.MaxValue)]
        // Identifies the related employee record.
        public int Employee_Id { get; set; }
    }

    // Defines the installation schedule employee option response or transfer shape exchanged through the API.
    public sealed class InstallationScheduleEmployeeOptionDto
    {
        // Identifies the related employee record.
        public int Employee_Id { get; set; }
        // Supplies the first name used to identify the related record.
        public string? FirstName { get; set; }
        // Supplies the last name used to identify the related record.
        public string? LastName { get; set; }
    }

    // Defines the installation schedule employee assignment response or transfer shape exchanged through the API.
    public sealed class InstallationScheduleEmployeeAssignmentDto
    {
        // Identifies the related installation record.
        public int Installation_ID { get; set; }
        // Identifies the related employee record.
        public int Employee_Id { get; set; }
        // Supplies the first name used to identify the related record.
        public string? FirstName { get; set; }
        // Supplies the last name used to identify the related record.
        public string? LastName { get; set; }
    }
}
