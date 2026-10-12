namespace APINIMIRHEMAA.Services
{
    public class PasswordService
    {
        public string Hash(string password) =>
            BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);

        public bool Verify(string hash, string password)
        {
            try { return BCrypt.Net.BCrypt.Verify(password, hash); }
            catch (BCrypt.Net.SaltParseException) { return false; }   // stored value isn't a BCrypt hash
        }
    }
}