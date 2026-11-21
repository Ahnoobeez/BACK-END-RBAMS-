namespace APINIMIRHEMAA.Services
{
    // Provides reusable application operations for password service workflows.
    public class PasswordService
    {
        // Creates a one-way password hash for the supplied password.
        public string Hash(string password) =>
            BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);

        // Checks the password against its stored BCrypt hash and rejects malformed hashes.
        public bool Verify(string hash, string password)
        {
            // Keep the operation and its exception-handling paths together.
            try { return BCrypt.Net.BCrypt.Verify(password, hash); }
            // Handle `BCrypt.Net.SaltParseException` raised by the preceding operation.
            catch (BCrypt.Net.SaltParseException) { return false; }   // stored value isn't a BCrypt hash
        }
    }
}