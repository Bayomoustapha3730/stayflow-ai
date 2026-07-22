import type { ConversationSummary } from "../../models/hostConversations";
import { formatRelativeTime, formatReservationRange, formatSenderLabel } from "../../utils/dateTime";
import { HostStatusBadge } from "./HostStatusBadge";

interface HostConversationListItemProps {
  item: ConversationSummary;
  isSelected: boolean;
  onSelect: (conversationId: string) => void;
}

export function HostConversationListItem({ item, isSelected, onSelect }: HostConversationListItemProps) {
  const guestName = item.guest?.fullName?.trim() || `${item.guest?.firstName ?? ""} ${item.guest?.lastName ?? ""}`.trim() || "Guest";
  const guestEmail = item.guest?.email?.trim() || "Email unavailable";
  const propertyName = item.property?.name?.trim() || "Property unavailable";
  const reservationNumber = item.reservation?.confirmationNumber?.trim() || "No reservation number";

  return (
    <button
      type="button"
      className={`sf-host-conversation-item${isSelected ? " is-selected" : ""}`}
      onClick={() => onSelect(item.conversationId)}
      aria-pressed={isSelected}
    >
      <div className="sf-host-row-main">
        <div>
          <h3>{guestName}</h3>
          <p>{guestEmail}</p>
        </div>
        <HostStatusBadge status={item.status} />
      </div>

      <div className="sf-host-row-meta">
        <span>{propertyName}</span>
        <span>{reservationNumber}</span>
        <span>{formatReservationRange(item.reservation?.checkInDate, item.reservation?.checkOutDate)}</span>
      </div>

      <div className="sf-host-row-tags">
        {item.requiresHostAttention ? <span className="sf-host-pill sf-host-pill-attention">Needs attention</span> : null}
        {item.humanTakeoverEnabled ? <span className="sf-host-pill sf-host-pill-human">Human takeover</span> : null}
        {item.subject ? <span className="sf-host-pill">{item.subject}</span> : null}
      </div>

      <div className="sf-host-row-footer">
        <p>{item.latestVisibleMessagePreview?.trim() || "No visible messages yet"}</p>
        <div>
          <span>{formatSenderLabel(item.latestVisibleMessageSenderType)}</span>
          <span>{formatRelativeTime(item.latestVisibleMessageTimestamp ?? item.lastActivityAt)}</span>
          <span>{item.totalVisibleMessageCount} msgs</span>
        </div>
      </div>
    </button>
  );
}
