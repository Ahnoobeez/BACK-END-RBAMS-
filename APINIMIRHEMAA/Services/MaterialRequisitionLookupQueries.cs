using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Services
{

    public sealed record MaterialRequisitionInventoryOption(
        int Material_ID,
        string Item_Name,
        string Code_Name,
        string Category,
        string Unit,
        int? Balance);


    public sealed record MaterialRequisitionPurchaseReceiptOption(
        int Purchased_ID,
        int Material_ID,
        int SI_No,
        string Supplier_Name,
        int Unit_price,
        int Remaining_Quantity);


    public static class MaterialRequisitionLookupQueries
    {

        public static async Task<List<MaterialRequisitionInventoryOption>> GetInventoryOptionsAsync(
            LocalDbContext db,
            CancellationToken cancellationToken)
        {
            var materials = await db.Materials
                .IgnoreQueryFilters()
                .AsNoTracking()

                .OrderBy(material => material.Item_Name)
                .ToListAsync(cancellationToken);
            var inventoryRecords = await db.Inventory
                .IgnoreQueryFilters()
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            var pendingRequests = await db.MaterialRequisition_Slip
                .IgnoreQueryFilters()
                .AsNoTracking()

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


        public static async Task<List<MaterialRequisitionPurchaseReceiptOption>> GetPurchaseReceiptOptionsAsync(
            LocalDbContext db,
            int materialId,
            CancellationToken cancellationToken)
        {
            var purchases = await db.Purchased_Material
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(purchase => purchase.Material_ID == materialId
                    && purchase.Quantity > 0)

                .OrderByDescending(purchase => purchase.Purchased_ID)
                .ToListAsync(cancellationToken);

            var consumedByPurchaseId = await GetConsumedQuantitiesByPurchaseIdAsync(
                db,
                purchases.Select(purchase => purchase.Purchased_ID).ToList(),
                cancellationToken);

            return purchases
                .Select(purchase => new MaterialRequisitionPurchaseReceiptOption(
                    purchase.Purchased_ID,
                    purchase.Material_ID,
                    purchase.SI_No,
                    purchase.Supplier_Name,
                    purchase.Unit_price,
                    (int)Math.Max(
                        (long)purchase.Quantity - consumedByPurchaseId.GetValueOrDefault(purchase.Purchased_ID),
                        0)))
                .Where(receipt => receipt.Remaining_Quantity > 0)
                .ToList();
        }

        public static async Task<Dictionary<int, long>> GetConsumedQuantitiesByPurchaseIdAsync(
            LocalDbContext db,
            IReadOnlyCollection<int> purchasedIds,
            CancellationToken cancellationToken)
        {
            var ids = purchasedIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<int, long>();
            }

            var quantities = await db.MaterialRequisition_Slip
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(slip =>
                    slip.Purchased_ID.HasValue &&
                    ids.Contains(slip.Purchased_ID.Value) &&
                    (slip.Status == null ||
                     (slip.Status.Trim().ToUpper() != "REJECTED" &&
                      slip.Status.Trim().ToUpper() != "CANCELED" &&
                      slip.Status.Trim().ToUpper() != "CANCELLED")))
                .GroupBy(slip => slip.Purchased_ID!.Value)
                .Select(group => new
                {
                    PurchasedId = group.Key,
                    Quantity = group.Sum(slip => (long)(slip.Quantity ?? 0))
                })
                .ToDictionaryAsync(
                    group => group.PurchasedId,
                    group => group.Quantity,
                    cancellationToken);

            return quantities.ToDictionary(
                entry => entry.Key,
                entry => Math.Max(entry.Value, 0));
        }
    }
}
