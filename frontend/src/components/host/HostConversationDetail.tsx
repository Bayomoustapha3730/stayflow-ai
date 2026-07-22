import { ConversationStatus } from "../../models/enums";
import { useHostConversationDetail } from "../../hooks/useHostConversationDetail";
import { useConversationCopilot } from "../../hooks/useConversationCopilot";
import { CopilotPanel } from "../copilot";
import { HostConversationActions } from "./HostConversationActions";
import { HostConversationComposer } from "./HostConversationComposer";
import { HostConversationDetailError } from "./HostConversationDetailError";
import { HostConversationDetailSkeleton } from "./HostConversationDetailSkeleton";
import { HostConversationHeader } from "./HostConversationHeader";
import { HostConversationMetadata } from "./HostConversationMetadata";
import { HostConversationTimeline } from "./HostConversationTimeline";
import { HostInternalNoteComposer } from "./HostInternalNoteComposer";
import { useState } from "react";

interface HostConversationDetailProps {
  conversationId: string | null;
  accessToken: string | null;
  onUnauthorized: () => void;
  onConversationChanged?: () => void;
}

export function HostConversationDetail({
  conversationId,
  accessToken,
  onUnauthorized,
  onConversationChanged
}: HostConversationDetailProps) {
  const [copilotDraft, setCopilotDraft] = useState<string | null>(null);
  const [copilotDraftVersion, setCopilotDraftVersion] = useState(0);
  const detail = useHostConversationDetail({
    conversationId,
    accessToken,
    onUnauthorized,
    onConversationChanged
  });
  const copilot = useConversationCopilot({
    conversationId,
    accessToken,
    onUnauthorized
  });

  if (!conversationId) {
    return (
      <aside className="sf-host-selection-panel" aria-live="polite">
        <h3>Select a conversation</h3>
        <p>Choose a conversation from the inbox to view timeline, notes, and actions.</p>
      </aside>
    );
  }

  if (detail.isLoading) {
    return <HostConversationDetailSkeleton />;
  }

  if (detail.error) {
    return <HostConversationDetailError error={detail.error} onRetry={() => void detail.refresh()} />;
  }

  if (!detail.conversation) {
    return (
      <section className="sf-host-detail-state" role="status">
        <h3>Conversation unavailable</h3>
        <p>The selected conversation could not be loaded.</p>
      </section>
    );
  }

  const conversation = detail.conversation;
  const conversationClosed = conversation.status === ConversationStatus.Closed;
  const canSendHostReply = !conversationClosed && conversation.humanTakeoverEnabled;

  const replyDisabledReason = conversationClosed
    ? "This conversation is closed and cannot accept new replies."
    : !conversation.humanTakeoverEnabled
      ? "Enable human takeover before sending a host reply."
      : undefined;

  return (
    <aside className="sf-host-detail-workspace" aria-live="polite">
      <HostConversationHeader
        conversation={conversation}
        isRefreshing={detail.isRefreshing}
        onRefresh={() => {
          void detail.refresh();
        }}
      />

      <HostConversationMetadata conversation={conversation} />

      {detail.actionError ? (
        <div className="sf-host-inline-error" role="alert">
          {detail.actionError}
        </div>
      ) : null}

      <HostConversationActions
        conversation={conversation}
        isChangingMode={detail.isChangingMode}
        isResolving={detail.isResolving}
        isClosing={detail.isClosing}
        isAssigning={detail.isChangingMode}
        onTakeOver={detail.enableHumanTakeover}
        onAssignToMe={detail.assignToMe}
        onUnassign={detail.unassign}
        onReturnToAI={detail.returnToAI}
        onResolve={detail.resolveConversation}
        onClose={detail.closeConversation}
      />

      <HostConversationTimeline
        messages={detail.messages}
        isRefreshing={detail.isRefreshing}
        unreadMessageCount={conversation.unreadMessageCount}
        isGuestTyping={detail.isGuestTyping}
        isAnotherStaffTyping={detail.isAnotherStaffTyping}
        isInternalNoteTyping={detail.isInternalNoteTyping}
        connectionState={detail.realtimeState}
        onRetryFailedMessage={(messageId) => {
          void detail.retryFailedMessage(messageId);
        }}
      />

      <CopilotPanel
        copilot={copilot}
        disabled={conversationClosed || detail.isChangingMode || detail.isClosing}
        onUseDraft={(draft) => {
          setCopilotDraft(draft);
          setCopilotDraftVersion((current) => current + 1);
        }}
      />

      <HostConversationComposer
        isSending={detail.isSendingReply}
        disabled={!canSendHostReply || detail.isChangingMode || detail.isClosing}
        disabledReason={replyDisabledReason}
        actionError={detail.actionError}
        externalDraft={copilotDraft}
        externalDraftVersion={copilotDraftVersion}
        onSend={detail.sendHostMessage}
        onStartTyping={() => {
          void detail.startTyping("host");
        }}
        onStopTyping={() => {
          void detail.stopTyping("host");
        }}
      />

      <HostInternalNoteComposer
        isAddingNote={detail.isAddingNote}
        disabled={conversationClosed || detail.isClosing}
        actionError={detail.actionError}
        onSubmit={detail.addInternalNote}
        onStartTyping={() => {
          void detail.startTyping("internal-note");
        }}
        onStopTyping={() => {
          void detail.stopTyping("internal-note");
        }}
      />
    </aside>
  );
}
