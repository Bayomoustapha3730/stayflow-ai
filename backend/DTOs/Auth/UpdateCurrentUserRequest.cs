namespace StayFlow.Api.DTOs.Auth;

public sealed class UpdateCurrentUserRequest
{
    public string FullName { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string PreferredLanguage { get; init; } = "en";
    public string TimeZone { get; init; } = "UTC";
    public bool EmailNotificationsEnabled { get; init; } = true;
    public bool SecurityNotificationsEnabled { get; init; } = true;
    public bool ProductUpdatesEnabled { get; init; }
}