using APINIMIRHEMAA.Models;

namespace APINIMIRHEMAA.Services
{
    public class IssuePlan
    {
        public int FromBalance { get; set; }
        public int FromBuffer { get; set; }
        public string? Error { get; set; }
    }

    public static class InventoryRules
    {
        public const int BufferTarget = 10;

        public static bool IsWarehouse(string? department) =>
            !string.IsNullOrWhiteSpace(department) &&
            (department.Contains("warehouse", StringComparison.OrdinalIgnoreCase) ||
             department.Contains("inventory", StringComparison.OrdinalIgnoreCase));

        public static string? CheckDirection(string? from, string? to) =>
            IsWarehouse(from) == IsWarehouse(to)
                ? "Choose the Warehouse as either the FROM or the TO department (not both, not neither)."
                : null;

        public static int BufferAllocation(int currentBuffer, int purchaseQuantity) =>
            Math.Min(purchaseQuantity, Math.Max(BufferTarget - currentBuffer, 0));

        public static void Recalculate(Inventory inventory)
        {
            inventory.Balance = Available(inventory);

            var balance = inventory.Balance ?? 0;
            var buffer = inventory.Buffer ?? 0;
            if (buffer < BufferTarget && (inventory.Request_Buffer ?? 0) > 0)
            {
                inventory.Status = $"Need To Restock Buffer is only {buffer}";
            }
            else if (balance <= 5)
            {
                inventory.Status = "Balance Low";
            }
            else
            {
                inventory.Status = "Balance Still Good";
            }
        }

        public static int Available(Inventory inventory) =>
            (inventory.Purchased ?? 0)
            + (inventory.Returned ?? 0)
            - (inventory.Request_Balance ?? 0)
            - (inventory.Request_Purchased ?? 0);

        public static IssuePlan PlanIssue(Inventory inventory, int quantity)
        {
            var plan = new IssuePlan();
            if (quantity <= 0)
            {
                plan.Error = "Quantity must be greater than zero.";
                return plan;
            }

            var availableBalance = Math.Max(Available(inventory), 0);
            var availableBuffer = Math.Max(inventory.Buffer ?? 0, 0);
            if ((long)availableBalance + availableBuffer < quantity)
            {
                plan.Error = $"Not enough stock. Available: {availableBalance + availableBuffer}, requested: {quantity}.";
                return plan;
            }

            plan.FromBalance = Math.Min(availableBalance, quantity);
            plan.FromBuffer = quantity - plan.FromBalance;
            return plan;
        }

        public static void Apply(Inventory inventory, IssuePlan plan)
        {
            Recalculate(inventory);
            inventory.Request_Purchased = (inventory.Request_Purchased ?? 0) + plan.FromBalance;
            inventory.Request_Buffer = (inventory.Request_Buffer ?? 0) + plan.FromBuffer;
            inventory.Buffer = (inventory.Buffer ?? 0) - plan.FromBuffer;
            Recalculate(inventory);
        }
    }
}
