namespace APINIMIRHEMAA.Services
{
    public class ModuleAccess
    {
        public static readonly string[] AllModules =
            { "analytics", "marketing", "technical", "production", "warehouse", "admin" };

        public static List<string> GetModules(string role, string department)
        {
            // Super Admin and Admin roles see everything
            if (role == "Super Admin" || role == "Admin")
                return AllModules.ToList();

            var modules = new List<string>();

            // Everyone else gets their own department's module
            switch (department)
            {
                case "Marketing": modules.Add("marketing"); break;
                case "Technical": modules.Add("technical"); break;
                case "Production": modules.Add("production"); break;
                case "Inventory": modules.Add("warehouse"); break;
                case "Admin": modules.Add("admin"); break;
            }

            // Department Managers also get the analytics dashboard
            if (role == "Department Manager")
                modules.Add("dashboard");

            return modules;
        }

        public static bool CanAccess(string role, string department, string module)
            => GetModules(role, department).Contains(module);
    }
}
