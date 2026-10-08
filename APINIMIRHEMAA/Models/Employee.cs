using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class Employee
    {
        [Key]
        public int Employee_Id { get; set; }
        public string? LastName { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? status { get; set; }
        public int Department_ID { get; set; }
        public int Role_ID { get; set; }
        public DateOnly Birtday { get; set; }
        public int ContactNumber { get; set; }
        public string? SSS_Number { get; set; }
        public string? Philhealth_Number { get; set; }
        public string? Pagibig_Number { get; set; }
        public DateOnly Date_Started { get; set; }
        public DateOnly Date_Resigned { get; set; }
    }
}
