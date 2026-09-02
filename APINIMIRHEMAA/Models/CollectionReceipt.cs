using System.ComponentModel.DataAnnotations;

namespace APINIMIRHEMAA.Models
{
    
    public class CollectionReceipt
    {
        [Key]
        public int CollectionReceipt_ID { get; set; }
        public byte[] Payment_Method { get; set; }
        public DateOnly Payment_Date { get; set; }
        public string Account_Number { get; set; }
        public string Transaction_Description { get; set; }
        public int Amount { get; set; }
        public int Total_Paid_Amount { get; set; }
        public int Invoice_Reference_Number { get; set; }
        public int Check_Number { get; set; }
        public string Bank { get; set; }

    }
}
