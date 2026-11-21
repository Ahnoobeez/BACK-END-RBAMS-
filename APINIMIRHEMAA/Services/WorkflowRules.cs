using APINIMIRHEMAA.Models;

namespace APINIMIRHEMAA.Services
{
    // Provides reusable application operations for workflow rules workflows.
    public static class WorkflowRules
    {
        // Validates the target delivery.
        public static string? ValidateTargetDelivery(DateOnly? targetDelivery)
        {
            // Exit the current operation when `!targetDelivery.HasValue` is true.
            if (!targetDelivery.HasValue)
            {
                return "Target Delivery is required.";
            }

            // Exit the current operation when `targetDelivery.Value < DateOnly.FromDateTime(DateTime.Today)` is true.
            if (targetDelivery.Value < DateOnly.FromDateTime(DateTime.Today))
            {
                return "Target Delivery cannot be in the past.";
            }

            return null;
        }

        // Checks whether the receiving user is permitted by the workflow role rules.
        public static bool IsReceivedByRole(string? fileRole) =>
            string.Equals(fileRole?.Trim(), "ReceivedBy", StringComparison.OrdinalIgnoreCase);

        // Checks whether the required receiving photo is present.
        public static bool HasReceivedByPhoto(IEnumerable<DeliveryFiles> files, int deliveryId) =>
            files.Any(file =>
                file.Delivery_ID == deliveryId &&
                IsReceivedByRole(file.File_Role) &&
                UploadValidator.ValidateImage(file.File_Data) == null);

        // Retrieves the delivery receipt status.
        public static string GetDeliveryReceiptStatus(bool hasReceivedByPhoto) =>
            hasReceivedByPhoto ? "Completed" : "In Progress";

        // Retrieves the delivery receipt approval status.
        public static string GetDeliveryReceiptApprovalStatus(
            string? persistedStatus,
            IEnumerable<DeliveryFiles> files,
            int deliveryId)
        {
            var preparedByUserId = files
                // Apply this predicate in the database query before materializing results.
                .Where(file => file.Delivery_ID == deliveryId &&
                    string.Equals(file.File_Role, "PreparedBy", StringComparison.OrdinalIgnoreCase))
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(file => file.File_ID)
                .Select(file => file.CreatedByUserId)
                .FirstOrDefault();
            // Exit the current operation when `string.Equals(persistedStatus, , StringComparison.OrdinalIgnoreCase)` is true.
            if (string.Equals(persistedStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                return "Rejected";
            }
            // Exit the current operation when `string.Equals(persistedStatus, , StringComparison.OrdinalIgnoreCase)` is true.
            if (string.Equals(persistedStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                return "Approved";
            }

            var hasValidApproval = preparedByUserId.HasValue && files.Any(file =>
                file.Delivery_ID == deliveryId &&
                string.Equals(file.File_Role, "ApproverSigned", StringComparison.OrdinalIgnoreCase) &&
                file.CreatedByUserId.HasValue &&
                file.CreatedByUserId.Value != preparedByUserId.Value);

            // Exit the current operation when `hasValidApproval` is true.
            if (hasValidApproval)
            {
                return "Approved";
            }
            return "Request";
        }
    }
}
