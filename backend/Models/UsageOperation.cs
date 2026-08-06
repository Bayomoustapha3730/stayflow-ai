namespace StayFlow.Api.Models;

public sealed class UsageOperation : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Metric { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTimeOffset PeriodStartUtc { get; set; }
    public long Quantity { get; set; }

    public Company Company { get; set; } = null!;
}