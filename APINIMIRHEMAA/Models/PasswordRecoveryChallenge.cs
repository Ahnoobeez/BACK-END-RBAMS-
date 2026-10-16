namespace APINIMIRHEMAA.Models
{
    public class PasswordRecoveryChallenge
    {
        public Guid Challenge_ID { get; set; }
        public int? User_ID { get; set; }
        public int Question1_ID { get; set; }
        public int Question2_ID { get; set; }
        public int Question3_ID { get; set; }
        public int Attempts { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public DateTime? ResetAt { get; set; }
    }
}
