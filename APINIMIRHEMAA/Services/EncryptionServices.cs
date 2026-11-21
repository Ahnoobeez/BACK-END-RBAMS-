using APINIMIRHEMAA.Models;
using Microsoft.AspNetCore.DataProtection;

namespace APINIMIRHEMAA.Services
{
    // Provides reusable application operations for encryption service workflows.
    public class EncryptionService
    {
        // Injected or cached protector dependency/state used by this class.
        private readonly IDataProtector _protector;

        // Initializes a new encryption service instance.
        public EncryptionService(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector(
                "MiRhema.ClientInformation.v1"
            );
        }

        // Protects a non-empty value and leaves values already in protected form unchanged.
        public string Encrypt(string? value)
        {
            // Exit the current operation when `string.IsNullOrEmpty(value)` is true.
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            // Enter this branch when `IsProtected(value)` is true.
            if (IsProtected(value))
            {
                _protector.Unprotect(value);
                return value;
            }

            return _protector.Protect(value);
        }

        // Unprotects the supplied protected string value.
        public string Decrypt(string? value)
        {
            // Exit the current operation when `string.IsNullOrEmpty(value)` is true.
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            return _protector.Unprotect(value);
        }
        // Protects a non-empty value and leaves values already in protected form unchanged.
        public byte[] Encrypt(byte[]? value)
        {
            // Exit the current operation when `value == null || value.Length == 0` is true.
            if (value == null || value.Length == 0)
                return Array.Empty<byte>();

            // Enter this branch when `IsProtected(value)` is true.
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
            // Exit the current operation when `value == null || value.Length == 0` is true.
            if (value == null || value.Length == 0)
                return Array.Empty<byte>();

            return _protector.Unprotect(value);
        }
        // Checks whether the value is stored in protected form.
        public static bool IsProtected(byte[]? value) =>
            value != null &&
            value.Length > 4 &&
            value[0] == 0x09 &&
            value[1] == 0xF0 &&
            value[2] == 0xC9 &&
            value[3] == 0xF0;

        // Checks whether the value is stored in protected form.
        public static bool IsProtected(string? value) =>
            !string.IsNullOrEmpty(value) &&
            value.StartsWith("CfDJ8", StringComparison.Ordinal);

        // Unprotects protected data and returns unprotected input unchanged.
        public byte[] TryDecrypt(byte[]? value)
        {
            // Exit the current operation when `value == null || value.Length == 0` is true.
            if (value == null || value.Length == 0)
                return Array.Empty<byte>();

            return IsProtected(value) ? _protector.Unprotect(value) : value;
        }

        // Unprotects protected data and returns unprotected input unchanged.
        public string TryDecrypt(string? value)
        {
            // Exit the current operation when `string.IsNullOrEmpty(value)` is true.
            if (string.IsNullOrEmpty(value))
                return value ?? string.Empty;

            return IsProtected(value) ? _protector.Unprotect(value) : value;
        }
    }
}
