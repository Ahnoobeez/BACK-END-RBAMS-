namespace APINIMIRHEMAA.Services
{
    // Provides reusable application operations for password policy workflows.
    public static class PasswordPolicy
    {
        public const string RequirementMessage =
            "Password must be at least 8 characters and include an uppercase letter, a number, and a special symbol.";
        public const string DefaultResetPassword = "mirhemaG123!";

        // Checks whether the supplied password meets the configured length and character requirements.
        public static bool IsValid(string? password)
        {
            return password is { Length: >= 8 } &&
                password.Any(char.IsUpper) &&
                password.Any(char.IsDigit) &&
                password.Any(character =>
                    !char.IsLetterOrDigit(character) &&
                    !char.IsWhiteSpace(character));
        }
    }
}
