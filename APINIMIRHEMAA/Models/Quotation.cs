using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class Quotation
    {
        [Key]
        public int Quotation_ID { get; set; }
        public int Project_ID { get; set; }
        public string? Title { get; set; }
        public int Sub_Total { get; set; }
        public int? Less_Discount { get; set; }
        public int? Ingress_and_Engress { get; set; }
        public int Overall_Total { get; set; }
        public string? Status { get; set; }
        public string? Comment { get; set; }
        public string? Terms_Condition { get; set; }
        public DateOnly Start_Date { get; set; }
        public TimeOnly Time { get; set; }
        public DateOnly End_Date { get; set; }

    }
}
