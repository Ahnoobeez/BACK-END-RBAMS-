namespace APINIMIRHEMAA.DTO.Security
{
    public sealed class SecurityQuestionAnswerDto
    {
        public int QuestionId { get; set; }
        public string? Answer { get; set; }
    }

    public sealed class CompleteSecuritySetupDto
    {
        public string? SetupToken { get; set; }
        public List<SecurityQuestionAnswerDto>? Answers { get; set; }
    }

    public sealed class ForgotPasswordStartDto
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
    }

    public sealed class VerifyPasswordRecoveryDto
    {
        public Guid ChallengeId { get; set; }
        public List<SecurityQuestionAnswerDto>? Answers { get; set; }
    }

    public sealed class ResetPasswordRecoveryDto
    {
        public Guid ChallengeId { get; set; }
        public string? NewPassword { get; set; }
        public string? ConfirmNewPassword { get; set; }
    }
}
