using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO;
using APINIMIRHEMAA.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace APINIMIRHEMAA.Services
{
    // Represents the validation result returned when previewing a material requisition.
    public sealed record MaterialRequisitionPreviewOutcome(object? Response, string? Error);
    // Represents the result of submitting a material requisition batch.
    public sealed record MaterialRequisitionBatchOutcome(int Saved, string? Error);

    // Provides reusable application operations for material requisition workflow service workflows.
    public static class MaterialRequisitionWorkflowService
    {
        // Validates a material requisition and returns its preview result without submitting it.
        public static MaterialRequisitionPreviewOutcome Preview(
            LocalDbContext db,
            RequisitionPreviewDto dto,
            string? expectedDepartment = null)
        {
            var directionError = ValidateDirection(dto.FromWho, dto.ToWho, expectedDepartment);
            // Exit the current operation when `directionError != null` is true.
            if (directionError != null)
            {
                return new(null, directionError);
            }

            var purchaseError = ValidatePurchase(
                db,
                dto.Purchased_ID,
                dto.Material_ID,
                dto.Supplier,
                dto.Unit_Cost);
            // Exit the current operation when `purchaseError != null` is true.
            if (purchaseError != null)
            {
                return new(null, purchaseError);
            }

            // Exit the current operation when `InventoryRules.IsWarehouse(dto.ToWho)` is true.
            if (InventoryRules.IsWarehouse(dto.ToWho))
            {
                return new(new { usesBuffer = false, fromBuffer = 0, fromPurchased = 0, fromBalance = 0 }, null);
            }

            var inventory = db.Inventory
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefault(record => record.Material_ID == dto.Material_ID);
            // Exit the current operation when `inventory == null` is true.
            if (inventory == null)
            {
                return new(null, "This material has no inventory record.");
            }

            var plan = InventoryRules.IsAdminSupplierReturn(dto.FromWho, dto.ToWho)
                ? InventoryRules.PlanPurchasedIssue(inventory, dto.Quantity)
                : InventoryRules.PlanIssue(inventory, dto.Quantity);
            // Exit the current operation when `plan.Error != null` is true.
            if (plan.Error != null)
            {
                return new(null, plan.Error);
            }

            return new(new { available = InventoryRules.Available(inventory) }, null);
        }

        // Validates and saves a material requisition batch in a transaction.
        public static async Task<MaterialRequisitionBatchOutcome> SubmitBatchAsync(
            LocalDbContext db,
            NotificationService notificationService,
            List<MaterialRequesition_Slip>? slips,
            int submitterUserId,
            string? expectedDepartment,
            CancellationToken cancellationToken)
        {
            // Exit the current operation when `slips == null || slips.Count == 0` is true.
            if (slips == null || slips.Count == 0)
            {
                return new(0, "No materials.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var submitterSignature = await db.UserSignatures
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    signature => signature.User_ID == submitterUserId,
                    cancellationToken);
            // Exit the current operation when `submitterSignature == null || submitterSignature.SignatureData.Length == 0` is true.
            if (submitterSignature == null || submitterSignature.SignatureData.Length == 0)
            {
                return new(0, "Add a signature in Profile Settings before submitting a Material Requisition.");
            }
            var submittedBySignature =
                $"data:image/png;base64,{Convert.ToBase64String(submitterSignature.SignatureData)}";

            var today = DateOnly.FromDateTime(DateTime.Now);
            var initializedRequests = new HashSet<int>();
            // Repeat the body while `the loop condition` remains true.
            for (var index = 0; index < slips.Count; index++)
            {
                var slip = slips[index];
                var label = $"Row {index + 1} ({slip.Material_Name})";

                // Exit the current operation when `string.IsNullOrWhiteSpace(slip.Control_Number)` is true.
                if (string.IsNullOrWhiteSpace(slip.Control_Number))
                {
                    return new(0, $"{label}: control number is required.");
                }
                // Exit the current operation when `(slip.Quantity ?? 0) <= 0` is true.
                if ((slip.Quantity ?? 0) <= 0)
                {
                    return new(0, $"{label}: quantity must be greater than zero.");
                }
                var quantity = slip.Quantity.GetValueOrDefault();
                // Exit the current operation when `(slip.Unit_Cost ?? 0) < 0` is true.
                if ((slip.Unit_Cost ?? 0) < 0)
                {
                    return new(0, $"{label}: unit cost cannot be negative.");
                }

                var purchaseError = ValidatePurchase(
                    db,
                    slip.Purchased_ID,
                    slip.Material_ID,
                    slip.Supplier,
                    slip.Unit_Cost);
                // Exit the current operation when `purchaseError != null` is true.
                if (purchaseError != null)
                {
                    return new(0, $"{label}: {purchaseError}");
                }

                var directionError = ValidateDirection(slip.FromWho, slip.ToWho, expectedDepartment);
                // Exit the current operation when `directionError != null` is true.
                if (directionError != null)
                {
                    return new(0, $"{label}: {directionError}");
                }

                var inventory = await db.Inventory
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(record => record.Material_ID == slip.Material_ID, cancellationToken);
                // Exit the current operation when `inventory == null` is true.
                if (inventory == null)
                {
                    return new(0, $"{label}: this material has no inventory record.");
                }

                // Enter this branch when `initializedRequests.Add(slip.Material_ID)` is true.
                if (initializedRequests.Add(slip.Material_ID))
                {
                    inventory.Request = GetPendingDemand(db, slip.Material_ID);
                }

                // Enter this branch when `InventoryRules.IsAdminSupplierReturn(slip.FromWho, slip.ToWho)` is true.
                if (InventoryRules.IsAdminSupplierReturn(slip.FromWho, slip.ToWho))
                {
                    var plan = InventoryRules.PlanPurchasedIssue(inventory, quantity);
                    // Exit the current operation when `plan.Error != null` is true.
                    if (plan.Error != null)
                    {
                        return new(0, $"{label}: {plan.Error}");
                    }
                }
                // Enter this branch when `InventoryRules.IsWarehouse(slip.FromWho)` is true.
                else if (InventoryRules.IsWarehouse(slip.FromWho))
                {
                    var plan = InventoryRules.PlanIssue(inventory, quantity);
                    // Exit the current operation when `plan.Error != null` is true.
                    if (plan.Error != null)
                    {
                        return new(0, $"{label}: {plan.Error}");
                    }
                }

                inventory.Request = (inventory.Request ?? 0) + quantity;
                slip.MR_ID = 0;
                slip.Total_Cost = slip.Quantity * slip.Unit_Cost;
                slip.Status = "Pending";
                slip.Approved_By = null;
                slip.Submmited_By = submittedBySignature;
                slip.Date = today;
                db.MaterialRequisition_Slip.Add(slip);
            }

            // Persist the pending entity changes to the database at this point.
            await db.SaveChangesAsync(cancellationToken);
            await notificationService.NotifyMaterialRequisitionSubmittedAsync(slips, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(slips.Count, null);
        }

        // Validates the direction.
        private static string? ValidateDirection(
            string? fromDepartment,
            string? toDepartment,
            string? expectedDepartment)
        {
            // Exit the current operation when `!string.IsNullOrWhiteSpace(expectedDepartment) && (!InventoryRules.IsWarehouse(fromDepartment) || !string.Equals( toDepartment?.Trim(), expectedDepartment, StringComparison.OrdinalIgnoreCase))` is true.
            if (!string.IsNullOrWhiteSpace(expectedDepartment)
                && (!InventoryRules.IsWarehouse(fromDepartment)
                    || !string.Equals(
                        toDepartment?.Trim(),
                        expectedDepartment,
                        StringComparison.OrdinalIgnoreCase)))
            {
                return $"This account can submit requisitions only from Warehouse to {expectedDepartment}.";
            }

            return InventoryRules.CheckDirection(fromDepartment, toDepartment);
        }

        // Validates the purchase.
        private static string? ValidatePurchase(
            LocalDbContext db,
            int? purchasedId,
            int materialId,
            string? supplierName,
            int? unitCost)
        {
            // Exit the current operation when `!purchasedId.HasValue || purchasedId.Value <= 0` is true.
            if (!purchasedId.HasValue || purchasedId.Value <= 0)
            {
                return "Choose a specific purchase receipt for this material.";
            }

            var purchase = db.Purchased_Material
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefault(record => record.Purchased_ID == purchasedId.Value
                    && record.Material_ID == materialId);
            // Exit the current operation when `purchase == null` is true.
            if (purchase == null)
            {
                return "The selected purchase receipt was not found for this material.";
            }

            // Exit the current operation when `db.MaterialRequisition_Slip .IgnoreQueryFilters() .Any(slip => slip.Purchased_ID == purchasedId.Value)` is true.
            if (db.MaterialRequisition_Slip
                .IgnoreQueryFilters()
                .Any(slip => slip.Purchased_ID == purchasedId.Value))
            {
                return "This purchase receipt is locked because it is already linked to a saved Material Requisition.";
            }

            // Exit the current operation when `!string.Equals( purchase.Supplier_Name?.Trim(), supplierName?.Trim(), StringComparison.OrdinalIgnoreCase)` is true.
            if (!string.Equals(
                purchase.Supplier_Name?.Trim(),
                supplierName?.Trim(),
                StringComparison.OrdinalIgnoreCase))
            {
                return "The selected purchase receipt does not match the supplier.";
            }

            // Exit the current operation when `!unitCost.HasValue || unitCost.Value != purchase.Unit_price` is true.
            if (!unitCost.HasValue || unitCost.Value != purchase.Unit_price)
            {
                return "The selected purchase receipt does not match the unit cost.";
            }

            return null;
        }

        // Retrieves the pending demand.
        private static int GetPendingDemand(LocalDbContext db, int materialId)
        {
            return db.MaterialRequisition_Slip
                .AsNoTracking()
                .IgnoreQueryFilters()
                // Filter the query to rows satisfying `.Where(slip => slip.Material_ID == materialId && (slip.Status == null || slip.Status.Trim() == || slip.Status.Trim().ToUpper() == )) .Sum(slip => slip.Quantity ?? 0); } } }` before materializing the results.
                .Where(slip => slip.Material_ID == materialId &&
                    (slip.Status == null || slip.Status.Trim() == "" ||
                     slip.Status.Trim().ToUpper() == "PENDING"))
                .Sum(slip => slip.Quantity ?? 0);
        }
    }
}
