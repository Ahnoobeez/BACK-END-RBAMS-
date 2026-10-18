using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.DTO.JobOrders
{
    public class CreateJobOrderRequest
    {
        [Range(1, int.MaxValue)]
        public int Conforme_FileID { get; set; }
        public string? Title { get; set; }
        public DateOnly? Installation_Date { get; set; }
        public DateOnly? Target_Delivery { get; set; }
        public DateOnly? Date_Delivered { get; set; }
        public bool? Tiling { get; set; }
        public bool? Eyelet { get; set; }
        public bool? Bleeding { get; set; }
        public string? Status { get; set; }
        [MaxLength(4000)]
        public string? SpecialInstruction { get; set; }
    }

    public class AddJobOrderCommentRequest
    {
        [Range(1, int.MaxValue)]
        public int JobOrder_ID { get; set; }
        [Required, MaxLength(4000)]
        public string Comment { get; set; } = "";
    }
}
