import { CopilotEmptyState } from "./CopilotEmptyState";
import { CopilotSuggestionCard } from "./CopilotSuggestionCard";
import type { UseConversationCopilotResult } from "../../hooks/useConversationCopilot";

interface CopilotPanelProps {
  copilot: UseConversationCopilotResult;
  disabled?: boolean;
  onUseDraft: (draft: string) => void;
}

export function CopilotPanel({ copilot, disabled = false, onUseDraft }: CopilotPanelProps) {
  const urgencyLabel = copilot.summary?.urgency ?? "low";
  const urgencyClassName = `sf-host-copilot-urgency sf-host-copilot-urgency-${urgencyLabel}`;

  return (
    <section className="sf-host-detail-section" aria-label="Host Copilot">
      <div className="sf-host-detail-section-header">
        <h3>Host Copilot</h3>
        <p>Assistant workspace</p>
      </div>

      <div className="sf-host-copilot-controls">
        <label htmlFor="sf-host-copilot-tone">Tone</label>
        <select
          id="sf-host-copilot-tone"
          value={copilot.tone}
          onChange={(event) => copilot.setTone(event.target.value as typeof copilot.tone)}
          disabled={disabled || copilot.isLoadingSuggestions}
        >
          <option value="professional">Professional</option>
          <option value="friendly">Friendly</option>
          <option value="luxury">Luxury</option>
          <option value="casual">Casual</option>
        </select>

        <button
          type="button"
          disabled={disabled || copilot.isRefreshing}
          onClick={() => {
            void copilot.refreshAll();
          }}
        >
          {copilot.isRefreshing ? "Refreshing..." : "Refresh"}
        </button>
      </div>

      <article className="sf-host-copilot-summary" aria-label="Conversation Summary">
        <header>
          <h4>Conversation Summary</h4>
          {copilot.summary ? <span className={urgencyClassName}>{urgencyLabel}</span> : null}
        </header>

        {copilot.isLoadingSummary ? (
          <div className="sf-host-copilot-skeleton" aria-label="summary loading skeleton">
            <div className="sf-host-skeleton-block sf-host-copilot-skeleton-line" />
            <div className="sf-host-skeleton-block sf-host-copilot-skeleton-line" />
            <div className="sf-host-skeleton-block sf-host-copilot-skeleton-line short" />
          </div>
        ) : copilot.summaryError ? (
          <div className="sf-host-inline-error" role="alert">
            <p>{copilot.summaryError}</p>
            <button type="button" onClick={() => void copilot.retrySummary()}>
              Retry
            </button>
          </div>
        ) : copilot.summary ? (
          <div className="sf-host-copilot-summary-body">
            <p>{copilot.summary.summary}</p>
            <dl>
              <dt>Guest Intent</dt>
              <dd>{copilot.summary.guestIntent || "General stay support"}</dd>
            </dl>
            <div>
              <h5>Important Facts</h5>
              <ul>
                {(copilot.summary.importantFacts ?? []).map((fact, index) => (
                  <li key={`${fact}-${index}`}>{fact}</li>
                ))}
              </ul>
            </div>
          </div>
        ) : null}
      </article>

      <article className="sf-host-copilot-suggestions" aria-label="Suggested Replies">
        <header>
          <h4>Suggested Replies</h4>
        </header>

        {copilot.isLoadingSuggestions ? (
          <div className="sf-host-copilot-skeleton" aria-label="suggestions loading skeleton">
            <div className="sf-host-skeleton-block sf-host-copilot-suggestion-skeleton" />
            <div className="sf-host-skeleton-block sf-host-copilot-suggestion-skeleton" />
            <div className="sf-host-skeleton-block sf-host-copilot-suggestion-skeleton" />
          </div>
        ) : copilot.suggestionsError ? (
          <div className="sf-host-inline-error" role="alert">
            <p>{copilot.suggestionsError}</p>
            <button type="button" onClick={() => void copilot.retrySuggestions()}>
              Retry
            </button>
          </div>
        ) : copilot.suggestions.length === 0 ? (
          <CopilotEmptyState isGenerating={false} />
        ) : (
          <div className="sf-host-copilot-suggestion-list">
            {copilot.suggestions.slice(0, 3).map((reply, index) => (
              <CopilotSuggestionCard
                key={`${reply}-${index}`}
                reply={reply}
                onInsert={onUseDraft}
              />
            ))}
          </div>
        )}
      </article>

    </section>
  );
}