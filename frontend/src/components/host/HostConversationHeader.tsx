import type { ConversationDetail } from "../../models/hostConversations";
import { formatRelativeTime } from "../../utils/dateTime";
import { HostStatusBadge } from "./HostStatusBadge";

interface HostConversationHeaderProps {
  conversation: ConversationDetail;
  isRefreshing: boolean;
  onRefresh: () => void;
}

export function HostConversationHeader({ conversation, isRefreshing, onRefresh }: HostConversationHeaderProps) {
  const guestName =
    conversation.guest?.fullName?.trim() ||
    `${conversation.guest?.firstName ?? ""} ${conversation.guest?.lastName ?? ""}`.trim() ||
    "Guest";

  return (
    <header className="sf-host-detail-header">
      <div>
        <h2>{guestName}</h2>
        <p>{conversation.guest?.email?.trim() || "Email unavailable"}</p>
      </div>

      <div className="sf-host-detail-header-meta">
        <HostStatusBadge status={conversation.status} />
        {conversation.requiresHostAttention ? (
          <span className="sf-host-pill sf-host-pill-attention">Needs host attention</span>
        ) : null}
        {conversation.humanTakeoverEnabled ? (
          <span className="sf-host-pill sf-host-pill-human">Human takeover active</span>
        ) : null}
        <span className="sf-host-pill">Last activity {formatRelativeTime(conversation.lastActivityAt)}</span>
        <button type="button" onClick={onRefresh} disabled={isRefreshing} aria-label="Refresh conversation detail">
          {isRefreshing ? "Refreshing..." : "Refresh"}
        </button>
      </div>
    </header>
  );
}
