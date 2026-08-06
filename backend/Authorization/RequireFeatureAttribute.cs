namespace StayFlow.Api.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireFeatureAttribute(string featureKey) : Attribute
{
    public string FeatureKey { get; } = featureKey;
}