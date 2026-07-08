namespace StayFlow.Api.Services;

public interface ICurrentTenantContext
{
    Guid? CompanyId { get; }
    Guid? UserId { get; }
    string? CorrelationId { get; }
    bool IsAuthenticated { get; }
}
