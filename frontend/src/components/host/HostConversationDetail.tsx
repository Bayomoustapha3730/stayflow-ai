import { ConversationStatus } from "../../models/enums";
import { useHostConversationDetail } from "../../hooks/useHostConversationDetail";
import { HostConversationActions } from "./HostConversationActions";
import { HostConversationComposer } from "./HostConversationComposer";
import { HostConversationDetailError } from "./HostConversationDetailError";
import { HostConversationDetailSkeleton } from "./HostConversationDetailSkeleton";
import { HostConversationHeader } from "./HostConversationHeader";
import { HostConversationMetadata } from "./HostConversationMetadata";
import { HostConversationTimeline } from "./HostConversationTimeline";
import { HostInternalNoteComposer } from "./HostInternalNoteComposer";

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
  const detail = useHostConversationDetail({
    conversationId,
    accessToken,
    onUnauthorized,
    onConversationChanged
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
        onTakeOver={detail.enableHumanTakeover}
        onReturnToAI={detail.returnToAI}
        onResolve={detail.resolveConversation}
        onClose={detail.closeConversation}
      />

      <HostConversationTimeline messages={detail.messages} isRefreshing={detail.isRefreshing} />

      <HostConversationComposer
        isSending={detail.isSendingReply}
        disabled={!canSendHostReply || detail.isChangingMode || detail.isClosing}
        disabledReason={replyDisabledReason}
        actionError={detail.actionError}
        onSend={detail.sendHostMessage}
      />

      <HostInternalNoteComposer
        isAddingNote={detail.isAddingNote}
        disabled={conversationClosed || detail.isClosing}
        actionError={detail.actionError}
        onSubmit={detail.addInternalNote}
      />
    </aside>
  );
}
