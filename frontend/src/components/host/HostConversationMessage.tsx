import { ConversationMessageType, ConversationSenderType } from "../../models/enums";
import type { ConversationMessage } from "../../models/hostConversations";

interface HostConversationMessageProps {
  message: ConversationMessage;
}

function messageLabel(message: ConversationMessage): string {
  if (message.isInternal || message.messageType === ConversationMessageType.InternalNote) {
    return "Internal Note";
  }

  switch (message.senderType) {
    case ConversationSenderType.Guest:
      return "Guest";
    case ConversationSenderType.AI:
      return "AI";
    case ConversationSenderType.Host:
      return "Host";
    case ConversationSenderType.System:
      return "System";
    default:
      return "Unknown";
  }
}

function messageClassName(message: ConversationMessage): string {
  if (message.isInternal || message.messageType === ConversationMessageType.InternalNote) {
    return "sf-host-message sf-host-message-internal";
  }

  switch (message.senderType) {
    case ConversationSenderType.Guest:
      return "sf-host-message sf-host-message-guest";
    case ConversationSenderType.AI:
      return "sf-host-message sf-host-message-ai";
    case ConversationSenderType.Host:
      return "sf-host-message sf-host-message-host";
    case ConversationSenderType.System:
      return "sf-host-message sf-host-message-system";
    default:
      return "sf-host-message";
  }
}

export function HostConversationMessage({ message }: HostConversationMessageProps) {
  const label = messageLabel(message);
  const timestamp = Number.isNaN(Date.parse(message.sentAt))
    ? "Unknown time"
    : new Intl.DateTimeFormat(undefined, {
        month: "short",
        day: "numeric",
        hour: "numeric",
        minute: "2-digit"
      }).format(new Date(message.sentAt));

  return (
    <li className={messageClassName(message)}>
      <header className="sf-host-message-header">
        <span className="sf-host-message-sender">{label}</span>
        {message.isInternal || message.messageType === ConversationMessageType.InternalNote ? (
          <span className="sf-host-message-staff-tag">Staff only</span>
        ) : null}
        <time dateTime={message.sentAt}>{timestamp}</time>
      </header>
      <p>{message.content}</p>
    </li>
  );
}
