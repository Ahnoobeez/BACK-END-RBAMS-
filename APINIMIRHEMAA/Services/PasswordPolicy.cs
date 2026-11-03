namespace APINIMIRHEMAA.Services
{
    public static class PasswordPolicy
    {
        public const string RequirementMessage =
            "Password must be at least 8 characters and include an uppercase letter, a number, and a special symbol.";
        public const string DefaultResetPassword = "mirhemaG123!";

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
