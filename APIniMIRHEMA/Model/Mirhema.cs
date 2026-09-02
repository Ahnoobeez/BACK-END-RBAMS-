using System.ComponentModel.DataAnnotations;

namespace APIniMIRHEMA.Model
{
    public class Mirhema
    {
        [Key]
        public int Client_ID { get; set; }
        public string Client_Name { get; set; }
        public int Client_Telephone { get; set; }
        public string Client_Address { get; set; }
        public string Client_TIN { get; set; }
        public string Payment_Terms { get; set; }
    }
}
