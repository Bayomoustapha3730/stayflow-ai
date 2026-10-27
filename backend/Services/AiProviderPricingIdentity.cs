namespace StayFlow.Api.Services;

public static class AiProviderPricingIdentity
{
    // Provider names are canonicalized; pricing model keys remain exact and case-sensitive.
    public static string NormalizeProvider(string provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.Trim().ToUpperInvariant();
    }
}
