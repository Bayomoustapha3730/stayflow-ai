import { ConversationMessageType, ConversationSenderType } from "../models/enums";
import type { ChatMessage } from "../models/chat";

export function isGuestVisibleMessage(message: ChatMessage): boolean {
  return message.messageType !== ConversationMessageType.InternalNote;
}

export function sortMessages(messages: ChatMessage[]): ChatMessage[] {
  return [...messages].sort((left, right) => {
    const leftTime = new Date(left.sentAt).getTime();
    const rightTime = new Date(right.sentAt).getTime();
    return leftTime - rightTime;
  });
}

export function mergeMessages(existing: ChatMessage[], incoming: ChatMessage[]): ChatMessage[] {
  const byId = new Map<string, ChatMessage>();

  for (const message of [...existing, ...incoming]) {
    if (!isGuestVisibleMessage(message)) {
      continue;
    }

    byId.set(message.id, message);
  }

  const messages = Array.from(byId.values());
  const assistantContents = new Set(
    messages
      .filter((message) => message.senderType === ConversationSenderType.AI)
      .map((message) => message.content.trim())
  );

  return sortMessages(
    messages.filter(
      (message) =>
        message.senderType !== ConversationSenderType.Guest ||
        !assistantContents.has(message.content.trim())
    )
  );
}

export function buildLocalMessage(content: string, conversationId = "pending"): ChatMessage {
  return {
    id: `local-${crypto.randomUUID()}`,
    conversationId,
    senderType: 0,
    content,
    messageType: ConversationMessageType.Text,
    sentAt: new Date().toISOString(),
    localStatus: "sending"
  };
}
