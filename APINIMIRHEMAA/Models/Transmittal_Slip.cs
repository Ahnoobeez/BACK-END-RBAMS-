using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class Transmittal_Slip
    {
        [Key]
        public string Control_Number { get; set; }
        public DateOnly Date { get; set; }
        public string Fromwho { get; set; }
        public string Towho { get; set; }
        public string Machine { get; set; }
        public string Client_Name { get; set; }
        public string Materials_Used { get; set; }
        public string Trans_Width { get; set; }
        public string Trans_Height { get; set; }
        public string Remarks { get; set; }
        public string Signed_By { get; set; }
        public string Submmited_By { get; set; }
        public string Received_By { get; set; }


    }
}
