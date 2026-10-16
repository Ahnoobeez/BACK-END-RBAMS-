using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class Installation_Schedule : ICreatedByRecord
    {
        public string? CreatedBy { get; set; }
        [Key]
        public int Installation_ID { get; set; }
        public TimeOnly Time { get; set; }
        public string Remarks { get; set; }
        public string Mall_Hours { get; set; }
        public DateOnly Date { get; set; }
        public int JobOrder_ID { get; set; }

    }
}
