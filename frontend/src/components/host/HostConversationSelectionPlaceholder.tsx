interface HostConversationSelectionPlaceholderProps {
  conversationId: string | null;
}

export function HostConversationSelectionPlaceholder({ conversationId }: HostConversationSelectionPlaceholderProps) {
  return (
    <aside className="sf-host-selection-panel" aria-live="polite">
      <h3>Conversation detail coming next</h3>
      <p>Conversation detail will be implemented in Sprint 4 Part 2B.</p>
      <p>
        Selected conversation ID:
        <strong>{conversationId ?? "None selected"}</strong>
      </p>
    </aside>
  );
}
