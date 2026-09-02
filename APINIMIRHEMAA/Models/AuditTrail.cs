using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class AuditTrail
    {
        [Key]
        public int AuditTrail_ID { get; set; }
        public string Name { get; set; }
        public DateOnly Date { get; set; }
        public TimeOnly Time { get; set; }
        public string Action { get; set; }
        public string Department { get; set; }
    }
}
