namespace StayFlow.Api.Hubs;

public static class ConversationHubChannels
{
    public static string ConversationGroup(Guid conversationId) => $"conversation:{conversationId:D}";

    public static string HostCompanyGroup(Guid companyId) => $"company:{companyId:D}:hosts";
}
