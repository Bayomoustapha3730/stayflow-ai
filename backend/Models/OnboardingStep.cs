namespace StayFlow.Api.Models;

public enum OnboardingStep
{
    Welcome = 1,
    OrganizationProfile = 2,
    PlanConfirmation = 3,
    FirstProperty = 4,
    TeamInvitations = 5,
    WhatsAppSetup = 6,
    AiProviderSetup = 7,
    KnowledgeBaseSetup = 8,
    DemoData = 9,
    Review = 10,
    Completed = 11
}

public enum OnboardingStepState
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2,
    Skipped = 3,
    Blocked = 4
}

public static class OnboardingStepExtensions
{
    public static string ToStorageValue(this OnboardingStep step)
    {
        return step.ToString();
    }

    public static bool TryParse(string? value, out OnboardingStep step)
    {
        return Enum.TryParse(value, true, out step);
    }

    public static int Rank(this OnboardingStep step)
    {
        return (int)step;
    }
}