namespace StayFlow.Api.Services;

public interface ITenantExecutionContextAccessor
{
    Guid? CompanyId { get; }
    Guid? UserId { get; }
    string? CorrelationId { get; }
    bool IsAuthenticated { get; }

    void Set(Guid companyId, Guid? userId, string? correlationId);
    void Clear();
}