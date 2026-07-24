namespace StayFlow.Api.Services.AI.Orchestration;

public sealed record AIReplyOrchestrationWarning(
    string Code,
    string Message,
    string Severity = "warning");
