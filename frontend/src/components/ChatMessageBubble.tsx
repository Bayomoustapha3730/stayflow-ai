import type { ChatMessage } from "../models/chat";
import { ConversationSenderType, senderLabel } from "../models/enums";

interface ChatMessageBubbleProps {
  message: ChatMessage;
}

export function ChatMessageBubble({ message }: ChatMessageBubbleProps) {
  const isGuest = message.senderType === ConversationSenderType.Guest;
  const isSystem = message.senderType === ConversationSenderType.System;
  const className = `sf-chat-message ${
    isGuest ? "sf-chat-message-guest" : isSystem ? "sf-chat-message-system" : "sf-chat-message-assistant"
  }`;
  const sentAt = new Intl.DateTimeFormat(undefined, {
    hour: "2-digit",
    minute: "2-digit"
  }).format(new Date(message.sentAt));

  return (
    <article className={className}>
      <span className="sf-chat-message-label">{senderLabel(message.senderType)}</span>
      <p>{message.content}</p>
      <time dateTime={message.sentAt} className="sf-chat-message-time">
        {sentAt}
      </time>
      {message.localStatus === "failed" ? <span className="sf-chat-message-status">Not sent</span> : null}
    </article>
  );
}
