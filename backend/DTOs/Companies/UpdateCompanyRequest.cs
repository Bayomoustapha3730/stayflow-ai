namespace StayFlow.Api.DTOs.Companies;

public sealed class UpdateCompanyRequest
{
    public string Name { get; init; } = string.Empty;
    public string? LegalName { get; init; }
    public string Email { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string CountryCode { get; init; } = "KE";
    public string TimeZone { get; init; } = "Africa/Nairobi";
    public bool IsActive { get; init; } = true;
}
