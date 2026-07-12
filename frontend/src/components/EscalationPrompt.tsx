interface EscalationPromptProps {
  disabled: boolean;
  onEscalate: () => void;
}

export function EscalationPrompt({ disabled, onEscalate }: EscalationPromptProps) {
  return (
    <div className="sf-chat-escalation">
      <span>Need a person?</span>
      <button type="button" onClick={onEscalate} disabled={disabled}>
        Ask host
      </button>
    </div>
  );
}
