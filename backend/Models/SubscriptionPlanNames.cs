namespace StayFlow.Api.Models;

public static class SubscriptionPlanNames
{
    public const string Free = "Free";
    public const string Starter = "Starter";
    public const string Professional = "Professional";
    public const string Enterprise = "Enterprise";

    public static IReadOnlyCollection<string> Canonical { get; } =
    [
        Free,
        Starter,
        Professional,
        Enterprise
    ];
}