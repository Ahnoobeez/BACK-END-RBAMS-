using APINIMIRHEMAA.Models;

namespace APINIMIRHEMAA.Services
{
    // Provides reusable application operations for issue plan workflows.
    public class IssuePlan
    {
        public int FromBalance { get; set; }
        public int FromBuffer { get; set; }
        public string? Error { get; set; }
    }

    // Provides reusable application operations for inventory rules workflows.
    public static class InventoryRules
    {
        public const int BufferTarget = 10;
        public const int LowStockThreshold = 5;

        // Checks whether the department is a warehouse or inventory department.
        public static bool IsWarehouse(string? department) =>
            !string.IsNullOrWhiteSpace(department) &&
            (department.Contains("warehouse", StringComparison.OrdinalIgnoreCase) ||
             department.Contains("inventory", StringComparison.OrdinalIgnoreCase));

        // Checks whether the transfer is a return from Admin or Warehouse to a supplier.
        public static bool IsAdminSupplierReturn(string? from, string? to) =>
            (string.Equals(from?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase) ||
             IsWarehouse(from)) &&
            string.Equals(to?.Trim(), "Supplier", StringComparison.OrdinalIgnoreCase);

        // Returns an error when the source and destination departments are the same.
        public static string? CheckSameDepartment(string? from, string? to) =>
            !string.IsNullOrWhiteSpace(from) &&
            !string.IsNullOrWhiteSpace(to) &&
            string.Equals(from.Trim(), to.Trim(), StringComparison.OrdinalIgnoreCase)
                ? "The from field cannot be the same with the To field"
                : null;

        // Requires exactly one of the source and destination departments to be Warehouse or Inventory.
        public static string? CheckDirection(string? from, string? to)
        {
            var sameDepartmentError = CheckSameDepartment(from, to);
            // Exit the current operation when `sameDepartmentError != null` is true.
            if (sameDepartmentError != null)
                return sameDepartmentError;

            return IsWarehouse(from) == IsWarehouse(to)
                ? "Choose the Warehouse as either the FROM or the TO department (not both, not neither)."
                : null;
        }

        // Calculates how much of a purchase is needed to bring the buffer up to its target.
        public static int BufferAllocation(int currentBuffer, int purchaseQuantity) =>
            Math.Min(purchaseQuantity, Math.Max(BufferTarget - currentBuffer, 0));

        // Recomputes the inventory balance and assigns its restock, low-stock, or healthy status.
        public static void Recalculate(Inventory inventory)
        {
            inventory.Balance = Available(inventory);

            var balance = inventory.Balance ?? 0;
            var buffer = inventory.Buffer ?? 0;
            // Enter this branch when `buffer < BufferTarget && (inventory.Request_Buffer ?? 0) > 0` is true.
            if (buffer < BufferTarget && (inventory.Request_Buffer ?? 0) > 0)
            {
                inventory.Status = $"Need To Restock Buffer is only {buffer}";
            }
            // Enter this branch when `balance <= 5` is true.
            else if (balance <= LowStockThreshold)
            {
                inventory.Status = "Balance Low";
            }
            // Use this fallback path when the preceding condition is false.
            else
            {
                inventory.Status = "Balance Still Good";
            }
        }

        // Calculates remaining inventory after outstanding balance and purchased requests.
        public static int Available(Inventory inventory) =>
            (inventory.Purchased ?? 0)
            + (inventory.Returned ?? 0)
            - (inventory.Request_Balance ?? 0)
            - (inventory.Request_Purchased ?? 0);

        // Plans how an issue can be fulfilled from available balance and buffer stock.
        public static IssuePlan PlanIssue(Inventory inventory, int quantity)
        {
            var plan = new IssuePlan();
            // Enter this branch when `quantity <= 0` is true.
            if (quantity <= 0)
            {
                plan.Error = "Quantity must be greater than zero.";
                return plan;
            }

            var availableBalance = Math.Max(Available(inventory), 0);
            var availableBuffer = Math.Max(inventory.Buffer ?? 0, 0);
            // Enter this branch when `(long)availableBalance + availableBuffer < quantity` is true.
            if ((long)availableBalance + availableBuffer < quantity)
            {
                plan.Error = $"Not enough stock. Available: {availableBalance + availableBuffer}, requested: {quantity}.";
                return plan;
            }

            plan.FromBalance = Math.Min(availableBalance, quantity);
            plan.FromBuffer = quantity - plan.FromBalance;
            return plan;
        }

        // Plans an issue using available purchased stock and reports any shortfall.
        public static IssuePlan PlanPurchasedIssue(Inventory inventory, int quantity)
        {
            var plan = new IssuePlan();
            // Enter this branch when `quantity <= 0` is true.
            if (quantity <= 0)
            {
                plan.Error = "Quantity must be greater than zero.";
                return plan;
            }

            var purchased = Math.Max((inventory.Purchased ?? 0) - (inventory.Request_Purchased ?? 0), 0);
            var available = Math.Min(purchased, Math.Max(Available(inventory), 0));
            // Enter this branch when `available < quantity` is true.
            if (available < quantity)
            {
                plan.Error = $"Not enough purchased stock. Available: {available}, requested: {quantity}.";
                return plan;
            }

            plan.FromBalance = quantity;
            return plan;
        }

        // Applies the planned stock issue to the inventory request totals.
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
