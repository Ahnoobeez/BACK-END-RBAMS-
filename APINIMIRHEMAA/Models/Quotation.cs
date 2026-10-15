using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    public class Quotation
    {
        [Key]
        public int Quotation_ID { get; set; }
        public int Project_ID { get; set; }
        public string? Title { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Sub_Total { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Delivery_Charge { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Installation_Charge { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal? VAT { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Overall_Total { get; set; }
        public string? Status { get; set; }
        public string? Comment { get; set; }
        public string? Terms_Condition { get; set; }
        public DateOnly Start_Date { get; set; }
        public TimeOnly Time { get; set; }
        public int? User_ID { get; set; }

    }
}
