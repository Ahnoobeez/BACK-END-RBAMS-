using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APINIMIRHEMAA.Models
{
    public class Quotation_Items
    {
        [Key]
        public int QI_ID { get; set; }
        public string? Requirements { get; set; }
        public string? Description { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal? Width { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal? Height { get; set; }
        public string? Measurement_Unit { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal? Quantity { get; set; }
        [Column(TypeName = "decimal(18,4)")]
        public decimal? Price_Per_Square_Inch { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Unit_Price { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal? Total_Amount { get; set; }
        public int QS_ID { get; set; }
    }
}
