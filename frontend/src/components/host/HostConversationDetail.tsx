import { ConversationSenderType, ConversationStatus } from "../../models/enums";
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
import { useEffect, useMemo, useRef, useState } from "react";

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
  const [composerFocusVersion, setComposerFocusVersion] = useState(0);
  const [replyDraft, setReplyDraft] = useState("");
  const [pendingDraftReplacement, setPendingDraftReplacement] = useState<string | null>(null);
  const [copilotRefreshReason, setCopilotRefreshReason] = useState<string | null>(null);
  const latestVisibleMessageIdRef = useRef<string | null>(null);
  const lastStateSnapshotRef = useRef<string>("");
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

  const latestVisibleMessage = useMemo(
    () => [...detail.messages].reverse().find((message) => !message.isInternal),
    [detail.messages]
  );

  useEffect(() => {
    if (!conversationId) {
      latestVisibleMessageIdRef.current = null;
      return;
    }

    const latestId = latestVisibleMessage?.id ?? null;
    if (!latestId) {
      latestVisibleMessageIdRef.current = null;
      return;
    }

    if (latestVisibleMessage?.deliveryStatus === "sending") {
      return;
    }

    if (latestVisibleMessage && latestVisibleMessageIdRef.current && latestVisibleMessageIdRef.current !== latestId) {
      const senderType = latestVisibleMessage.senderType;
      if (
        senderType === ConversationSenderType.Guest
        || senderType === ConversationSenderType.AI
        || senderType === ConversationSenderType.Host
      ) {
        setCopilotRefreshReason("message");
      }
    }

    latestVisibleMessageIdRef.current = latestId;
  }, [conversationId, latestVisibleMessage]);

  useEffect(() => {
    if (!detail.conversation) {
      lastStateSnapshotRef.current = "";
      return;
    }

    const snapshot = `${detail.conversation.status}|${detail.conversation.humanTakeoverEnabled}`;
    if (lastStateSnapshotRef.current && lastStateSnapshotRef.current !== snapshot) {
      setCopilotRefreshReason("state");
    }

    lastStateSnapshotRef.current = snapshot;
  }, [detail.conversation]);

  useEffect(() => {
    if (!conversationId || !copilotRefreshReason) {
      return;
    }

    const timerId = window.setTimeout(() => {
      void copilot.refreshAll();
      setCopilotRefreshReason(null);
    }, 700);

    return () => {
      window.clearTimeout(timerId);
    };
  }, [conversationId, copilot.refreshAll, copilotRefreshReason]);

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
  const connectionStatus: "live" | "reconnecting" | "degraded" | "offline" =
    detail.realtimeState === "online"
      ? "live"
      : detail.realtimeState === "reconnecting"
        ? "reconnecting"
        : detail.conversation || !detail.error
          ? "degraded"
          : "offline";

  const replyDisabledReason = conversationClosed
    ? "This conversation is closed and cannot accept new replies."
    : !conversation.humanTakeoverEnabled
      ? "Enable human takeover before sending a host reply."
      : undefined;

  const internalNoteDisabledReason = conversationClosed
    ? "This conversation is closed. Internal notes are unavailable."
    : undefined;

  function requestInsertIntoComposer(nextDraft: string) {
    if (replyDraft.trim().length > 0 && replyDraft.trim() !== nextDraft.trim()) {
      setPendingDraftReplacement(nextDraft);
      return;
    }

    setCopilotDraft(nextDraft);
    setCopilotDraftVersion((current) => current + 1);
    setComposerFocusVersion((current) => current + 1);
  }

  async function handleSendHostMessage(content: string) {
    const sent = await detail.sendHostMessage(content);
    if (sent) {
      setCopilotRefreshReason("host-reply");
    }

    return sent;
  }

  async function handleAddInternalNote(content: string) {
    const added = await detail.addInternalNote(content);
    if (added) {
      setCopilotRefreshReason("internal-note");
    }

    return added;
  }

  async function runActionWithCopilotRefresh(action: () => Promise<boolean>) {
    const succeeded = await action();
    if (succeeded) {
      setCopilotRefreshReason("conversation-action");
    }

    return succeeded;
  }

  return (
    <aside className="sf-host-detail-workspace" aria-live="polite">
      <div className="sf-host-detail-conversation-pane">
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
          onTakeOver={() => runActionWithCopilotRefresh(detail.enableHumanTakeover)}
          onAssignToMe={() => runActionWithCopilotRefresh(detail.assignToMe)}
          onUnassign={() => runActionWithCopilotRefresh(detail.unassign)}
          onReturnToAI={() => runActionWithCopilotRefresh(detail.returnToAI)}
          onResolve={() => runActionWithCopilotRefresh(detail.resolveConversation)}
          onClose={() => runActionWithCopilotRefresh(detail.closeConversation)}
        />

        <HostConversationTimeline
          messages={detail.messages}
          isRefreshing={detail.isRefreshing}
          unreadMessageCount={conversation.unreadMessageCount}
          isGuestTyping={detail.isGuestTyping}
          isAnotherStaffTyping={detail.isAnotherStaffTyping}
          isInternalNoteTyping={detail.isInternalNoteTyping}
          connectionStatus={connectionStatus}
          onRetryFailedMessage={(messageId) => {
            void detail.retryFailedMessage(messageId);
          }}
        />

        {pendingDraftReplacement ? (
          <div className="sf-host-inline-error" role="status" aria-live="polite">
            <p>Replace the current reply draft?</p>
            <div className="sf-host-copilot-actions">
              <button
                type="button"
                onClick={() => {
                  setCopilotDraft(pendingDraftReplacement);
                  setCopilotDraftVersion((current) => current + 1);
                  setComposerFocusVersion((current) => current + 1);
                  setPendingDraftReplacement(null);
                }}
              >
                Replace
              </button>
              <button type="button" onClick={() => setPendingDraftReplacement(null)}>
                Cancel
              </button>
            </div>
          </div>
        ) : null}

        <HostConversationComposer
          isSending={detail.isSendingReply}
          disabled={!canSendHostReply || detail.isChangingMode || detail.isClosing}
          disabledReason={replyDisabledReason}
          actionError={detail.actionError}
          externalDraft={copilotDraft}
          externalDraftVersion={copilotDraftVersion}
          requestFocusVersion={composerFocusVersion}
          onDraftChange={setReplyDraft}
          onSend={handleSendHostMessage}
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
          disabledReason={internalNoteDisabledReason}
          actionError={detail.actionError}
          onSubmit={handleAddInternalNote}
          onStartTyping={() => {
            void detail.startTyping("internal-note");
          }}
          onStopTyping={() => {
            void detail.stopTyping("internal-note");
          }}
        />
      </div>

      <div className="sf-host-detail-copilot-pane">
        <CopilotPanel
          copilot={copilot}
          disabled={conversationClosed || detail.isChangingMode || detail.isClosing}
          currentDraft={replyDraft}
          connectionStatus={connectionStatus}
          requiresHostAttention={conversation.requiresHostAttention}
          humanTakeoverEnabled={conversation.humanTakeoverEnabled}
          onUseDraft={requestInsertIntoComposer}
        />
      </div>
    </aside>
  );
}
