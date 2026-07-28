import type { ChatMessage } from "../models/chat";
import { ConversationMessageFeedbackValue } from "../models/chat";
import { ConversationSenderType, senderLabel } from "../models/enums";

interface ChatMessageBubbleProps {
  message: ChatMessage;
  onSubmitFeedback?: (messageId: string, feedbackValue: ConversationMessageFeedbackValue) => Promise<void>;
  isSubmittingFeedback?: boolean;
}

export function ChatMessageBubble({ message, onSubmitFeedback, isSubmittingFeedback = false }: ChatMessageBubbleProps) {
  const isGuest = message.senderType === ConversationSenderType.Guest;
  const isSystem = message.senderType === ConversationSenderType.System;
  const isAssistant = message.senderType === ConversationSenderType.AI;
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
      {isAssistant ? (
        <div className="sf-chat-feedback" aria-label="Reply feedback">
          <button
            type="button"
            className={message.feedback?.feedbackValue === ConversationMessageFeedbackValue.Helpful ? "is-selected" : ""}
            disabled={isSubmittingFeedback}
            onClick={() => {
              void onSubmitFeedback?.(message.id, ConversationMessageFeedbackValue.Helpful);
            }}
          >
            Helpful
          </button>
          <button
            type="button"
            className={message.feedback?.feedbackValue === ConversationMessageFeedbackValue.NotHelpful ? "is-selected" : ""}
            disabled={isSubmittingFeedback}
            onClick={() => {
              void onSubmitFeedback?.(message.id, ConversationMessageFeedbackValue.NotHelpful);
            }}
          >
            Not helpful
          </button>
        </div>
      ) : null}
      {message.localStatus === "failed" ? <span className="sf-chat-message-status">Not sent</span> : null}
    </article>
  );
}
