interface CopilotSuggestionCardProps {
  reply: string;
  onInsert: (reply: string) => void;
}

export function CopilotSuggestionCard({ reply, onInsert }: CopilotSuggestionCardProps) {
  return (
    <article className="sf-host-copilot-suggestion-card" aria-live="polite">
      <p>{reply}</p>

      <div className="sf-host-copilot-actions">
        <button type="button" onClick={() => onInsert(reply)}>
          Insert
        </button>
      </div>
    </article>
  );
}