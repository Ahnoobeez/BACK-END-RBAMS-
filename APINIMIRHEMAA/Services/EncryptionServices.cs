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
        internal void Encrypt(ClientsProject clientProjectDetails)
        {
            throw new NotImplementedException();
        }
    }
}
