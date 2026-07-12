export enum GuestChannel {
  Web = 0,
  WhatsApp = 1,
  Sms = 2,
  Email = 3
}

export enum ConversationStatus {
  Open = 0,
  AwaitingGuest = 1,
  AwaitingHost = 2,
  Escalated = 3,
  HumanManaged = 4,
  Resolved = 5,
  Closed = 6
}

export enum ConversationSenderType {
  Guest = 0,
  AI = 1,
  Host = 2,
  System = 3
}

export enum ConversationMessageType {
  Text = 0,
  SystemEvent = 1,
  Escalation = 2,
  InternalNote = 3
}

export function statusLabel(status: ConversationStatus): string {
  switch (status) {
    case ConversationStatus.AwaitingHost:
    case ConversationStatus.Escalated:
    case ConversationStatus.HumanManaged:
      return "A host will respond shortly";
    case ConversationStatus.Closed:
      return "Conversation closed";
    case ConversationStatus.AwaitingGuest:
      return "Waiting for your reply";
    case ConversationStatus.Resolved:
      return "Resolved";
    default:
      return "AI assistant";
  }
}

export function senderLabel(sender: ConversationSenderType): string {
  switch (sender) {
    case ConversationSenderType.Guest:
      return "You";
    case ConversationSenderType.Host:
      return "Guest Services";
    case ConversationSenderType.System:
      return "StayFlow";
    default:
      return "StayFlow Concierge";
  }
}

export function requiresHostAttention(status: ConversationStatus, humanTakeoverEnabled: boolean): boolean {
  return (
    humanTakeoverEnabled ||
    status === ConversationStatus.AwaitingHost ||
    status === ConversationStatus.Escalated ||
    status === ConversationStatus.HumanManaged
  );
}
