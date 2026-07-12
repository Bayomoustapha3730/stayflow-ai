import { useEffect, useRef } from "react";
import type { ChatMessage } from "../models/chat";
import { ChatMessageBubble } from "./ChatMessageBubble";
import { EmptyConversationState } from "./EmptyConversationState";
import { TypingIndicator } from "./TypingIndicator";

interface ChatMessageListProps {
  messages: ChatMessage[];
  welcomeMessage: string;
  isSending: boolean;
  isLoadingHistory: boolean;
  showAssistantTyping: boolean;
}

export function ChatMessageList({
  messages,
  welcomeMessage,
  isSending,
  isLoadingHistory,
  showAssistantTyping
}: ChatMessageListProps) {
  const endRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages.length, isSending]);

  return (
    <main className="sf-chat-messages" aria-live="polite">
      {isLoadingHistory ? <div className="sf-chat-loading">Loading conversation...</div> : null}
      {messages.length === 0 ? <EmptyConversationState welcomeMessage={welcomeMessage} /> : null}
      {messages.map((message) => (
        <ChatMessageBubble key={message.id} message={message} />
      ))}
      {isSending && showAssistantTyping ? <TypingIndicator /> : null}
      <div ref={endRef} />
    </main>
  );
}
