namespace StayFlow.Api.DTOs.Auth;

public sealed class CurrentUserDto
{
    public Guid Id { get; init; }
    public Guid CompanyId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string PreferredLanguage { get; init; } = "en";
    public string TimeZone { get; init; } = "UTC";
    public bool IsEmailVerified { get; init; }
    public bool EmailNotificationsEnabled { get; init; }
    public bool SecurityNotificationsEnabled { get; init; }
    public bool ProductUpdatesEnabled { get; init; }
    public string? OrganizationRole { get; init; }
    public IReadOnlyCollection<string> Roles { get; init; } = [];
    public IReadOnlyCollection<string> Permissions { get; init; } = [];
}
