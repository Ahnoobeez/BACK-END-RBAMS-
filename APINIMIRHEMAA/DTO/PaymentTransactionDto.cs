namespace APINIMIRHEMAA.DTO
{
    public class PaymentTransactionDto
    {
        public int PaymentTransaction_ID { get; set; }
        public int? CollectionReceipt_ID { get; set; }
        public int JobOrderID { get; set; }
        public int Quotation_ID { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaidAtUtc { get; set; }
        public string? CreatedBy { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? CreatedByDepartmentId { get; set; }
    }
}
