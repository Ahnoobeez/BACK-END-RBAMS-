using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    [Table("Installation_Schedule_Employee")]
    // Represents the installation schedule employee record stored or used by the application.
    public class InstallationScheduleEmployee : ICreatedByRecord
    {
        // Stores created by on this installation schedule employee.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Identifies the related installation schedule employee record.
        public int Installation_Schedule_Employee_ID { get; set; }

        // Identifies the related installation record.
        public int Installation_ID { get; set; }

        // Identifies the related employee record.
        public int Employee_Id { get; set; }
    }
}
