namespace APINIMIRHEMAA.Services
{

    public class ModuleAccess
    {
        public static readonly string[] AllModules =
            { "analytics", "marketing", "technical", "production", "warehouse", "admin" };


        public static bool IsAdministratorRole(string? role)
        {
            var normalizedRole = Normalize(role);
            return normalizedRole is "admin" or "superadmin";
        }


        public static bool IsDepartmentManagerRole(string? role)
        {
            var normalizedRole = Normalize(role);
            return normalizedRole is "departmentmanager" or "departmenthead";
        }


        public static string? GetModuleForDepartment(string? department)
        {
            var normalizedDepartment = Normalize(department);

            if (normalizedDepartment.Contains("marketing") || normalizedDepartment.Contains("sales"))
                return "marketing";

            if (normalizedDepartment.Contains("technical") || normalizedDepartment.Contains("design"))
                return "technical";

            if (normalizedDepartment.Contains("production") || normalizedDepartment.Contains("joborder"))
                return "production";

            if (normalizedDepartment.Contains("inventory") || normalizedDepartment.Contains("warehouse"))
                return "warehouse";
            return null;
        }


        public static List<string> GetModules(string role, string department)
        {

            if (IsAdministratorRole(role))
                return AllModules.ToList();

            var modules = new List<string>();
            var departmentModule = GetModuleForDepartment(department);

            if (departmentModule != null)
                modules.Add(departmentModule);

            return modules;
        }


        public static bool CanAccess(string role, string department, string module)
        {

            if (IsAdministratorRole(role))
                return AllModules.Contains(module, StringComparer.OrdinalIgnoreCase);

            if (string.Equals(module, "admin", StringComparison.OrdinalIgnoreCase))
                return false;
            return GetModules(role, department).Contains(module, StringComparer.OrdinalIgnoreCase);
        }


        private static string Normalize(string? value) =>

            new((value ?? "").Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
    }
}
