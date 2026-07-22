import { useState } from "react";
import { CopilotEmptyState } from "./CopilotEmptyState";
import { CopilotSuggestionCard } from "./CopilotSuggestionCard";
import type { UseConversationCopilotResult } from "../../hooks/useConversationCopilot";

interface CopilotPanelProps {
  copilot: UseConversationCopilotResult;
  disabled?: boolean;
  onUseDraft: (draft: string) => void;
}

export function CopilotPanel({ copilot, disabled = false, onUseDraft }: CopilotPanelProps) {
  const [guidance, setGuidance] = useState("");

  return (
    <section className="sf-host-detail-section" aria-label="Host Copilot">
      <div className="sf-host-detail-section-header">
        <h3>Host Copilot</h3>
        <p>Draft assistant</p>
      </div>

      <label htmlFor="sf-host-copilot-guidance">Optional guidance</label>
      <textarea
        id="sf-host-copilot-guidance"
        value={guidance}
        onChange={(event) => setGuidance(event.target.value)}
        rows={3}
        disabled={disabled || copilot.isGenerating}
        placeholder="Example: Keep it brief and offer a late check-out alternative."
      />

      <div className="sf-host-copilot-controls">
        <button
          type="button"
          disabled={disabled || copilot.isGenerating}
          onClick={() => {
            void copilot.generateSuggestion(guidance);
          }}
        >
          {copilot.isGenerating ? "Generating..." : "Generate draft"}
        </button>
        {copilot.error ? (
          <button type="button" onClick={copilot.clearError}>
            Dismiss error
          </button>
        ) : null}
      </div>

      {copilot.error ? <p className="sf-host-inline-error">{copilot.error}</p> : null}

      {copilot.suggestion ? (
        <CopilotSuggestionCard
          suggestion={copilot.suggestion}
          onUseDraft={onUseDraft}
          onClear={copilot.clearSuggestion}
        />
      ) : (
        <CopilotEmptyState isGenerating={copilot.isGenerating} />
      )}
    </section>
  );
}