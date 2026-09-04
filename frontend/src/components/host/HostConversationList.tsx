import type { ConversationSummary } from "../../models/hostConversations";
import { HostConversationEmptyState } from "./HostConversationEmptyState";
import { HostConversationListItem } from "./HostConversationListItem";

interface HostConversationListProps {
  isLoading: boolean;
  isRefreshing?: boolean;
  error: string | null;
  items: ConversationSummary[];
  selectedConversationId: string | null;
  onRetry: () => void;
  onSelect: (conversationId: string) => void;
}

export function HostConversationList({
  isLoading,
  isRefreshing = false,
  error,
  items,
  selectedConversationId,
  onRetry,
  onSelect
}: HostConversationListProps) {
  if (isLoading && items.length === 0) {
    return <div className="sf-host-list-state">Loading conversations...</div>;
  }

  if (error && items.length === 0) {
    return (
      <div className="sf-host-list-state" role="alert">
        <p>{error}</p>
        <button type="button" onClick={onRetry}>
          Retry
        </button>
      </div>
    );
  }

  if (items.length === 0) {
    return <HostConversationEmptyState />;
  }

  return (
    <div className="sf-host-conversation-list" aria-label="Conversation inbox list" tabIndex={0}>
      {error ? (
        <div className="sf-host-inline-error" role="alert">
          <span>{error}</span>
          <button type="button" onClick={onRetry}>
            Retry
          </button>
        </div>
      ) : isRefreshing ? (
        <p className="sf-host-muted-note" role="status">
          Updating...
        </p>
      ) : null}
      {items.map((item) => (
        <HostConversationListItem
          key={item.conversationId}
          item={item}
          isSelected={selectedConversationId === item.conversationId}
          onSelect={onSelect}
        />
      ))}
    </div>
  );
}
