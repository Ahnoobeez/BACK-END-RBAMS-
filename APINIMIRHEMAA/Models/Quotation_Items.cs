using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class Quotation_Items
    {
        [Key]
        public int QI_ID { get; set; }
        public string? Requirements { get; set; }
        public string? Description { get; set; }
        public decimal? Width { get; set; }
        public decimal? Height { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? Unit_Price { get; set; }
        public decimal? Total_Amount { get; set; }
        public int QS_ID { get; set; }
    }
}
