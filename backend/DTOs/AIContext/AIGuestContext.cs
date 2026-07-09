namespace StayFlow.Api.DTOs.AIContext;

public sealed class AIGuestContext
{
    public string PreferredLanguage { get; init; } = "en";
    public bool IsReturningGuest { get; init; }
    public string Limitation { get; init; } = "Structured guest preferences are not implemented yet; private operational notes and inferred preferences are excluded.";
}
