import type { PendingActionCard } from "../models/chat";

interface ActionConfirmationCardProps {
  pendingAction: PendingActionCard;
  disabled?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

export function ActionConfirmationCard({ pendingAction, disabled, onConfirm, onCancel }: ActionConfirmationCardProps) {
  return (
    <section className="sf-action-card" aria-live="polite" aria-label="Action confirmation">
      <p className="sf-action-card__prompt">{pendingAction.prompt}</p>
      <div className="sf-action-card__actions">
        <button type="button" className="sf-action-card__confirm" onClick={onConfirm} disabled={disabled}>
          Confirm
        </button>
        <button type="button" className="sf-action-card__cancel" onClick={onCancel} disabled={disabled}>
          Cancel
        </button>
      </div>
    </section>
  );
}
