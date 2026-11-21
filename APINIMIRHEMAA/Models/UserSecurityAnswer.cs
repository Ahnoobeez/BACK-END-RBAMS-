namespace APINIMIRHEMAA.Models
{
    // Represents the user security answer record stored or used by the application.
    public class UserSecurityAnswer
    {
        // Identifies the related user record.
        public int User_ID { get; set; }
        // Identifies the related question record.
        public int Question_ID { get; set; }
        // Stores answer hash on this user security answer.
        public required string AnswerHash { get; set; }
    }
}
