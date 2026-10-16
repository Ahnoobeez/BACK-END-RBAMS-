using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    [Table("Installation_Schedule_Employee")]
    public class InstallationScheduleEmployee : ICreatedByRecord
    {
        public string? CreatedBy { get; set; }
        [Key]
        public int Installation_Schedule_Employee_ID { get; set; }

        public int Installation_ID { get; set; }

        public int Employee_Id { get; set; }
    }
}
