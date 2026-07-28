export enum ConversationMessageDeliveryStatus {
  Pending = 0,
  Sent = 1,
  Delivered = 2,
  Read = 3,
  Failed = 4
}

export function deliveryStatusLabel(status?: ConversationMessageDeliveryStatus | null): string | null {
  switch (status) {
    case ConversationMessageDeliveryStatus.Pending:
      return "Pending";
    case ConversationMessageDeliveryStatus.Sent:
      return "Sent";
    case ConversationMessageDeliveryStatus.Delivered:
      return "Delivered";
    case ConversationMessageDeliveryStatus.Read:
      return "Read";
    case ConversationMessageDeliveryStatus.Failed:
      return "Failed";
    default:
      return null;
  }
}