namespace APINIMIRHEMAA.DTO.Security
{
    // Defines the security question answer response or transfer shape exchanged through the API.
    public sealed class SecurityQuestionAnswerDto
    {
        // Identifies the related question record.
        public int QuestionId { get; set; }
        // Carries answer for the related API operation. This value is optional.
        public string? Answer { get; set; }
    }

    // Defines the complete security setup response or transfer shape exchanged through the API.
    public sealed class CompleteSecuritySetupDto
    {
        // Carries setup token for the related API operation. This value is optional.
        public string? SetupToken { get; set; }
        // Carries answers for the related API operation. This value is optional.
        public List<SecurityQuestionAnswerDto>? Answers { get; set; }
    }

    // Defines the forgot password start response or transfer shape exchanged through the API.
    public sealed class ForgotPasswordStartDto
    {
        // Carries username for the related API operation. This value is optional.
        public string? Username { get; set; }
        // Carries email for the related API operation. This value is optional.
        public string? Email { get; set; }
    }

    // Defines the verify password recovery response or transfer shape exchanged through the API.
    public sealed class VerifyPasswordRecoveryDto
    {
        // Identifies the related challenge record.
        public Guid ChallengeId { get; set; }
        // Carries answers for the related API operation. This value is optional.
        public List<SecurityQuestionAnswerDto>? Answers { get; set; }
    }

    // Defines the reset password recovery response or transfer shape exchanged through the API.
    public sealed class ResetPasswordRecoveryDto
    {
        // Identifies the related challenge record.
        public Guid ChallengeId { get; set; }
        // Carries new password for the related API operation. This value is optional.
        public string? NewPassword { get; set; }
        // Carries confirm new password for the related API operation. This value is optional.
        public string? ConfirmNewPassword { get; set; }
    }
}
