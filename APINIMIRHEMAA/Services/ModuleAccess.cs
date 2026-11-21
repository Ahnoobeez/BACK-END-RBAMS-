namespace APINIMIRHEMAA.Services
{
    // Provides reusable application operations for module access workflows.
    public class ModuleAccess
    {
        public static readonly string[] AllModules =
            { "analytics", "marketing", "technical", "production", "warehouse", "admin" };

        // Checks whether the supplied role name identifies a system administrator.
        public static bool IsAdministratorRole(string? role)
        {
            var normalizedRole = Normalize(role);
            return normalizedRole is "admin" or "superadmin";
        }

        // Checks whether the supplied role name identifies a department manager.
        public static bool IsDepartmentManagerRole(string? role)
        {
            var normalizedRole = Normalize(role);
            return normalizedRole is "departmentmanager" or "departmenthead";
        }

        // Retrieves the module for department.
        public static string? GetModuleForDepartment(string? department)
        {
            var normalizedDepartment = Normalize(department);
            // Exit the current operation when `normalizedDepartment.Contains() || normalizedDepartment.Contains()` is true.
            if (normalizedDepartment.Contains("marketing") || normalizedDepartment.Contains("sales"))
                return "marketing";
            // Exit the current operation when `normalizedDepartment.Contains() || normalizedDepartment.Contains()` is true.
            if (normalizedDepartment.Contains("technical") || normalizedDepartment.Contains("design"))
                return "technical";
            // Exit the current operation when `normalizedDepartment.Contains() || normalizedDepartment.Contains()` is true.
            if (normalizedDepartment.Contains("production") || normalizedDepartment.Contains("joborder"))
                return "production";
            // Exit the current operation when `normalizedDepartment.Contains() || normalizedDepartment.Contains()` is true.
            if (normalizedDepartment.Contains("inventory") || normalizedDepartment.Contains("warehouse"))
                return "warehouse";
            return null;
        }

        // Retrieves the modules.
        public static List<string> GetModules(string role, string department)
        {
            // Exit the current operation when `IsAdministratorRole(role)` is true.
            if (IsAdministratorRole(role))
                return AllModules.ToList();

            var modules = new List<string>();
            var departmentModule = GetModuleForDepartment(department);
            // Enter this branch when `departmentModule != null` is true.
            if (departmentModule != null)
                modules.Add(departmentModule);

            return modules;
        }

        // Checks whether the role and department can access the specified module.
        public static bool CanAccess(string role, string department, string module)
        {
            // Exit the current operation when `IsAdministratorRole(role)` is true.
            if (IsAdministratorRole(role))
                return AllModules.Contains(module, StringComparer.OrdinalIgnoreCase);
            // Exit the current operation when `string.Equals(module, , StringComparison.OrdinalIgnoreCase)` is true.
            if (string.Equals(module, "admin", StringComparison.OrdinalIgnoreCase))
                return false;
            return GetModules(role, department).Contains(module, StringComparer.OrdinalIgnoreCase);
        }

        // Removes punctuation and lowercases a role or department name for comparison.
        private static string Normalize(string? value) =>
            // Filter the query to rows satisfying `?` before materializing the results.
            new((value ?? "").Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
    }
}
