using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class Quotation
    {
        [Key]
        public int Quotation_ID { get; set; }
        public int Client_ID { get; set; }
        public int Project_ID { get; set; }
        public string Title { get; set; }
        public string Store { get; set; }
        public string Description { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Quantity { get; set; }
        public int Unit_Price { get; set; }
        public int Total_Amount { get; set; }
        public int Sub_Total { get; set; }
        public int Less_Discount { get; set; }
        public int Ingress_and_Engress { get; set; }
        public int Overall_Total { get; set; }
        public bool Status { get; set; }
        public string Comment { get; set; }
        public string Terms_Condition { get; set; }
        public DateOnly Start_Date { get; set; }
        public TimeOnly Time { get; set; }
        public DateOnly End_Date { get; set; }

    }
}
