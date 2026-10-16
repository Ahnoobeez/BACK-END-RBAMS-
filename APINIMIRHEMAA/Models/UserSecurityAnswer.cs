namespace APINIMIRHEMAA.Models
{
    public class UserSecurityAnswer
    {
        public int User_ID { get; set; }
        public int Question_ID { get; set; }
        public required string AnswerHash { get; set; }
    }
}
