using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class JobOrder
    {
        [Key]
        public int JobOrder_ID { get; set; }
        public int Conforme_FileID { get; set; }
        public string? Title { get; set; }
        public string?  Store { get; set; }
        public string? Requirements { get; set; }
        public string? Description { get; set; }
        public int? Quantity { get; set; }
        public string? Width { get; set; }
        public string? Length { get; set; }
        public string? Artist_Initial { get; set; }
        public string? Production_Initial { get; set; }
        public string? Remarks { get; set; }
        public DateOnly? Installation_Date { get; set; }
        public DateOnly? Target_Delivery { get; set; }
        public DateOnly? Date_Delivered { get; set; }
        public bool? Tiling { get; set; }
        public bool? Eyelet { get; set; }
        public bool? Bleeding { get; set; }
        public string? Status { get; set; }


    }
}
