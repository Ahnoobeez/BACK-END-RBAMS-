using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class Quotation_Store : ICreatedByRecord
    {
        public string? CreatedBy { get; set; }
        [Key]
        public int QS_ID { get; set; }
        public string? Store { get; set; }
        public int Quotation_ID { get; set; }

    }
}
