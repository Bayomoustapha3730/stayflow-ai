import { useEffect, useState } from "react";
import type { StayFlowChatTheme } from "../models/theme";
import { ConversationStatus } from "../models/enums";
import type { UseChatResult } from "../hooks/useChat";
import { ChatComposer } from "./ChatComposer";
import { ChatHeader } from "./ChatHeader";
import { ChatMessageList } from "./ChatMessageList";
import { ChatStatusBanner } from "./ChatStatusBanner";
import { DevLoginPanel } from "./DevLoginPanel";
import { EndConversationDialog } from "./EndConversationDialog";
import { ErrorBanner } from "./ErrorBanner";
import { EscalationPrompt } from "./EscalationPrompt";

interface ChatPanelProps {
  chat: UseChatResult;
  theme: StayFlowChatTheme;
  demoEmail?: string;
}

export function ChatPanel({ chat, theme, demoEmail }: ChatPanelProps) {
  const [showEndDialog, setShowEndDialog] = useState(false);
  const [isSigningIn, setIsSigningIn] = useState(false);

  useEffect(() => {
    if (chat.isOpen && chat.isAuthenticated && chat.conversationId) {
      void chat.loadHistory();
    }
  }, [chat.isAuthenticated, chat.isOpen, chat.conversationId]);

  async function handleLogin(email: string, password: string) {
    setIsSigningIn(true);
    chat.clearError();

    try {
      await chat.login(email, password);
    } catch {
      // The hook captures and normalizes API failures for display.
    } finally {
      setIsSigningIn(false);
    }
  }

  const composerDisabled =
    !chat.isAuthenticated ||
    chat.conversationStatus === ConversationStatus.Closed;

  return (
    <section
      className="sf-chat-panel"
      aria-label="StayFlow guest chat"
      onKeyDown={(event) => {
        if (event.key === "Escape") {
          chat.close();
        }
      }}
    >
      <ChatHeader
        theme={theme}
        status={chat.conversationStatus}
        onClose={chat.close}
        onEnd={() => setShowEndDialog(true)}
        canEnd={Boolean(chat.conversationId) && chat.conversationStatus !== ConversationStatus.Closed}
      />
      <ErrorBanner message={chat.error} onDismiss={chat.clearError} />
      {chat.isAuthenticated ? (
        <>
          <ChatStatusBanner
            status={chat.conversationStatus}
            requiresHostAttention={chat.requiresHostAttention}
            humanTakeoverEnabled={chat.humanTakeoverEnabled}
          />
          <ChatMessageList
            messages={chat.messages}
            welcomeMessage={theme.welcomeMessage}
            isSending={chat.isSending}
            isLoadingHistory={chat.isLoadingHistory}
            showAssistantTyping={chat.isHostTyping || (!chat.requiresHostAttention && !chat.humanTakeoverEnabled && chat.isSending)}
            realtimeState={chat.realtimeState}
          />
          <EscalationPrompt
            disabled={!chat.conversationId || chat.isEscalating || chat.conversationStatus === ConversationStatus.Closed}
            onEscalate={() => void chat.escalate("Guest requested host support from the web widget.")}
          />
          <ChatComposer
            disabled={composerDisabled}
            isSending={chat.isSending}
            onSend={chat.sendMessage}
            onStartTyping={() => {
              void chat.startTyping();
            }}
            onStopTyping={() => {
              void chat.stopTyping();
            }}
          />
          {chat.conversationStatus === ConversationStatus.Closed ? (
            <button type="button" className="sf-chat-start-new" onClick={chat.startNewConversation}>
              Start new conversation
            </button>
          ) : null}
        </>
      ) : (
        <DevLoginPanel defaultEmail={demoEmail} isBusy={isSigningIn} onLogin={handleLogin} />
      )}
      <EndConversationDialog
        open={showEndDialog}
        isEnding={chat.isEnding}
        onCancel={() => setShowEndDialog(false)}
        onConfirm={() => {
          void chat.endConversation().finally(() => setShowEndDialog(false));
        }}
      />
    </section>
  );
}
