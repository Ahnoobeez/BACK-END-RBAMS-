using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class JobOrder
    {
        [Key]
        public int JobOrder_ID { get; set; }
        public int Client_ID { get; set; }
        public int Quotation_ID { get; set; }
        public int Project_ID { get; set; }
        public string Title { get; set; }
        public int Quantity { get; set; }
        public string Width { get; set; }
        public string Length { get; set; }
        public string Material_Code { get; set; }
        public string Artist_Initial { get; set; }
        public string Production_Initial { get; set; }
        public string Remarks { get; set; }
        public DateOnly Installation_Date { get; set; }
        public DateOnly Target_Delivery { get; set; }
        public DateOnly Date_Delivered { get; set; }
        public byte[] Tiling { get; set; }
        public byte[] Eyelet { get; set; }
        public byte[] Bleeding { get; set; }


    }
}
