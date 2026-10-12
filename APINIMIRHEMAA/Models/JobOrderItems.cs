using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class JobOrderItems
    {
        [Key]
        public int JobOrderItems_ID { get; set; }
        public int JobOrder_ID { get; set; }
        public string? Store { get; set; }
        public string? Requirements { get; set; }
        public string? Description { get; set; }
        public int? Quantity { get; set; }
        public string? Width { get; set; }
        public string? Length { get; set; }
        public string? Artist_Initial { get; set; }
        public string? Production_Initial { get; set; }
        public string? Remarks { get; set; }

    }
}
