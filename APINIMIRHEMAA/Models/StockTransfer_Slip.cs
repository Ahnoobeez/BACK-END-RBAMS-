using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class StockTransfer_Slip
    {
        [Key]
        public string Control_Number { get; set; }
        public DateOnly Date { get; set; }
        public string Fromwho { get; set; }
        public string Towho { get; set; }
        public int Quantity { get; set; }
        public string Material_Name { get; set; }
        public string Stock_Width { get; set; }
        public string Stock_Height { get; set; }


    }
}
