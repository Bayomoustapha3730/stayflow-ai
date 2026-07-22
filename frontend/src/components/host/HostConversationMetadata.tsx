import type { ConversationDetail } from "../../models/hostConversations";
import { GuestChannel } from "../../models/enums";
import { formatReservationRange } from "../../utils/dateTime";

interface HostConversationMetadataProps {
  conversation: ConversationDetail;
}

export function HostConversationMetadata({ conversation }: HostConversationMetadataProps) {
  const reservationNumber = conversation.reservation?.confirmationNumber?.trim() || "No confirmation number";
  const reservationDates = formatReservationRange(
    conversation.reservation?.checkInDate,
    conversation.reservation?.checkOutDate
  );
  const assignedUser = conversation.assignedUser?.fullName?.trim() || "Unassigned";
  const channel = GuestChannel[conversation.channel] ?? "Unknown";

  return (
    <section className="sf-host-detail-section" aria-label="Conversation metadata">
      <h3>Details</h3>
      <dl className="sf-host-metadata-grid">
        <div>
          <dt>Property</dt>
          <dd>{conversation.property?.name?.trim() || "Property unavailable"}</dd>
        </div>
        <div>
          <dt>Reservation</dt>
          <dd>{reservationNumber}</dd>
        </div>
        <div>
          <dt>Stay dates</dt>
          <dd>{reservationDates}</dd>
        </div>
        <div>
          <dt>Assigned host</dt>
          <dd>{assignedUser}</dd>
        </div>
        <div>
          <dt>Channel</dt>
          <dd>{channel}</dd>
        </div>
        <div>
          <dt>Subject</dt>
          <dd>{conversation.subject?.trim() || "No subject"}</dd>
        </div>
      </dl>
    </section>
  );
}
