using APINIMIRHEMAA.DTO.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace APINIMIRHEMAA.Services
{
    // Provides reusable application operations for record access scope workflows.
    public static class RecordAccessScope
    {
        public const string SystemAdministrator = "RecordAccessScope.SystemAdministrator";
        public const string Module = "RecordAccessScope.Module";
        public const string RoleName = "RecordAccessScope.RoleName";
        public const string DepartmentName = "RecordAccessScope.DepartmentName";
        public const string DepartmentId = "RecordAccessScope.DepartmentId";
        public const string DepartmentManager = "RecordAccessScope.DepartmentManager";
    }

    // Provides reusable application operations for module access requirement workflows.
    public sealed class ModuleAccessRequirement : IAuthorizationRequirement
    {
        // Initializes a new module access requirement instance.
        public ModuleAccessRequirement(string module)
        {
            Module = module;
        }

        public string Module { get; }
    }

    // Provides reusable application operations for module access authorization handler workflows.
    public sealed class ModuleAccessAuthorizationHandler : AuthorizationHandler<ModuleAccessRequirement>
    {
        // Injected or cached permissions dependency/state used by this class.
        private readonly AccessPermissionService _permissions;
        // Database context used by this class to query and persist application records.
        private readonly IHttpContextAccessor _httpContextAccessor;

        // Initializes a new module access authorization handler instance.
        public ModuleAccessAuthorizationHandler(
            AccessPermissionService permissions,
            IHttpContextAccessor httpContextAccessor)
        {
            _permissions = permissions;
            _httpContextAccessor = httpContextAccessor;
        }

        // Handles the requirement.
        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ModuleAccessRequirement requirement)
        {
            var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Exit the current operation when `!int.TryParse(userIdValue, out var userId)` is true.
            if (!int.TryParse(userIdValue, out var userId))
                return;

            var identity = await _permissions.GetCurrentIdentityAsync(userId);
            // Exit the current operation when `identity == null` is true.
            if (identity == null)
                return;

            var isSystemAdministrator = ModuleAccess.IsAdministratorRole(identity.RoleName);
            var httpContext = _httpContextAccessor.HttpContext;
            // Enter this branch when `httpContext != null` is true.
            if (httpContext != null)
            {
                httpContext.Items[RecordAccessScope.SystemAdministrator] = isSystemAdministrator;
                httpContext.Items[RecordAccessScope.RoleName] = identity.RoleName;
                httpContext.Items[RecordAccessScope.DepartmentName] = identity.DepartmentName;
                httpContext.Items[RecordAccessScope.DepartmentId] = identity.DepartmentId;
                httpContext.Items[RecordAccessScope.DepartmentManager] =
                    ModuleAccess.IsDepartmentManagerRole(identity.RoleName);
            }

            // Enter this branch when `isSystemAdministrator` is true.
            if (isSystemAdministrator)
            {
                context.Succeed(requirement);
                return;
            }
            // Exit the current operation when `string.Equals(requirement.Module, , StringComparison.OrdinalIgnoreCase)` is true.
            if (string.Equals(requirement.Module, "admin", StringComparison.OrdinalIgnoreCase))
                return;

            var permissions = await _permissions.GetEffectivePermissionsAsync(userId, null, null);
            var accessibleModules = AccessPermissionService.GetAccessibleModules(
                permissions, false);

            var allowed = requirement.Module == "clients"
                ? new[] { "marketing", "technical", "production", "admin" }
                    .Any(module => accessibleModules.Contains(module))
                : accessibleModules.Contains(requirement.Module);
            // Enter this branch when `allowed` is true.
            if (allowed)
            {
                // Enter this branch when `httpContext != null` is true.
                if (httpContext != null)
                httpContext.Items[RecordAccessScope.Module] = true;
                context.Succeed(requirement);
            }
        }
    }

    // Provides reusable application operations for access permission authorization filter workflows.
    public sealed class AccessPermissionAuthorizationFilter : IAsyncActionFilter
    {
        // Injected or cached permissions dependency/state used by this class.
        private readonly AccessPermissionService _permissions;

        // Initializes a new access permission authorization filter instance.
        public AccessPermissionAuthorizationFilter(AccessPermissionService permissions)
        {
            _permissions = permissions;
        }

        // Handles the on action execution lifecycle event.
        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            // Enter this branch when `user.Identity?.IsAuthenticated != true || context.ActionDescriptor is not ControllerActionDescriptor actionDescriptor` is true.
            if (user.Identity?.IsAuthenticated != true ||
                context.ActionDescriptor is not ControllerActionDescriptor actionDescriptor)
            {
                await next();
                return;
            }

            // Enter this branch when `actionDescriptor.ControllerName == && actionDescriptor.ActionName ==` is true.
            if (actionDescriptor.ControllerName == "Analytics" &&
                actionDescriptor.ActionName == "GetCompanyOverview")
            {
                await next();
                return;
            }

            // Enter this branch when `actionDescriptor.ControllerName == && actionDescriptor.ActionName ==` is true.
            if (actionDescriptor.ControllerName == "Analytics" &&
                actionDescriptor.ActionName == "GetMarketingOverview")
            {
                var marketingUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                // Enter this branch when `int.TryParse(marketingUserId, out var marketingUserIdValue)` is true.
                if (int.TryParse(marketingUserId, out var marketingUserIdValue))
                {
                    var permissions = await _permissions.GetEffectivePermissionsAsync(
                        marketingUserIdValue, null, null);
                    // Enter this branch when `AccessPermissionService.GetReadableModules(permissions, false) .Contains()` is true.
                    if (AccessPermissionService.GetReadableModules(permissions, false)
                        .Contains("marketing"))
                    {
                        context.HttpContext.Items[RecordAccessScope.Module] = true;
                        await next();
                        return;
                    }
                }

                context.Result = new ForbidResult();
                return;
            }

            // Enter this branch when `!AccessPermissionCatalog.TryResolveService( actionDescriptor.ControllerName, actionDescriptor.ActionName, out var serviceKey)` is true.
            if (!AccessPermissionCatalog.TryResolveService(
                    actionDescriptor.ControllerName,
                    actionDescriptor.ActionName,
                    out var serviceKey))
            {
                // Enter this branch when `IsExplicitlySystemAdministratorOnly(actionDescriptor)` is true.
                if (IsExplicitlySystemAdministratorOnly(actionDescriptor))
                {
                    var explicitAdministratorId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                    // Enter this branch when `int.TryParse(explicitAdministratorId, out var explicitAdministratorUserId) && await _permissions.IsSystemAdministratorAsync(explicitAdministratorUserId)` is true.
                    if (int.TryParse(explicitAdministratorId, out var explicitAdministratorUserId) &&
                        await _permissions.IsSystemAdministratorAsync(explicitAdministratorUserId))
                    {
                        context.HttpContext.Items[RecordAccessScope.SystemAdministrator] = true;
                        await next();
                    }
                    // Use this fallback path when the preceding condition is false.
                    else
                    {
                        context.Result = new ForbidResult();
                    }
                    return;
                }

                // Enter this branch when `!AccessPermissionCatalog.IsPermissionManagedController( actionDescriptor.ControllerName)` is true.
                if (!AccessPermissionCatalog.IsPermissionManagedController(
                        actionDescriptor.ControllerName))
                {
                    await next();
                    return;
                }

                var administratorId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                // Enter this branch when `int.TryParse(administratorId, out var administratorUserId) && await _permissions.IsSystemAdministratorAsync(administratorUserId)` is true.
                if (int.TryParse(administratorId, out var administratorUserId) &&
                    await _permissions.IsSystemAdministratorAsync(administratorUserId))
                {
                    context.HttpContext.Items[RecordAccessScope.SystemAdministrator] = true;
                    context.HttpContext.Items[RecordAccessScope.Module] = true;
                    await next();
                    return;
                }

                context.Result = new ForbidResult();
                return;
            }

            var actionKey = AccessPermissionCatalog.ResolveAction(
                actionDescriptor.ActionName,
                context.HttpContext.Request.Method);
            var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
            // Return HTTP 403 Forbidden when `actionKey == null || !int.TryParse(userIdValue, out var userId)` is true.
            if (actionKey == null || !int.TryParse(userIdValue, out var userId))
            {
                context.Result = new ForbidResult();
                return;
            }

            var allowed = false;
            // Enter this branch when `actionDescriptor.ControllerName == && actionKey ==` is true.
            if (actionDescriptor.ControllerName == "Clients" && actionKey == "Read")
            {
                var matrix = await _permissions.GetEffectivePermissionsAsync(userId, null, null);
                var clientReadServices = new[]
                {
                    "marketing.create-client",
                    "technical.job-order",
                    "production.job-order"
                };
                allowed = matrix.Any(permission =>
                    clientReadServices.Contains(permission.ServiceKey, StringComparer.Ordinal) &&
                    permission.ActionKey == "Read" &&
                    permission.Allowed);
            }
            // Use this fallback path when the preceding condition is false.
            else
            {
                allowed = await _permissions.IsAllowedAsync(userId, serviceKey, actionKey);
            }

            // Return HTTP 403 Forbidden when `!allowed` is true.
            if (!allowed)
            {
                context.Result = new ForbidResult();
                return;
            }

            context.HttpContext.Items[RecordAccessScope.Module] = true;
            await next();
        }

        // Checks whether the action metadata restricts the endpoint to system administrators.
        private static bool IsExplicitlySystemAdministratorOnly(
            ControllerActionDescriptor actionDescriptor)
        {
            var authorizationData = actionDescriptor.EndpointMetadata
                .OfType<IAuthorizeData>()
                .ToList();
            return authorizationData.Any(data =>
                    string.Equals(data.Policy, "admin", StringComparison.OrdinalIgnoreCase)) ||
                authorizationData
                    // Filter the query to rows satisfying `.Where(data => !string.IsNullOrWhiteSpace(data.Roles)) .Any(data => data.Roles! .Split(, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) .All(ModuleAccess.IsAdministratorRole)); } } }` before materializing the results.
                    .Where(data => !string.IsNullOrWhiteSpace(data.Roles))
                    .Any(data => data.Roles!
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .All(ModuleAccess.IsAdministratorRole));
        }
    }
}
