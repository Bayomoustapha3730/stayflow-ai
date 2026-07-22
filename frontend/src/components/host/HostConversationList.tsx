import type { ConversationSummary } from "../../models/hostConversations";
import { HostConversationEmptyState } from "./HostConversationEmptyState";
import { HostConversationListItem } from "./HostConversationListItem";

interface HostConversationListProps {
  isLoading: boolean;
  error: string | null;
  items: ConversationSummary[];
  selectedConversationId: string | null;
  onRetry: () => void;
  onSelect: (conversationId: string) => void;
}

export function HostConversationList({
  isLoading,
  error,
  items,
  selectedConversationId,
  onRetry,
  onSelect
}: HostConversationListProps) {
  if (isLoading) {
    return <div className="sf-host-list-state">Loading conversations...</div>;
  }

  if (error) {
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
    <div className="sf-host-conversation-list" aria-label="Conversations">
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
