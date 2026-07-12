interface EndConversationDialogProps {
  open: boolean;
  isEnding: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

export function EndConversationDialog({ open, isEnding, onConfirm, onCancel }: EndConversationDialogProps) {
  if (!open) {
    return null;
  }

  return (
    <div className="sf-chat-dialog-backdrop" role="presentation">
      <div className="sf-chat-dialog" role="dialog" aria-modal="true" aria-labelledby="sf-chat-end-title">
        <h3 id="sf-chat-end-title">End this conversation?</h3>
        <p>You can start a new conversation later if you need more help.</p>
        <div className="sf-chat-dialog-actions">
          <button type="button" onClick={onCancel} disabled={isEnding}>
            Cancel
          </button>
          <button type="button" onClick={onConfirm} disabled={isEnding}>
            {isEnding ? "Ending" : "End conversation"}
          </button>
        </div>
      </div>
    </div>
  );
}
