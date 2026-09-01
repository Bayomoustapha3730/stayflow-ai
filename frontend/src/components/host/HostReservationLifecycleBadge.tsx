// Backend-derived stage labels (ReservationLifecycleStage). Frontend never computes lifecycle.
const LIFECYCLE_STAGE_LABELS: Record<string, string> = {
  NotConfirmed: "NOT CONFIRMED",
  FutureConfirmed: "FUTURE",
  PreArrival: "PRE-ARRIVAL",
  ArrivingToday: "ARRIVING TODAY",
  InStay: "IN STAY",
  CheckingOutToday: "CHECKING OUT TODAY",
  Completed: "COMPLETED",
  Cancelled: "CANCELLED",
  NoShow: "NO SHOW"
};

interface HostReservationLifecycleBadgeProps {
  lifecycleStage?: string | null;
}

export function HostReservationLifecycleBadge({ lifecycleStage }: HostReservationLifecycleBadgeProps) {
  const label = lifecycleStage ? LIFECYCLE_STAGE_LABELS[lifecycleStage] : undefined;

  // Missing or unrecognized stage: omit the badge rather than guessing.
  if (!label) {
    return null;
  }

  return (
    <span
      className={`sf-host-pill sf-host-lifecycle-badge sf-host-lifecycle-${lifecycleStage!.toLowerCase()}`}
      data-lifecycle-stage={lifecycleStage}
    >
      {label}
    </span>
  );
}
