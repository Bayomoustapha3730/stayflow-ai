namespace StayFlow.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    private const int MaximumLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await next(context);
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values)
            && !string.IsNullOrWhiteSpace(values.FirstOrDefault()))
        {
            var candidate = values.First()!.Trim();
            if (IsValidCorrelationId(candidate))
            {
                return candidate;
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    private static bool IsValidCorrelationId(string value)
    {
        if (value.Length is 0 or > MaximumLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            var isAllowed = char.IsLetterOrDigit(character)
                || character is '-' or '_' or '.';
            if (!isAllowed)
            {
                return false;
            }
        }

        return true;
    }
}
