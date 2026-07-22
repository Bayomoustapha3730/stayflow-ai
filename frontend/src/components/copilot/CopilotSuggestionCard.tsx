import type { CopilotSuggestReplyResponse } from "../../models/copilot";

interface CopilotSuggestionCardProps {
  suggestion: CopilotSuggestReplyResponse;
  onUseDraft: (draft: string) => void;
  onClear: () => void;
}

export function CopilotSuggestionCard({ suggestion, onUseDraft, onClear }: CopilotSuggestionCardProps) {
  const generatedAt = new Date(suggestion.generatedAt);
  const generatedLabel = Number.isNaN(generatedAt.valueOf())
    ? suggestion.generatedAt
    : generatedAt.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });

  async function copyToClipboard() {
    try {
      await navigator.clipboard.writeText(suggestion.suggestedReply);
    } catch {
      // Browser restrictions or permissions can block clipboard access.
    }
  }

  return (
    <article className="sf-host-copilot-suggestion" aria-live="polite">
      <header>
        <strong>Suggested reply</strong>
        <span>{generatedLabel}</span>
      </header>

      <p>{suggestion.suggestedReply}</p>

      {suggestion.rationale ? <small>{suggestion.rationale}</small> : null}

      <div className="sf-host-copilot-actions">
        <button type="button" onClick={() => onUseDraft(suggestion.suggestedReply)}>
          Use in reply box
        </button>
        <button type="button" onClick={() => void copyToClipboard()}>
          Copy
        </button>
        <button type="button" onClick={onClear}>
          Clear
        </button>
      </div>
    </article>
  );
}