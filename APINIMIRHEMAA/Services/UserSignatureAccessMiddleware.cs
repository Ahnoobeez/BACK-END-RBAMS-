using APINIMIRHEMAA.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace APINIMIRHEMAA.Services
{
    // Provides reusable application operations for user signature access middleware workflows.
    public sealed class UserSignatureAccessMiddleware
    {
        // Injected or cached next dependency/state used by this class.
        private readonly RequestDelegate _next;

        // Initializes a new user signature access middleware instance.
        public UserSignatureAccessMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        // Validates the authenticated account and required signature before continuing the request pipeline.
        public async Task InvokeAsync(HttpContext context, LocalDbContext db)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var isSignatureEndpoint =
                string.Equals(path, "/api/UserSignature/me", StringComparison.OrdinalIgnoreCase) &&
                (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsPost(context.Request.Method));
            var isSessionPresenceEndpoint =
                (string.Equals(path, "/api/Auth/session-heartbeat", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(path, "/api/Auth/session-end", StringComparison.OrdinalIgnoreCase)) &&
                HttpMethods.IsPost(context.Request.Method);
            var isDeliveryReceiptApprovalReadEndpoint =
                HttpMethods.IsGet(context.Request.Method) &&
                (string.Equals(path, "/api/Production/GetDeliveryReceiptApprovalQueue", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(path, "/api/Production/GetDeliveryReceiptApprovalItems", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(path, "/api/Production/GetDeliveryReceiptApprovalSignatures", StringComparison.OrdinalIgnoreCase));

            // Enter this branch when `!path.StartsWith(, StringComparison.OrdinalIgnoreCase) || context.User.Identity?.IsAuthenticated != true` is true.
            if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
                context.User.Identity?.IsAuthenticated != true)
            {
                await _next(context);
                return;
            }

            // Return HTTP 401 Unauthorized when `!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)` is true.
            if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new { message = "Your session is invalid. Please sign in again." },
                    context.RequestAborted);
                return;
            }

            var isSessionEndEndpoint =
                string.Equals(path, "/api/Auth/session-end", StringComparison.OrdinalIgnoreCase) &&
                HttpMethods.IsPost(context.Request.Method);
            var isActiveAccount = await db.Users
                .AsNoTracking()
                .AnyAsync(user => user.User_ID == userId && user.IsActive, context.RequestAborted);
            // Return HTTP 401 Unauthorized when `!isActiveAccount && !isSessionEndEndpoint` is true.
            if (!isActiveAccount && !isSessionEndEndpoint)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new { message = "This account is inactive. Please contact an administrator." },
                    context.RequestAborted);
                return;
            }

            // Enter this branch when `isSignatureEndpoint || isSessionPresenceEndpoint || isDeliveryReceiptApprovalReadEndpoint` is true.
            if (isSignatureEndpoint || isSessionPresenceEndpoint || isDeliveryReceiptApprovalReadEndpoint)
            {
                await _next(context);
                return;
            }

            var hasSignature = await db.UserSignatures
                .AsNoTracking()
                .AnyAsync(signature =>
                    signature.User_ID == userId &&
                    signature.SignatureData.Length > 0,
                    context.RequestAborted);

            // Return HTTP 403 Forbidden when `!hasSignature` is true.
            if (!hasSignature)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    new { message = "Upload a signature in Profile Settings before using the system." },
                    context.RequestAborted);
                return;
            }

            await _next(context);
        }
    }
}
