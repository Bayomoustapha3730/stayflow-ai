using StayFlow.Api.Services.AI.Context;

namespace StayFlow.Api.Services.AI.Intent;

public interface IGuestIntentDetector
{
    GuestIntentResult Detect(ConversationContext context);
}
