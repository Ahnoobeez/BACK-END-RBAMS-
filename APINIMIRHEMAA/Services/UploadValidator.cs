namespace APINIMIRHEMAA.Services
{
    public static class UploadValidator
    {
        private const int MaxBytes = 10 * 1024 * 1024;   // per file

        public static string? ValidatePdf(string? data) => CheckBase64(data, IsPdf, "PDF");
        public static string? ValidatePdf(byte[]? bytes) => CheckBytes(bytes, IsPdf, "PDF");
        public static string? ValidateImage(string? data) => CheckBase64(data, IsImage, "JPG or PNG image");
        public static string? ValidateImage(byte[]? bytes) => CheckBytes(bytes, IsImage, "JPG or PNG image");
        public static string? ValidateImageOrPdf(string? data) => CheckBase64(data, b => IsImage(b) || IsPdf(b), "JPG, PNG or PDF file");
        public static string? ValidateImageOrPdf(byte[]? bytes) => CheckBytes(bytes, b => IsImage(b) || IsPdf(b), "JPG, PNG or PDF file");
        private static bool IsPdf(byte[] b) =>
            b.Length > 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46;

        private static bool IsImage(byte[] b) =>
            (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) ||
            (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47);

        private static string? CheckBase64(string? data, Func<byte[], bool> isValid, string label)
        {
            if (string.IsNullOrWhiteSpace(data)) return "File is empty.";
            if (data.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = data.IndexOf(',');
                if (comma < 0) return "File data is malformed.";
                data = data[(comma + 1)..];
            }
            byte[] bytes;
            try { bytes = Convert.FromBase64String(data); }
            catch (FormatException) { return "File data is not valid base64."; }
            return CheckBytes(bytes, isValid, label);
        }

        private static string? CheckBytes(byte[]? bytes, Func<byte[], bool> isValid, string label)
        {
            if (bytes == null || bytes.Length == 0) return "File is empty.";
            if (bytes.Length > MaxBytes) return "File is larger than 10 MB.";
            return isValid(bytes) ? null : $"File is not a valid {label}.";
        }
    }
}