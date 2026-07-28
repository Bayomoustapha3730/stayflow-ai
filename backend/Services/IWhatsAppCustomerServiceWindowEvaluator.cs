namespace StayFlow.Api.Services;

public sealed class WhatsAppCustomerServiceWindowEvaluation
{
    public bool IsOpen { get; init; }
    public DateTimeOffset? LastInboundAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public interface IWhatsAppCustomerServiceWindowEvaluator
{
    Task<WhatsAppCustomerServiceWindowEvaluation> EvaluateAsync(Guid companyId, Guid conversationId, CancellationToken cancellationToken);
}
