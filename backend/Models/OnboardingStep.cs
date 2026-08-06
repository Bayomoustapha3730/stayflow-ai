namespace StayFlow.Api.Models;

public enum OnboardingStep
{
    AccountCreated = 1,
    OrganizationCreated = 2,
    PlanSelected = 3,
    FirstPropertyCreated = 4,
    TeammatesInvited = 5,
    Completed = 6
}

public static class OnboardingStepExtensions
{
    public static string ToStorageValue(this OnboardingStep step)
    {
        return step.ToString();
    }
}