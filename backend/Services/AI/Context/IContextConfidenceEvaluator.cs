namespace StayFlow.Api.Services.AI.Context;

public interface IContextConfidenceEvaluator
{
    ContextConfidenceResult Evaluate(ConversationContext context);
}
