using APINIMIRHEMAA.Models;

namespace APINIMIRHEMAA.Services
{
    public static class WorkflowRules
    {
        public static string? ValidateTargetDelivery(DateOnly? targetDelivery)
        {
            if (!targetDelivery.HasValue)
            {
                return "Target Delivery is required.";
            }

            if (targetDelivery.Value < DateOnly.FromDateTime(DateTime.Today))
            {
                return "Target Delivery cannot be in the past.";
            }

            return null;
        }

        public static bool IsReceivedByRole(string? fileRole) =>
            string.Equals(fileRole?.Trim(), "ReceivedBy", StringComparison.OrdinalIgnoreCase);

        public static bool HasReceivedByPhoto(IEnumerable<DeliveryFiles> files, int deliveryId) =>
            files.Any(file =>
                file.Delivery_ID == deliveryId &&
                IsReceivedByRole(file.File_Role) &&
                UploadValidator.ValidateImage(file.File_Data) == null);

        public static string GetDeliveryReceiptStatus(bool hasReceivedByPhoto) =>
            hasReceivedByPhoto ? "Completed" : "In Progress";
    }
}
