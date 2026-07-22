import { ConversationStatus } from "../../models/enums";
import { formatStatusLabel } from "../../utils/dateTime";

interface HostStatusBadgeProps {
  status: ConversationStatus;
}

export function HostStatusBadge({ status }: HostStatusBadgeProps) {
  return (
    <span className={`sf-host-status sf-host-status-${ConversationStatus[status].toLowerCase()}`}>
      {formatStatusLabel(status)}
    </span>
  );
}
