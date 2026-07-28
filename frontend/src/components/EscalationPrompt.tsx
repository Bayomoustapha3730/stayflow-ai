interface EscalationPromptProps {
  disabled: boolean;
  alreadyEscalated: boolean;
  onEscalate: () => void;
}

export function EscalationPrompt({ disabled, alreadyEscalated, onEscalate }: EscalationPromptProps) {
  return (
    <div className="sf-chat-escalation">
      <span>Need a person?</span>
      <button type="button" onClick={onEscalate} disabled={disabled}>
        {alreadyEscalated ? "Host already notified" : "Ask host"}
      </button>
    </div>
  );
}
