using APINIMIRHEMAA.DTO.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace APINIMIRHEMAA.Services
{
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

        public ModuleAccessAuthorizationHandler(AccessPermissionService permissions)
        {
            _permissions = permissions;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ModuleAccessRequirement requirement)
        {
            var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
                return;

            var roleName = context.User.FindFirstValue(ClaimTypes.Role) ?? "";
            var departmentName = context.User.FindFirstValue("Department") ?? "";
            var permissions = await _permissions.GetEffectivePermissionsAsync(userId, null, null);
            var readableModules = AccessPermissionService.GetReadableModules(
                permissions, roleName, departmentName);

            var allowed = requirement.Module == "clients"
                ? new[] { "marketing", "technical", "production", "admin" }
                    .Any(module => readableModules.Contains(module))
                : readableModules.Contains(requirement.Module);
            if (allowed)
                context.Succeed(requirement);
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
                context.ActionDescriptor is not ControllerActionDescriptor actionDescriptor ||
                !AccessPermissionCatalog.TryResolveService(
                    actionDescriptor.ControllerName,
                    actionDescriptor.ActionName,
                    out var serviceKey))
            {
                await next();
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

            await next();
        }
    }
}
