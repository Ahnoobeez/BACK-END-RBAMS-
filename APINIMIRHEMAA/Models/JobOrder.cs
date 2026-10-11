using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class JobOrder
    {
        [Key]
        public int JobOrder_ID { get; set; }
        public int Conforme_FileID { get; set; }
        public string? Title { get; set; }
        public string? Comment { get; set; }
        public DateOnly? Installation_Date { get; set; }
        public DateOnly? Target_Delivery { get; set; }
        public DateOnly? Date_Delivered { get; set; }
        public bool? Tiling { get; set; }
        public bool? Eyelet { get; set; }
        public bool? Bleeding { get; set; }
        public string? Status { get; set; }


    }
}
