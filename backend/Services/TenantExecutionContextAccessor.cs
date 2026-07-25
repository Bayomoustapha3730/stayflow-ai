namespace StayFlow.Api.Services;

public sealed class TenantExecutionContextAccessor : ITenantExecutionContextAccessor
{
    public Guid? CompanyId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? CorrelationId { get; private set; }
    public bool IsAuthenticated { get; private set; }

    public void Set(Guid companyId, Guid? userId, string? correlationId)
    {
        CompanyId = companyId;
        UserId = userId;
        CorrelationId = correlationId;
        IsAuthenticated = companyId != Guid.Empty;
    }

    public void Clear()
    {
        CompanyId = null;
        UserId = null;
        CorrelationId = null;
        IsAuthenticated = false;
    }
}