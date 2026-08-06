namespace StayFlow.Api.Models;

public sealed class UsageRecord : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Metric { get; set; } = string.Empty;
    public DateTimeOffset PeriodStartUtc { get; set; }
    public DateTimeOffset PeriodEndUtc { get; set; }
    public long QuantityUsed { get; set; }

    public Company Company { get; set; } = null!;
}