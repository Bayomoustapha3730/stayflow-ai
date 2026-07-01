using StayFlow.Api.Authorization;

namespace StayFlow.Api.Middleware;

public sealed class PermissionAuthorizationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var permission = context.GetEndpoint()?.Metadata.GetMetadata<RequiresPermissionAttribute>()?.Permission;
        if (permission is not null && !context.User.Claims.Any(claim => claim.Type == "permission" && claim.Value == permission))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(Common.ApiResponse<object>.Fail("Permission denied."));
            return;
        }

        await next(context);
    }
}
