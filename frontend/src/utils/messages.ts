import { ConversationMessageType } from "../models/enums";
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

  return sortMessages(Array.from(byId.values()));
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
