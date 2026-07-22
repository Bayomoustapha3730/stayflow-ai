import { useEffect, useRef, useState } from "react";
import type { ConversationMessage } from "../../models/hostConversations";
import { HostConversationMessage } from "./HostConversationMessage";

interface HostConversationTimelineProps {
  messages: ConversationMessage[];
  isRefreshing: boolean;
}

const autoScrollThresholdPx = 120;

export function HostConversationTimeline({ messages, isRefreshing }: HostConversationTimelineProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [isPinnedToBottom, setIsPinnedToBottom] = useState(true);

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
        <p aria-live="polite">{isRefreshing ? "Refreshing..." : "Up to date"}</p>
      </header>

      <div className="sf-host-timeline" onScroll={handleScroll} ref={containerRef}>
        {messages.length === 0 ? (
          <div className="sf-host-timeline-empty" role="status">
            No messages yet.
          </div>
        ) : (
          <ol className="sf-host-message-list">
            {messages.map((message) => (
              <HostConversationMessage key={message.id} message={message} />
            ))}
          </ol>
        )}
      </div>
    </section>
  );
}
