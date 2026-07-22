using Microsoft.AspNetCore.SignalR;
using StayFlow.Api.Hubs;

namespace StayFlow.Api.Services;

public sealed class ConversationRealtimePublisher(IHubContext<ConversationHub> hubContext) : IConversationRealtimePublisher
{
    public Task PublishMessageCreatedAsync(Guid companyId, Guid conversationId, object payload, bool internalOnly, CancellationToken cancellationToken)
    {
        if (internalOnly)
        {
            return hubContext.Clients.Group(ConversationHubChannels.HostCompanyGroup(companyId))
                .SendCoreAsync("ConversationMessageCreated", [payload], cancellationToken);
        }

        return hubContext.Clients.Group(ConversationHubChannels.ConversationGroup(conversationId))
            .SendCoreAsync("ConversationMessageCreated", [payload], cancellationToken);
    }

    public Task PublishTypingStartedAsync(Guid companyId, Guid conversationId, object payload, bool hostOnly, CancellationToken cancellationToken)
    {
        if (hostOnly)
        {
            return hubContext.Clients.Group(ConversationHubChannels.HostCompanyGroup(companyId))
                .SendCoreAsync("TypingStarted", [payload], cancellationToken);
        }

        return hubContext.Clients.Group(ConversationHubChannels.ConversationGroup(conversationId))
            .SendCoreAsync("TypingStarted", [payload], cancellationToken);
    }

    public Task PublishTypingStoppedAsync(Guid companyId, Guid conversationId, object payload, bool hostOnly, CancellationToken cancellationToken)
    {
        if (hostOnly)
        {
            return hubContext.Clients.Group(ConversationHubChannels.HostCompanyGroup(companyId))
                .SendCoreAsync("TypingStopped", [payload], cancellationToken);
        }

        return hubContext.Clients.Group(ConversationHubChannels.ConversationGroup(conversationId))
            .SendCoreAsync("TypingStopped", [payload], cancellationToken);
    }

    public async Task PublishConversationAssignedAsync(Guid companyId, Guid conversationId, object payload, CancellationToken cancellationToken)
    {
        await hubContext.Clients.Group(ConversationHubChannels.HostCompanyGroup(companyId))
            .SendCoreAsync("ConversationAssigned", [payload], cancellationToken);
        await hubContext.Clients.Group(ConversationHubChannels.ConversationGroup(conversationId))
            .SendCoreAsync("ConversationAssigned", [payload], cancellationToken);
    }

    public Task PublishConversationReadStateChangedAsync(Guid companyId, Guid conversationId, object payload, CancellationToken cancellationToken)
    {
        return hubContext.Clients.Group(ConversationHubChannels.HostCompanyGroup(companyId))
            .SendCoreAsync("ConversationReadStateChanged", [payload], cancellationToken);
    }

    public Task PublishConversationUnreadCountChangedAsync(Guid companyId, object payload, CancellationToken cancellationToken)
    {
        return hubContext.Clients.Group(ConversationHubChannels.HostCompanyGroup(companyId))
            .SendCoreAsync("ConversationUnreadCountChanged", [payload], cancellationToken);
    }
}
