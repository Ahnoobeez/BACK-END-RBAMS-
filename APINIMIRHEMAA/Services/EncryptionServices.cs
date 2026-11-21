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

            if (IsProtected(value))
            {
                _protector.Unprotect(value);
                return value;
            }

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

            if (IsProtected(value))
            {
                _protector.Unprotect(value);
                return value;
            }

            return _protector.Protect(value);
        }

        // For decrypting files
        public byte[] Decrypt(byte[]? value)
        {
            if (value == null || value.Length == 0)
                return Array.Empty<byte>();

            return _protector.Unprotect(value);
        }
        public static bool IsProtected(byte[]? value) =>
            value != null &&
            value.Length > 4 &&
            value[0] == 0x09 &&
            value[1] == 0xF0 &&
            value[2] == 0xC9 &&
            value[3] == 0xF0;

        public static bool IsProtected(string? value) =>
            !string.IsNullOrEmpty(value) &&
            value.StartsWith("CfDJ8", StringComparison.Ordinal);

        public byte[] TryDecrypt(byte[]? value)
        {
            if (value == null || value.Length == 0)
                return Array.Empty<byte>();

            return IsProtected(value) ? _protector.Unprotect(value) : value;
        }

        public string TryDecrypt(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            return IsProtected(value) ? _protector.Unprotect(value) : value;
        }
    }
}
