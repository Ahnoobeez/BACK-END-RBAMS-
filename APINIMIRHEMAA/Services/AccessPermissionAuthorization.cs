using APINIMIRHEMAA.DTO.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace APINIMIRHEMAA.Services
{
    public static class RecordAccessScope
    {
        public const string SystemAdministrator = "RecordAccessScope.SystemAdministrator";
        public const string Module = "RecordAccessScope.Module";
        public const string RoleName = "RecordAccessScope.RoleName";
        public const string DepartmentName = "RecordAccessScope.DepartmentName";
        public const string DepartmentId = "RecordAccessScope.DepartmentId";
        public const string DepartmentManager = "RecordAccessScope.DepartmentManager";
    }

    public sealed class ModuleAccessRequirement : IAuthorizationRequirement
    {
        public ModuleAccessRequirement(string module)
        {
            Module = module;
        }

        public string Module { get; }
    }

    public sealed class ModuleAccessAuthorizationHandler : AuthorizationHandler<ModuleAccessRequirement>
    {
        private readonly AccessPermissionService _permissions;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ModuleAccessAuthorizationHandler(
            AccessPermissionService permissions,
            IHttpContextAccessor httpContextAccessor)
        {
            _permissions = permissions;
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ModuleAccessRequirement requirement)
        {
            var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
                return;

            var identity = await _permissions.GetCurrentIdentityAsync(userId);
            if (identity == null)
                return;

            var isSystemAdministrator = ModuleAccess.IsAdministratorRole(identity.RoleName);
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                httpContext.Items[RecordAccessScope.SystemAdministrator] = isSystemAdministrator;
                httpContext.Items[RecordAccessScope.RoleName] = identity.RoleName;
                httpContext.Items[RecordAccessScope.DepartmentName] = identity.DepartmentName;
                httpContext.Items[RecordAccessScope.DepartmentId] = identity.DepartmentId;
                httpContext.Items[RecordAccessScope.DepartmentManager] =
                    ModuleAccess.IsDepartmentManagerRole(identity.RoleName);
            }

            if (isSystemAdministrator)
            {
                context.Succeed(requirement);
                return;
            }
            if (string.Equals(requirement.Module, "admin", StringComparison.OrdinalIgnoreCase))
                return;

            var permissions = await _permissions.GetEffectivePermissionsAsync(userId, null, null);
            var readableModules = AccessPermissionService.GetReadableModules(
                permissions, false);

            var allowed = requirement.Module == "clients"
                ? new[] { "marketing", "technical", "production", "admin" }
                    .Any(module => readableModules.Contains(module))
                : readableModules.Contains(requirement.Module);
            if (allowed)
            {
                if (httpContext != null)
                httpContext.Items[RecordAccessScope.Module] = true;
                context.Succeed(requirement);
            }
        }
    }

    public sealed class AccessPermissionAuthorizationFilter : IAsyncActionFilter
    {
        private readonly AccessPermissionService _permissions;

        public AccessPermissionAuthorizationFilter(AccessPermissionService permissions)
        {
            _permissions = permissions;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            if (user.Identity?.IsAuthenticated != true ||
                context.ActionDescriptor is not ControllerActionDescriptor actionDescriptor)
            {
                await next();
                return;
            }

            if (!AccessPermissionCatalog.TryResolveService(
                    actionDescriptor.ControllerName,
                    actionDescriptor.ActionName,
                    out var serviceKey))
            {
                if (IsExplicitlySystemAdministratorOnly(actionDescriptor))
                {
                    var explicitAdministratorId = user.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (int.TryParse(explicitAdministratorId, out var explicitAdministratorUserId) &&
                        await _permissions.IsSystemAdministratorAsync(explicitAdministratorUserId))
                    {
                        context.HttpContext.Items[RecordAccessScope.SystemAdministrator] = true;
                        await next();
                    }
                    else
                    {
                        context.Result = new ForbidResult();
                    }
                    return;
                }

                if (!AccessPermissionCatalog.IsPermissionManagedController(
                        actionDescriptor.ControllerName))
                {
                    await next();
                    return;
                }

                var administratorId = user.FindFirstValue(ClaimTypes.NameIdentifier);
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
            if (actionKey == null || !int.TryParse(userIdValue, out var userId))
            {
                context.Result = new ForbidResult();
                return;
            }

            var allowed = false;
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
            else
            {
                allowed = await _permissions.IsAllowedAsync(userId, serviceKey, actionKey);
            }

            if (!allowed)
            {
                context.Result = new ForbidResult();
                return;
            }

            context.HttpContext.Items[RecordAccessScope.Module] = true;
            await next();
        }

        private static bool IsExplicitlySystemAdministratorOnly(
            ControllerActionDescriptor actionDescriptor)
        {
            var authorizationData = actionDescriptor.EndpointMetadata
                .OfType<IAuthorizeData>()
                .ToList();
            return authorizationData.Any(data =>
                    string.Equals(data.Policy, "admin", StringComparison.OrdinalIgnoreCase)) ||
                authorizationData
                    .Where(data => !string.IsNullOrWhiteSpace(data.Roles))
                    .Any(data => data.Roles!
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .All(ModuleAccess.IsAdministratorRole));
        }
    }
}
