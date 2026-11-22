using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO;
using APINIMIRHEMAA.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace APINIMIRHEMAA.Services
{

    public sealed record MaterialRequisitionPreviewOutcome(object? Response, string? Error);

    public sealed record MaterialRequisitionBatchOutcome(int Saved, string? Error);


    public static class MaterialRequisitionWorkflowService
    {

        public static MaterialRequisitionPreviewOutcome Preview(
            LocalDbContext db,
            RequisitionPreviewDto dto,
            string? expectedDepartment = null)
        {
            var directionError = ValidateDirection(dto.FromWho, dto.ToWho, expectedDepartment);

            if (directionError != null)
            {
                return new(null, directionError);
            }

            var purchaseError = ValidatePurchase(
                db,
                dto.Purchased_ID,
                dto.Material_ID,
                dto.Supplier,
                dto.Unit_Cost,
                dto.Quantity,
                0);

            if (purchaseError != null)
            {
                return new(null, purchaseError);
            }


            if (InventoryRules.IsWarehouse(dto.ToWho))
            {
                return new(new { usesBuffer = false, fromBuffer = 0, fromPurchased = 0, fromBalance = 0 }, null);
            }

            var inventory = db.Inventory
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefault(record => record.Material_ID == dto.Material_ID);

            if (inventory == null)
            {
                return new(null, "This material has no inventory record.");
            }

            var plan = InventoryRules.IsAdminSupplierReturn(dto.FromWho, dto.ToWho)
                ? InventoryRules.PlanPurchasedIssue(inventory, dto.Quantity)
                : InventoryRules.PlanIssue(inventory, dto.Quantity);

            if (plan.Error != null)
            {
                return new(null, plan.Error);
            }

            return new(new { available = InventoryRules.Available(inventory) }, null);
        }


        public static async Task<MaterialRequisitionBatchOutcome> SubmitBatchAsync(
            LocalDbContext db,
            NotificationService notificationService,
            List<MaterialRequesition_Slip>? slips,
            int submitterUserId,
            string? expectedDepartment,
            CancellationToken cancellationToken,
            bool autoApprove = false)
        {

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

            if (submitterSignature == null || submitterSignature.SignatureData.Length == 0)
            {
                return new(0, "Add a signature in Profile Settings before submitting a Material Requisition.");
            }
            var submittedBySignature =
                $"data:image/png;base64,{Convert.ToBase64String(submitterSignature.SignatureData)}";

            var today = DateOnly.FromDateTime(DateTime.Now);
            var initializedRequests = new HashSet<int>();
            var reservedByPurchaseId = new Dictionary<int, int>();

            for (var index = 0; index < slips.Count; index++)
            {
                var slip = slips[index];
                var label = $"Row {index + 1} ({slip.Material_Name})";


                if (string.IsNullOrWhiteSpace(slip.Control_Number))
                {
                    return new(0, $"{label}: control number is required.");
                }

                if ((slip.Quantity ?? 0) <= 0)
                {
                    return new(0, $"{label}: quantity must be greater than zero.");
                }
                var quantity = slip.Quantity.GetValueOrDefault();

                if ((slip.Unit_Cost ?? 0) < 0)
                {
                    return new(0, $"{label}: unit cost cannot be negative.");
                }

                var purchasedId = slip.Purchased_ID.GetValueOrDefault();
                var reservedForBatch = reservedByPurchaseId.GetValueOrDefault(purchasedId);
                var purchaseError = ValidatePurchase(
                    db,
                    slip.Purchased_ID,
                    slip.Material_ID,
                    slip.Supplier,
                    slip.Unit_Cost,
                    quantity,
                    reservedForBatch);

                if (purchaseError != null)
                {
                    return new(0, $"{label}: {purchaseError}");
                }
                reservedByPurchaseId[purchasedId] = reservedForBatch + quantity;

                var directionError = ValidateDirection(slip.FromWho, slip.ToWho, expectedDepartment);

                if (directionError != null)
                {
                    return new(0, $"{label}: {directionError}");
                }

                var isAdminSupplierReturn = InventoryRules.IsAdminSupplierReturn(slip.FromWho, slip.ToWho);
                var isSupplierWarehouseReceipt = InventoryRules.IsSupplierWarehouseReceipt(slip.FromWho, slip.ToWho);
                if (autoApprove && !isAdminSupplierReturn && !isSupplierWarehouseReceipt)
                {
                    return new(0, $"{label}: the Admin module can automatically approve only Supplier-to-Warehouse receipts or Warehouse-to-Supplier returns.");
                }

                var inventory = await db.Inventory
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(record => record.Material_ID == slip.Material_ID, cancellationToken);

                if (inventory == null)
                {
                    return new(0, $"{label}: this material has no inventory record.");
                }


                if (initializedRequests.Add(slip.Material_ID))
                {
                    inventory.Request = GetPendingDemand(db, slip.Material_ID);
                }


                IssuePlan? purchasedIssuePlan = null;
                if (isAdminSupplierReturn)
                {
                    var plan = InventoryRules.PlanPurchasedIssue(inventory, quantity);

                    if (plan.Error != null)
                    {
                        return new(0, $"{label}: {plan.Error}");
                    }
                    purchasedIssuePlan = plan;
                }

                else if (InventoryRules.IsWarehouse(slip.FromWho))
                {
                    var plan = InventoryRules.PlanIssue(inventory, quantity);

                    if (plan.Error != null)
                    {
                        return new(0, $"{label}: {plan.Error}");
                    }
                }

                if (autoApprove)
                {
                    if (isSupplierWarehouseReceipt)
                    {
                        inventory.Returned = (inventory.Returned ?? 0) + quantity;
                        InventoryRules.Recalculate(inventory);
                    }
                    else if (purchasedIssuePlan != null)
                    {
                        InventoryRules.Apply(inventory, purchasedIssuePlan);
                    }
                    else
                    {
                        return new(0, $"{label}: the purchase receipt cannot be issued automatically.");
                    }
                }
                else
                {
                    inventory.Request = (inventory.Request ?? 0) + quantity;
                }

                slip.MR_ID = 0;
                slip.Total_Cost = slip.Quantity * slip.Unit_Cost;
                slip.Status = autoApprove ? "Approved" : "Pending";
                slip.Approved_By = autoApprove ? submittedBySignature : null;
                slip.Submmited_By = submittedBySignature;
                slip.Date = today;
                db.MaterialRequisition_Slip.Add(slip);
            }


            await db.SaveChangesAsync(cancellationToken);
            if (!autoApprove)
            {
                await notificationService.NotifyMaterialRequisitionSubmittedAsync(slips, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return new(slips.Count, null);
        }


        private static string? ValidateDirection(
            string? fromDepartment,
            string? toDepartment,
            string? expectedDepartment)
        {

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


        private static string? ValidatePurchase(
            LocalDbContext db,
            int? purchasedId,
            int materialId,
            string? supplierName,
            int? unitCost,
            int requestedQuantity,
            int reservedForBatch)
        {

            if (!purchasedId.HasValue || purchasedId.Value <= 0)
            {
                return "Choose a specific purchase receipt for this material.";
            }

            var purchase = db.Purchased_Material
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefault(record => record.Purchased_ID == purchasedId.Value
                    && record.Material_ID == materialId);

            if (purchase == null)
            {
                return "The selected purchase receipt was not found for this material.";
            }

            var alreadyConsumed = db.MaterialRequisition_Slip
                .IgnoreQueryFilters()
                .Where(slip =>
                    slip.Purchased_ID == purchasedId.Value &&
                    (slip.Status == null ||
                     (slip.Status.Trim().ToUpper() != "REJECTED" &&
                      slip.Status.Trim().ToUpper() != "CANCELED" &&
                      slip.Status.Trim().ToUpper() != "CANCELLED")))
                .Sum(slip => (long)(slip.Quantity ?? 0));
            var remainingQuantity = Math.Max(
                (long)purchase.Quantity - alreadyConsumed - reservedForBatch,
                0);
            if (requestedQuantity > remainingQuantity)
            {
                return $"This purchase receipt has {remainingQuantity} unit(s) remaining, but {requestedQuantity} were requested.";
            }


            if (!string.Equals(
                purchase.Supplier_Name?.Trim(),
                supplierName?.Trim(),
                StringComparison.OrdinalIgnoreCase))
            {
                return "The selected purchase receipt does not match the supplier.";
            }


            if (!unitCost.HasValue || unitCost.Value != purchase.Unit_price)
            {
                return "The selected purchase receipt does not match the unit cost.";
            }

            return null;
        }


        private static int GetPendingDemand(LocalDbContext db, int materialId)
        {
            return db.MaterialRequisition_Slip
                .AsNoTracking()
                .IgnoreQueryFilters()

                .Where(slip => slip.Material_ID == materialId &&
                    (slip.Status == null || slip.Status.Trim() == "" ||
                     slip.Status.Trim().ToUpper() == "PENDING"))
                .Sum(slip => slip.Quantity ?? 0);
        }
    }
}
