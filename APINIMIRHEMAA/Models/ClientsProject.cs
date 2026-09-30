using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    public class ClientsProject
    {
        [Key]
        public int Project_ID { get; set; }
        public int Client_ID { get; set; }
        public string? Attention { get; set; }
        public string? Business_Style { get; set; }
        public string Client_Subject { get; set; }
        public string? Representative { get; set; }
        public string? Contact_Person { get; set; }
        public string? Account_Executive { get; set; }
        public DateOnly? Date { get; set; }
        public TimeOnly? Time { get; set; }
        public string? Status { get; set; }
    }
}
