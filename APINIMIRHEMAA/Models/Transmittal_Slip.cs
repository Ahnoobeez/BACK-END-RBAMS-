using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    // Represents the transmittal slip record stored or used by the application.
    public class Transmittal_Slip : ICreatedByRecord
    {
        // Stores created by on this transmittal slip.
        public string? CreatedBy { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
        [Key]
        // Supplies the control number used to identify the related record.
        public int Control_Number { get; set; }
        // Records the date for this operation.
        public DateOnly Date { get; set; }
        // Stores fromwho on this transmittal slip.
        public string Fromwho { get; set; }
        // Stores towho on this transmittal slip.
        public string Towho { get; set; }
        // Stores machine on this transmittal slip.
        public string Machine { get; set; }
        // Supplies the client name used to identify the related record.
        public string Client_Name { get; set; }
        // Stores materials used on this transmittal slip.
        public string Materials_Used { get; set; }
        // Stores trans width on this transmittal slip.
        public string Trans_Width { get; set; }
        // Stores trans height on this transmittal slip.
        public string Trans_Height { get; set; }
        // Free-text remarks that records context or explanations for this transmittal slip.
        public string Remarks { get; set; }
        // Stores signed by on this transmittal slip.
        public string Signed_By { get; set; }
        // Stores submmited by on this transmittal slip.
        public string Submmited_By { get; set; }
        // Stores received by on this transmittal slip.
        public string Received_By { get; set; }


    }
}
