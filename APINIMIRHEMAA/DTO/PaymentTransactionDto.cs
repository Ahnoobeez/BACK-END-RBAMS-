namespace APINIMIRHEMAA.DTO
{
    // Defines the payment transaction response or transfer shape exchanged through the API.
    public class PaymentTransactionDto
    {
        // Identifies the related payment transaction record.
        public int PaymentTransaction_ID { get; set; }
        // Identifies the related collection receipt record.
        public int? CollectionReceipt_ID { get; set; }
        // Carries job order id for the related API operation.
        public int JobOrderID { get; set; }
        // Identifies the related quotation record.
        public int Quotation_ID { get; set; }
        // Financial amount recorded for this payment transaction dto.
        public decimal Amount { get; set; }
        // Time value for paid at utc, using the UTC/time-zone convention indicated by its name.
        public DateTime PaidAtUtc { get; set; }
        // Carries created by for the related API operation. This value is optional.
        public string? CreatedBy { get; set; }
        // Identifies the related created by user record.
        public int? CreatedByUserId { get; set; }
        // Identifies the related created by department record.
        public int? CreatedByDepartmentId { get; set; }
    }
}
