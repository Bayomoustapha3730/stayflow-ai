namespace StayFlow.Api.Services;

public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? CompanyId { get; }
    Guid? UserId { get; }
    string? CorrelationId { get; }
    bool IsAuthenticated { get; }
}