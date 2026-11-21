using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the employee record stored or used by the application.
    public class Employee
    {
        [Key]
        // Identifies the related employee record.
        public int Employee_Id { get; set; }
        // Supplies the last name used to identify the related record.
        public string? LastName { get; set; }
        // Supplies the first name used to identify the related record.
        public string? FirstName { get; set; }
        // Supplies the middle name used to identify the related record.
        public string? MiddleName { get; set; }
        // Current workflow status for this employee.
        public string? status { get; set; }
        // Identifies the related department record.
        public int? Department_ID { get; set; }
        // Identifies the related role record.
        public int? Role_ID { get; set; }
        // Stores birthday on this employee.
        public DateOnly? Birthday { get; set; }
        // Supplies the contact number used to identify the related record.
        public string? ContactNumber { get; set; }
        // Supplies the sss number used to identify the related record.
        public string? SSS_Number { get; set; }
        // Supplies the philhealth number used to identify the related record.
        public string? Philhealth_Number { get; set; }
        // Supplies the pagibig number used to identify the related record.
        public string? Pagibig_Number { get; set; }
        // Records the date started for this operation.
        public DateOnly? Date_Started { get; set; }
        // Records the date resigned for this operation.
        public DateOnly? Date_Resigned { get; set; }
        // Indicates whether archived applies to this employee.
        public bool IsArchived { get; set; }
    }
}
