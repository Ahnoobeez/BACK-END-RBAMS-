namespace APINIMIRHEMAA.Models
{
    // Represents the password recovery challenge record stored or used by the application.
    public class PasswordRecoveryChallenge
    {
        // Identifies the related challenge record.
        public Guid Challenge_ID { get; set; }
        // Identifies the related user record.
        public int? User_ID { get; set; }
        // Identifies the related question1 record.
        public int Question1_ID { get; set; }
        // Identifies the related question2 record.
        public int Question2_ID { get; set; }
        // Identifies the related question3 record.
        public int Question3_ID { get; set; }
        // Stores attempts on this password recovery challenge.
        public int Attempts { get; set; }
        // Stores created at on this password recovery challenge.
        public DateTime CreatedAt { get; set; }
        // Stores expires at on this password recovery challenge.
        public DateTime ExpiresAt { get; set; }
        // Stores verified at on this password recovery challenge.
        public DateTime? VerifiedAt { get; set; }
        // Stores reset at on this password recovery challenge.
        public DateTime? ResetAt { get; set; }
    }
}
