using APINIMIRHEMAA.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace APINIMIRHEMAA.Services
{
    public sealed class UserSignatureAccessMiddleware
    {
        private readonly RequestDelegate _next;

        public UserSignatureAccessMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, LocalDbContext db)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var isSignatureEndpoint =
                string.Equals(path, "/api/UserSignature/me", StringComparison.OrdinalIgnoreCase) &&
                (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsPost(context.Request.Method));

            if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
                isSignatureEndpoint ||
                context.User.Identity?.IsAuthenticated != true)
            {
                await _next(context);
                return;
            }

            if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    new { message = "Your session is invalid. Please sign in again." },
                    context.RequestAborted);
                return;
            }

            var hasSignature = await db.UserSignatures
                .AsNoTracking()
                .AnyAsync(signature =>
                    signature.User_ID == userId &&
                    signature.SignatureData.Length > 0,
                    context.RequestAborted);

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
