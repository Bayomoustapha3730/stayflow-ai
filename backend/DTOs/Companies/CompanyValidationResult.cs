namespace StayFlow.Api.DTOs.Companies;

public sealed class CompanyValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<string> Errors { get; } = [];
}
