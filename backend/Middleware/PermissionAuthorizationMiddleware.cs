using StayFlow.Api.Authorization;
using StayFlow.Api.Exceptions;

namespace StayFlow.Api.Middleware;

public sealed class PermissionAuthorizationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var permission = context.GetEndpoint()?.Metadata.GetMetadata<RequiresPermissionAttribute>()?.Permission;
        if (permission is not null && !context.User.Claims.Any(claim => claim.Type == "permission" && claim.Value == permission))
        {
            throw new ForbiddenOperationException("Permission denied.", "permission_denied");
        }

        await next(context);
    }
}
