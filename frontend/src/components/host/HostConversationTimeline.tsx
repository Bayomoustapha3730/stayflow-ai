import { Fragment, useEffect, useRef, useState } from "react";
import type { ConversationMessage } from "../../models/hostConversations";
import { HostConversationMessage } from "./HostConversationMessage";

interface HostConversationTimelineProps {
  messages: ConversationMessage[];
  isRefreshing: boolean;
  unreadMessageCount?: number;
  isGuestTyping?: boolean;
  isAnotherStaffTyping?: boolean;
  isInternalNoteTyping?: boolean;
  connectionState?: "offline" | "connecting" | "online" | "reconnecting";
  onRetryFailedMessage?: (messageId: string) => void;
}

const autoScrollThresholdPx = 120;

export function HostConversationTimeline({
  messages,
  isRefreshing,
  unreadMessageCount = 0,
  isGuestTyping = false,
  isAnotherStaffTyping = false,
  isInternalNoteTyping = false,
  connectionState = "offline",
  onRetryFailedMessage
}: HostConversationTimelineProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [isPinnedToBottom, setIsPinnedToBottom] = useState(true);

  const unreadDividerIndex = unreadMessageCount > 0 ? Math.max(messages.length - unreadMessageCount, 0) : -1;

  useEffect(() => {
    const container = containerRef.current;
    if (!container || messages.length === 0) {
      return;
    }

    if (!isPinnedToBottom) {
      return;
    }

    container.scrollTo({ top: container.scrollHeight, behavior: "smooth" });
  }, [isPinnedToBottom, messages.length]);

  const handleScroll = () => {
    const container = containerRef.current;
    if (!container) {
      return;
    }

    const distanceFromBottom = container.scrollHeight - container.scrollTop - container.clientHeight;
    setIsPinnedToBottom(distanceFromBottom < autoScrollThresholdPx);
  };

  return (
    <section className="sf-host-detail-section" aria-label="Conversation timeline">
      <header className="sf-host-detail-section-header">
        <h3>Timeline</h3>
        <p aria-live="polite">
          {connectionState === "online"
            ? isRefreshing
              ? "Refreshing..."
              : "Live"
            : connectionState === "reconnecting"
              ? "Reconnecting..."
              : connectionState === "connecting"
                ? "Connecting..."
                : "Offline"}
        </p>
      </header>

      <div className="sf-host-timeline" onScroll={handleScroll} ref={containerRef}>
        {messages.length === 0 ? (
          <div className="sf-host-timeline-empty" role="status">
            No messages yet.
          </div>
        ) : (
          <ol className="sf-host-message-list">
            {messages.map((message, index) => (
              <Fragment key={message.id}>
                {unreadDividerIndex === index ? <li className="sf-host-unread-divider">New messages</li> : null}
                <HostConversationMessage message={message} onRetry={onRetryFailedMessage} />
              </Fragment>
            ))}
          </ol>
        )}
      </div>

      {isGuestTyping ? (
        <p className="sf-host-typing-indicator" aria-live="polite">
          Guest is typing...
        </p>
      ) : null}
      {isAnotherStaffTyping ? (
        <p className="sf-host-typing-indicator" aria-live="polite">
          Another staff member is typing...
        </p>
      ) : null}
      {isInternalNoteTyping ? (
        <p className="sf-host-typing-indicator" aria-live="polite">
          A staff teammate is drafting an internal note...
        </p>
      ) : null}
      {!isPinnedToBottom ? (
        <button
          type="button"
          className="sf-host-scroll-latest"
          onClick={() => containerRef.current?.scrollTo({ top: containerRef.current.scrollHeight, behavior: "smooth" })}
        >
          Scroll to latest
        </button>
      ) : null}
    </section>
  );
}
