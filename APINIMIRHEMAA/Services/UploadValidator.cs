namespace APINIMIRHEMAA.Services
{
    // Provides reusable application operations for upload validator workflows.
    public static class UploadValidator
    {
        private const int MaxBytes = 10 * 1024 * 1024;   // per file

        // Validates the pdf.
        public static string? ValidatePdf(string? data) => CheckBase64(data, IsPdf, "PDF");
        // Validates the pdf.
        public static string? ValidatePdf(byte[]? bytes) => CheckBytes(bytes, IsPdf, "PDF");
        // Validates the image.
        public static string? ValidateImage(string? data) => CheckBase64(data, IsImage, "JPG or PNG image");
        // Validates the image.
        public static string? ValidateImage(byte[]? bytes) => CheckBytes(bytes, IsImage, "JPG or PNG image");
        // Validates the image or pdf.
        public static string? ValidateImageOrPdf(string? data) => CheckBase64(data, b => IsImage(b) || IsPdf(b), "JPG, PNG or PDF file");
        // Validates the image or pdf.
        public static string? ValidateImageOrPdf(byte[]? bytes) => CheckBytes(bytes, b => IsImage(b) || IsPdf(b), "JPG, PNG or PDF file");
        // Checks whether the upload has a PDF content type and file extension.
        private static bool IsPdf(byte[] b) =>
            b.Length > 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46;

        // Checks whether the upload has an allowed image content type and file extension.
        private static bool IsImage(byte[] b) =>
            (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) ||
            (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47);

        // Validates and decodes a base64 upload payload.
        private static string? CheckBase64(string? data, Func<byte[], bool> isValid, string label)
        {
            // Exit the current operation when `string.IsNullOrWhiteSpace(data)` is true.
            if (string.IsNullOrWhiteSpace(data)) return "File is empty.";
            // Enter this branch when `data.StartsWith(, StringComparison.OrdinalIgnoreCase)` is true.
            if (data.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = data.IndexOf(',');
                // Exit the current operation when `comma < 0` is true.
                if (comma < 0) return "File data is malformed.";
                data = data[(comma + 1)..];
            }
            byte[] bytes;
            // Keep the operation and its exception-handling paths together.
            try { bytes = Convert.FromBase64String(data); }
            // Handle `FormatException` raised by the preceding operation.
            catch (FormatException) { return "File data is not valid base64."; }
            return CheckBytes(bytes, isValid, label);
        }

        // Checks whether the byte content matches the expected file signature.
        private static string? CheckBytes(byte[]? bytes, Func<byte[], bool> isValid, string label)
        {
            // Exit the current operation when `bytes == null || bytes.Length == 0` is true.
            if (bytes == null || bytes.Length == 0) return "File is empty.";
            // Exit the current operation when `bytes.Length > MaxBytes` is true.
            if (bytes.Length > MaxBytes) return "File is larger than 10 MB.";
            return isValid(bytes) ? null : $"File is not a valid {label}.";
        }
    }
}