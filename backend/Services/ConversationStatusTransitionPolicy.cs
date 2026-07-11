using StayFlow.Api.Models;

namespace StayFlow.Api.Services;

public interface IConversationStatusTransitionPolicy
{
    bool CanStoreMessage(Conversation conversation, ConversationSenderType senderType);
    bool CanTransition(ConversationStatus currentStatus, ConversationStatus targetStatus);
}

public sealed class ConversationStatusTransitionPolicy : IConversationStatusTransitionPolicy
{
    public bool CanStoreMessage(Conversation conversation, ConversationSenderType senderType)
    {
        if (conversation.Status == ConversationStatus.Closed)
        {
            return false;
        }

        if (conversation.Status == ConversationStatus.HumanManaged && senderType == ConversationSenderType.AI)
        {
            return false;
        }

        return true;
    }

    public bool CanTransition(ConversationStatus currentStatus, ConversationStatus targetStatus)
    {
        if (currentStatus == targetStatus)
        {
            return true;
        }

        return currentStatus switch
        {
            ConversationStatus.Open => targetStatus is ConversationStatus.AwaitingGuest
                or ConversationStatus.AwaitingHost
                or ConversationStatus.Escalated
                or ConversationStatus.HumanManaged
                or ConversationStatus.Resolved
                or ConversationStatus.Closed,
            ConversationStatus.AwaitingGuest => targetStatus is ConversationStatus.Open
                or ConversationStatus.Escalated
                or ConversationStatus.HumanManaged
                or ConversationStatus.Resolved
                or ConversationStatus.Closed,
            ConversationStatus.AwaitingHost => targetStatus is ConversationStatus.Open
                or ConversationStatus.Escalated
                or ConversationStatus.HumanManaged
                or ConversationStatus.Resolved
                or ConversationStatus.Closed,
            ConversationStatus.Escalated => targetStatus is ConversationStatus.HumanManaged
                or ConversationStatus.Open
                or ConversationStatus.Resolved
                or ConversationStatus.Closed,
            ConversationStatus.HumanManaged => targetStatus is ConversationStatus.Open
                or ConversationStatus.Resolved
                or ConversationStatus.Closed,
            ConversationStatus.Resolved => targetStatus is ConversationStatus.Open
                or ConversationStatus.Closed,
            ConversationStatus.Closed => false,
            _ => false
        };
    }
}
