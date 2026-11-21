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


        public static string GetDeliveryReceiptApprovalStatus(
            string? persistedStatus,
            IEnumerable<DeliveryFiles> files,
            int deliveryId)
        {
            var preparedByUserId = files

                .Where(file => file.Delivery_ID == deliveryId &&
                    string.Equals(file.File_Role, "PreparedBy", StringComparison.OrdinalIgnoreCase))

                .OrderBy(file => file.File_ID)
                .Select(file => file.CreatedByUserId)
                .FirstOrDefault();

            if (string.Equals(persistedStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                return "Rejected";
            }

            if (string.Equals(persistedStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                return "Approved";
            }

            var hasValidApproval = preparedByUserId.HasValue && files.Any(file =>
                file.Delivery_ID == deliveryId &&
                string.Equals(file.File_Role, "ApproverSigned", StringComparison.OrdinalIgnoreCase) &&
                file.CreatedByUserId.HasValue &&
                file.CreatedByUserId.Value != preparedByUserId.Value);


            if (hasValidApproval)
            {
                return "Approved";
            }
            return "Request";
        }
    }
}
