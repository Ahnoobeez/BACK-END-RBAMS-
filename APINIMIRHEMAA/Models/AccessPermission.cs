using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class AccessPermission
    {
        [Key]
        public int AccessPermission_ID { get; set; }
        public int? Role_ID { get; set; }
        public int? Department_ID { get; set; }
        public int? User_ID { get; set; }
        [MaxLength(80)]
        public string ServiceKey { get; set; } = "";
        [MaxLength(20)]
        public string ActionKey { get; set; } = "";
        public bool IsAllowed { get; set; }
    }
}
