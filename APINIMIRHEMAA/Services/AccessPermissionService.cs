using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO.Admin;
using APINIMIRHEMAA.Models;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Services
{
    public sealed record AccessPermissionServiceDefinition(string ModuleKey, string ServiceKey, string Label);

    public static class AccessPermissionCatalog
    {
        public static readonly string[] Actions =
            { "Create", "Read", "Update", "Delete", "Approve", "Verify", "Release" };

        public static readonly AccessPermissionServiceDefinition[] Services =
        {
            new("marketing", "marketing.create-client", "Create Client"),
            new("marketing", "marketing.quotation-builder", "Quotation Builder"),
            new("marketing", "marketing.graphics", "Graphics"),
            new("marketing", "marketing.conforme", "Conforme"),
            new("marketing", "marketing.purchase-order", "Purchase Order"),
            new("marketing", "marketing.job-order", "Job Order"),
            new("marketing", "marketing.delivery-receipt", "Delivery Receipt"),
            new("marketing", "marketing.collection-receipt", "Collection Receipt"),
            new("marketing", "marketing.service-invoice", "Service Invoice"),
            new("marketing", "marketing.installation-schedule", "Installation Schedule"),
            new("technical", "technical.job-order", "Job Order"),
            new("technical", "technical.graphics", "Graphics"),
            new("technical", "technical.material-requisition", "Material Requisition"),
            new("production", "production.job-order", "Job Order"),
            new("production", "production.installation-report", "Installation Report"),
            new("production", "production.material-requisition", "Material Requisition"),
            new("production", "production.installation-schedule", "Installation Schedule"),
            new("warehouse", "warehouse.inventory-master", "Inventory Master"),
            new("warehouse", "warehouse.purchased-materials", "Purchased Materials"),
            new("warehouse", "warehouse.material-requisition", "Material Requisition"),
            new("warehouse", "warehouse.saved-records", "Saved Records")
        };

        public static bool IsValid(string serviceKey, string actionKey) =>
            Services.Any(service => service.ServiceKey == serviceKey) &&
            Actions.Contains(actionKey, StringComparer.Ordinal);

        public static bool TryResolveService(string controller, string action, out string serviceKey)
        {
            var name = Normalize(action);
            serviceKey = "";
            switch (controller)
            {
                case "Clients":
                    serviceKey = "marketing.create-client";
                    return true;
                case "ProjectViewer":
                    serviceKey = "marketing.quotation-builder";
                    return true;
                case "Marketing":
                    serviceKey = Match(name,
                        ("serviceinvoice", "marketing.service-invoice"),
                        ("installationschedule", "marketing.installation-schedule"),
                        ("collectionreceipt", "marketing.collection-receipt"),
                        ("deliveryreceipt", "marketing.delivery-receipt"),
                        ("purchaseorder", "marketing.purchase-order"),
                        ("quotation", "marketing.quotation-builder"),
                        ("conforme", "marketing.conforme"),
                        ("graphics", "marketing.graphics"),
                        ("joborder", "marketing.job-order"));
                    return !string.IsNullOrEmpty(serviceKey);
                case "Technical":
                    serviceKey = Match(name,
                        ("materialrequisition", "technical.material-requisition"),
                        ("materialrequesition", "technical.material-requisition"),
                        ("graphics", "technical.graphics"),
                        ("joborder", "technical.job-order"));
                    return !string.IsNullOrEmpty(serviceKey);
                case "Production":
                    serviceKey = Match(name,
                        ("installationschedule", "production.installation-schedule"),
                        ("installationreport", "production.installation-report"),
                        ("materialrequisition", "production.material-requisition"),
                        ("materialrequesition", "production.material-requisition"),
                        ("joborder", "production.job-order"));
                    return !string.IsNullOrEmpty(serviceKey);
                case "Inventory":
                    serviceKey = Match(name,
                        ("purchasedmaterial", "warehouse.purchased-materials"),
                        ("materialrequisition", "warehouse.material-requisition"),
                        ("materialrequesition", "warehouse.material-requisition"),
                        ("stocktransfer", "warehouse.saved-records"),
                        ("transmittal", "warehouse.saved-records"),
                        ("inventory", "warehouse.inventory-master"),
                        ("material", "warehouse.inventory-master"));
                    return !string.IsNullOrEmpty(serviceKey);
                default:
                    return false;
            }
        }

        public static string? ResolveAction(string actionName, string httpMethod)
        {
            var name = Normalize(actionName);
            if (name.Contains("delete") || httpMethod.Equals("DELETE", StringComparison.OrdinalIgnoreCase))
                return "Delete";
            if (name.Contains("approve") || name.Contains("reject") || name.Contains("complete"))
                return "Approve";
            if (name.Contains("verify"))
                return "Verify";
            if (name.Contains("release"))
                return "Release";
            if (name.Contains("update") || name.Contains("edit") || name.Contains("status"))
                return "Update";
            if (name.StartsWith("get", StringComparison.Ordinal) ||
                name.StartsWith("search", StringComparison.Ordinal) ||
                name.StartsWith("fetch", StringComparison.Ordinal) ||
                name.StartsWith("list", StringComparison.Ordinal) ||
                name.StartsWith("load", StringComparison.Ordinal) ||
                httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) ||
                httpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
                return "Read";
            if (httpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
                httpMethod.Equals("PATCH", StringComparison.OrdinalIgnoreCase))
                return "Update";
            if (httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                return "Create";
            return null;
        }

        private static string Match(string name, params (string Token, string Service)[] matches)
        {
            foreach (var (token, service) in matches)
            {
                if (name.Contains(token, StringComparison.Ordinal))
                    return service;
            }
            return "";
        }

        private static string Normalize(string value) =>
            new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    public sealed class AccessPermissionService
    {
        private readonly LocalDbContext _context;

        public AccessPermissionService(LocalDbContext context)
        {
            _context = context;
        }

        public async Task<List<AccessPermissionValueDto>> GetEffectivePermissionsAsync(
            int? userId,
            int? roleId,
            int? departmentId,
            CancellationToken cancellationToken = default)
        {
            if (userId.HasValue)
            {
                var user = await _context.Users.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.User_ID == userId.Value, cancellationToken);
                if (user != null)
                {
                    roleId = user.Role_ID;
                    departmentId = user.Department_ID;
                }
            }

            var roleName = roleId.HasValue
                ? await _context.Roles.AsNoTracking()
                    .Where(item => item.Role_ID == roleId.Value)
                    .Select(item => item.Role_Name)
                    .FirstOrDefaultAsync(cancellationToken) ?? ""
                : "";
            var departmentName = departmentId.HasValue
                ? await _context.Departments.AsNoTracking()
                    .Where(item => item.Department_ID == departmentId.Value)
                    .Select(item => item.Department_Name)
                    .FirstOrDefaultAsync(cancellationToken) ?? ""
                : "";

            var rules = await _context.AccessPermissions.AsNoTracking()
                .Where(rule =>
                    (userId.HasValue && rule.User_ID == userId.Value) ||
                    (roleId.HasValue && rule.Role_ID == roleId.Value) ||
                    (departmentId.HasValue && rule.Department_ID == departmentId.Value))
                .ToListAsync(cancellationToken);

            var userRules = userId.HasValue
                ? ScopeRules(rules.Where(rule => rule.User_ID == userId.Value))
                : null;
            if (userRules is { Count: > 0 })
                return ToCompleteMatrix(userRules, roleName, departmentName);

            var hasRoleAndDepartment = roleId.HasValue && departmentId.HasValue;
            var combinedRules = hasRoleAndDepartment
                ? ScopeRules(rules.Where(rule =>
                    rule.User_ID == null &&
                    rule.Role_ID == roleId &&
                    rule.Department_ID == departmentId))
                : null;
            if (combinedRules is { Count: > 0 })
                return ToCompleteMatrix(combinedRules, roleName, departmentName);

            var roleRules = roleId.HasValue
                ? ScopeRules(rules.Where(rule =>
                    rule.User_ID == null &&
                    rule.Role_ID == roleId &&
                    rule.Department_ID == null))
                : null;
            var departmentRules = departmentId.HasValue
                ? ScopeRules(rules.Where(rule =>
                    rule.User_ID == null &&
                    rule.Role_ID == null &&
                    rule.Department_ID == departmentId))
                : null;

            return AccessPermissionCatalog.Services
                .SelectMany(service => AccessPermissionCatalog.Actions.Select(action =>
                {
                    var key = Key(service.ServiceKey, action);
                    var hasRoleRule = roleRules is { Count: > 0 };
                    var hasDepartmentRule = departmentRules is { Count: > 0 };
                    var allowed = hasRoleRule && hasDepartmentRule
                        ? ReadRule(roleRules!, key) && ReadRule(departmentRules!, key)
                        : hasRoleRule
                            ? ReadRule(roleRules!, key)
                            : hasDepartmentRule
                                ? ReadRule(departmentRules!, key)
                                : ModuleAccess.CanAccess(roleName, departmentName, service.ModuleKey);
                    return new AccessPermissionValueDto
                    {
                        ServiceKey = service.ServiceKey,
                        ActionKey = action,
                        Allowed = allowed
                    };
                }))
                .ToList();
        }

        public async Task<bool> IsAllowedAsync(
            int userId,
            string serviceKey,
            string actionKey,
            CancellationToken cancellationToken = default)
        {
            var permissions = await GetEffectivePermissionsAsync(userId, null, null, cancellationToken);
            return permissions.Any(permission =>
                permission.ServiceKey == serviceKey &&
                permission.ActionKey == actionKey &&
                permission.Allowed);
        }

        public static IReadOnlyCollection<string> GetReadableModules(
            IReadOnlyCollection<AccessPermissionValueDto> permissions,
            string roleName,
            string departmentName)
        {
            var modules = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "analytics" };
            foreach (var service in AccessPermissionCatalog.Services)
            {
                if (permissions.Any(permission =>
                    permission.ServiceKey == service.ServiceKey &&
                    permission.ActionKey == "Read" &&
                    permission.Allowed))
                {
                    modules.Add(service.ModuleKey);
                }
            }

            if (ModuleAccess.CanAccess(roleName, departmentName, "admin"))
                modules.Add("admin");
            return modules;
        }

        private static Dictionary<string, bool> ScopeRules(IEnumerable<AccessPermission> rules) =>
            rules.ToDictionary(rule => Key(rule.ServiceKey, rule.ActionKey), rule => rule.IsAllowed);

        private static bool ReadRule(IReadOnlyDictionary<string, bool> rules, string key) =>
            rules.TryGetValue(key, out var allowed) && allowed;

        private static List<AccessPermissionValueDto> ToCompleteMatrix(
            IReadOnlyDictionary<string, bool> rules,
            string roleName,
            string departmentName) =>
            AccessPermissionCatalog.Services
                .SelectMany(service => AccessPermissionCatalog.Actions.Select(action => new AccessPermissionValueDto
                {
                    ServiceKey = service.ServiceKey,
                    ActionKey = action,
                    Allowed = rules.TryGetValue(Key(service.ServiceKey, action), out var allowed)
                        ? allowed
                        : false
                }))
                .ToList();

        public static string Key(string serviceKey, string actionKey) =>
            $"{serviceKey}|{actionKey}";
    }
}
