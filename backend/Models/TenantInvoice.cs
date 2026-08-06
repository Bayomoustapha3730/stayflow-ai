namespace StayFlow.Api.Models;

public sealed class TenantInvoice : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string ExternalInvoiceId { get; set; } = string.Empty;
    public string? ExternalCustomerId { get; set; }
    public string? ExternalSubscriptionId { get; set; }
    public string Status { get; set; } = "Open";
    public long AmountDue { get; set; }
    public long AmountPaid { get; set; }
    public string Currency { get; set; } = "usd";
    public DateTimeOffset? PeriodStartUtc { get; set; }
    public DateTimeOffset? PeriodEndUtc { get; set; }
    public DateTimeOffset? PaidAtUtc { get; set; }
    public DateTimeOffset? FailedAtUtc { get; set; }

    public Company Company { get; set; } = null!;
}