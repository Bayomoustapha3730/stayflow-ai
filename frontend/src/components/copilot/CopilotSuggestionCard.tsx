interface CopilotSuggestionCardProps {
  reply: string;
  index: number;
  onInsert: (reply: string) => void;
  onCopy: (reply: string) => void;
}

export function CopilotSuggestionCard({ reply, index, onInsert, onCopy }: CopilotSuggestionCardProps) {
  return (
    <article className="sf-host-copilot-suggestion-card" aria-live="polite">
      <p>{reply}</p>

      <div className="sf-host-copilot-actions">
        <button type="button" onClick={() => onInsert(reply)} aria-label={`Insert suggested reply ${index + 1}`}>
          Insert
        </button>
        <button type="button" onClick={() => onCopy(reply)} aria-label={`Copy suggested reply ${index + 1}`}>
          Copy
        </button>
      </div>
    </article>
  );
}