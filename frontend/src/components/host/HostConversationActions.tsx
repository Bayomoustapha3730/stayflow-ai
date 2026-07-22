import { useEffect, useRef, useState } from "react";
import { ConversationStatus } from "../../models/enums";
import type { ConversationDetail } from "../../models/hostConversations";

interface HostConversationActionsProps {
  conversation: ConversationDetail;
  isChangingMode: boolean;
  isResolving: boolean;
  isClosing: boolean;
  onTakeOver: () => Promise<boolean>;
  onReturnToAI: () => Promise<boolean>;
  onResolve: () => Promise<boolean>;
  onClose: () => Promise<boolean>;
}

type PendingAction = "resolve" | "close" | null;

function canTransitionToResolved(status: ConversationStatus): boolean {
  return status !== ConversationStatus.Closed && status !== ConversationStatus.Resolved;
}

function canTransitionToClosed(status: ConversationStatus): boolean {
  return status !== ConversationStatus.Closed;
}

function canTransitionToHumanManaged(status: ConversationStatus): boolean {
  return (
    status === ConversationStatus.Open ||
    status === ConversationStatus.AwaitingGuest ||
    status === ConversationStatus.AwaitingHost ||
    status === ConversationStatus.Escalated
  );
}

function canTransitionBackToOpen(status: ConversationStatus): boolean {
  return status !== ConversationStatus.Closed && status !== ConversationStatus.Resolved;
}

export function HostConversationActions({
  conversation,
  isChangingMode,
  isResolving,
  isClosing,
  onTakeOver,
  onReturnToAI,
  onResolve,
  onClose
}: HostConversationActionsProps) {
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);
  const confirmButtonRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (pendingAction) {
      confirmButtonRef.current?.focus();
    }
  }, [pendingAction]);

  const canTakeOver = !conversation.humanTakeoverEnabled && canTransitionToHumanManaged(conversation.status);
  const canReturnToAi = conversation.humanTakeoverEnabled && canTransitionBackToOpen(conversation.status);
  const canResolve = canTransitionToResolved(conversation.status);
  const canClose = canTransitionToClosed(conversation.status);

  return (
    <section className="sf-host-detail-section" aria-label="Conversation actions">
      <h3>Actions</h3>
      <div className="sf-host-action-row">
        {canTakeOver ? (
          <button type="button" onClick={() => void onTakeOver()} disabled={isChangingMode} aria-label="Take over conversation">
            {isChangingMode ? "Updating..." : "Take Over"}
          </button>
        ) : null}

        {canReturnToAi ? (
          <button type="button" onClick={() => void onReturnToAI()} disabled={isChangingMode} aria-label="Return conversation to AI">
            {isChangingMode ? "Updating..." : "Return to AI"}
          </button>
        ) : null}

        {canResolve ? (
          <button type="button" onClick={() => setPendingAction("resolve")} disabled={isResolving || isClosing} aria-label="Resolve conversation">
            {isResolving ? "Resolving..." : "Resolve"}
          </button>
        ) : null}

        {canClose ? (
          <button
            type="button"
            className="sf-host-danger"
            onClick={() => setPendingAction("close")}
            disabled={isClosing || isResolving}
            aria-label="Close conversation"
          >
            {isClosing ? "Closing..." : "Close"}
          </button>
        ) : null}
      </div>

      {pendingAction ? (
        <div className="sf-host-dialog-backdrop" role="presentation">
          <div className="sf-host-dialog" role="dialog" aria-modal="true" aria-labelledby="sf-host-action-dialog-title">
            <h4 id="sf-host-action-dialog-title">
              {pendingAction === "resolve" ? "Resolve this conversation?" : "Close this conversation?"}
            </h4>
            <p>
              {pendingAction === "resolve"
                ? "The conversation will be marked as resolved. Guests can still reopen by sending a new message."
                : "Closing prevents additional host or AI replies until a new guest message reopens the thread."}
            </p>
            <div className="sf-host-dialog-actions">
              <button type="button" onClick={() => setPendingAction(null)} disabled={isResolving || isClosing}>
                Cancel
              </button>
              <button
                type="button"
                ref={confirmButtonRef}
                onClick={async () => {
                  if (pendingAction === "resolve") {
                    const ok = await onResolve();
                    if (ok) {
                      setPendingAction(null);
                    }
                    return;
                  }

                  const ok = await onClose();
                  if (ok) {
                    setPendingAction(null);
                  }
                }}
                disabled={isResolving || isClosing}
              >
                {pendingAction === "resolve" ? "Confirm Resolve" : "Confirm Close"}
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </section>
  );
}
