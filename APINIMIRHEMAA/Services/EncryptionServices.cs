using APINIMIRHEMAA.Models;
using Microsoft.AspNetCore.DataProtection;

namespace APINIMIRHEMAA.Services
{
    public class EncryptionService
    {
        private readonly IDataProtector _protector;

        public EncryptionService(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector(
                "MiRhema.ClientInformation.v1"
            );
        }

        public string Encrypt(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            return _protector.Protect(value);
        }

        public string Decrypt(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            return _protector.Unprotect(value);
        }
        public byte[] Encrypt(byte[]? value)
        {
            if (value == null || value.Length == 0)
                return Array.Empty<byte>();

            return _protector.Protect(value);
        }

        // For decrypting files
        public byte[] Decrypt(byte[]? value)
        {
            if (value == null || value.Length == 0)
                return Array.Empty<byte>();

            return _protector.Unprotect(value);
        }
        public static bool IsProtected(byte[]? v) => v != null && v.Length > 4 && v[0] == 0x09 && v[1] == 0xF0 && v[2] == 0xC9 && v[3] == 0xF0;

        public byte[] TryDecrypt(byte[]? value)
        {
            if (value == null || value.Length == 0) return Array.Empty<byte>();
            if (!IsProtected(value)) return value;   // legacy plaintext row, return as-is
            try { return _protector.Unprotect(value); }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                Console.Error.WriteLine("Decrypt failed: " + ex.Message);
                return Array.Empty<byte>();
            }
        }
        public string TryDecrypt(string? value)
        {
            if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
            try { return _protector.Unprotect(value); }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                Console.Error.WriteLine("Decrypt failed: " + ex.Message);
                return string.Empty;
            }
        }
    }
}
