using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Services
{
    // Provides the material details and balance shown in requisition inventory choices.
    public sealed record MaterialRequisitionInventoryOption(
        int Material_ID,
        string Item_Name,
        string Code_Name,
        string Category,
        string Unit,
        int? Balance);

    // Provides purchase receipt details used to select purchased material.
    public sealed record MaterialRequisitionPurchaseReceiptOption(
        int Purchased_ID,
        int Material_ID,
        int SI_No,
        string Supplier_Name,
        int Unit_price);

    // Provides reusable application operations for material requisition lookup queries workflows.
    public static class MaterialRequisitionLookupQueries
    {
        // Retrieves the inventory options.
        public static async Task<List<MaterialRequisitionInventoryOption>> GetInventoryOptionsAsync(
            LocalDbContext db,
            CancellationToken cancellationToken)
        {
            var materials = await db.Materials
                .IgnoreQueryFilters()
                .AsNoTracking()
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(material => material.Item_Name)
                .ToListAsync(cancellationToken);
            var inventoryRecords = await db.Inventory
                .IgnoreQueryFilters()
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            var pendingRequests = await db.MaterialRequisition_Slip
                .IgnoreQueryFilters()
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(slip => slip.Status == null || slip.Status.Trim() == "" ||
                    slip.Status.Trim().ToUpper() == "PENDING")
                .GroupBy(slip => slip.Material_ID)
                .Select(group => new
                {
                    MaterialId = group.Key,
                    Quantity = group.Sum(slip => slip.Quantity ?? 0)
                })
                .ToListAsync(cancellationToken);
            var pendingRequestsByMaterialId = pendingRequests
                .ToDictionary(group => group.MaterialId, group => group.Quantity);

            // Process each `inventory` returned by the collection expression in the loop header.
            foreach (var inventory in inventoryRecords)
            {
                inventory.Request = pendingRequestsByMaterialId.TryGetValue(
                    inventory.Material_ID,
                    out var quantity)
                    ? quantity
                    : 0;
                InventoryRules.Recalculate(inventory);
            }

            var balancesByMaterialId = inventoryRecords
                .GroupBy(inventory => inventory.Material_ID)
                .ToDictionary(group => group.Key, group => group.First().Balance);

            return materials
                .Select(material => new MaterialRequisitionInventoryOption(
                    material.Material_ID,
                    material.Item_Name,
                    material.Code_Name,
                    material.Category,
                    material.Unit,
                    balancesByMaterialId.TryGetValue(material.Material_ID, out var balance)
                        ? balance
                        : null))
                .ToList();
        }

        // Retrieves the purchase receipt options.
        public static async Task<List<MaterialRequisitionPurchaseReceiptOption>> GetPurchaseReceiptOptionsAsync(
            LocalDbContext db,
            int materialId,
            CancellationToken cancellationToken)
        {
            var linkedPurchaseIds = db.MaterialRequisition_Slip
                .IgnoreQueryFilters()
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(slip => slip.Purchased_ID.HasValue)
                .Select(slip => slip.Purchased_ID.GetValueOrDefault());

            return await db.Purchased_Material
                .IgnoreQueryFilters()
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(purchase => purchase.Material_ID == materialId
                    && !linkedPurchaseIds.Contains(purchase.Purchased_ID))
                // Apply the ordering expression here before returning or materializing the results.
                .OrderByDescending(purchase => purchase.Purchased_ID)
                .Select(purchase => new MaterialRequisitionPurchaseReceiptOption(
                    purchase.Purchased_ID,
                    purchase.Material_ID,
                    purchase.SI_No,
                    purchase.Supplier_Name,
                    purchase.Unit_price))
                .ToListAsync(cancellationToken);
        }
    }
}
