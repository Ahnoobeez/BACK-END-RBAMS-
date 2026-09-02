using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class MaterialRequesition_Slip
    {
        [Key]
        public int Control_Number { get; set; }
        public DateOnly Date { get; set; }
        public string FromWho { get; set; }
        public string ToWho { get; set; }
        public string Material_Name { get; set; }
        public string Project_Name { get; set; }
        public string Supplier { get; set; }
        public int Quantity { get; set; }
        public int Unit_Cost { get; set; }
        public int Total_Cost { get; set; }
        public string Noted_By { get; set; }
        public string Approved_By { get; set; }
        public string Submmited_By { get; set; }
        public string Received_By { get; set; }

    }
}
