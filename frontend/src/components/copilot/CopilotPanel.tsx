import { CopilotEmptyState } from "./CopilotEmptyState";
import { CopilotSuggestionCard } from "./CopilotSuggestionCard";
import { useEffect, useMemo, useRef, useState } from "react";
import type { UseConversationCopilotResult } from "../../hooks/useConversationCopilot";
import {
  COPILOT_MAX_GENERATED_REPLY_LENGTH,
  COPILOT_MAX_INSTRUCTION_LENGTH
} from "../../models/copilot";

interface CopilotPanelProps {
  copilot: UseConversationCopilotResult;
  disabled?: boolean;
  currentDraft: string;
  connectionStatus: "live" | "reconnecting" | "degraded" | "offline";
  requiresHostAttention: boolean;
  humanTakeoverEnabled: boolean;
  onUseDraft: (draft: string) => void;
}

function formatDate(value?: string | null): string {
  if (!value) {
    return "Unknown";
  }

  const parsed = Date.parse(value);
  if (Number.isNaN(parsed)) {
    return "Unknown";
  }

  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit"
  }).format(new Date(parsed));
}

export function CopilotPanel({
  copilot,
  disabled = false,
  currentDraft,
  connectionStatus,
  requiresHostAttention,
  humanTakeoverEnabled,
  onUseDraft
}: CopilotPanelProps) {
  const [instruction, setInstruction] = useState("");
  const [editableGeneratedReply, setEditableGeneratedReply] = useState("");
  const [clipboardStatus, setClipboardStatus] = useState<string | null>(null);
  const clipboardTimerRef = useRef<number | null>(null);

  useEffect(() => {
    setEditableGeneratedReply(copilot.generatedReply);
  }, [copilot.generatedReply]);

  useEffect(() => {
    return () => {
      if (clipboardTimerRef.current) {
        window.clearTimeout(clipboardTimerRef.current);
      }
    };
  }, []);

  const urgencyLabel = copilot.summary?.urgency ?? "low";
  const urgencyClassName = `sf-host-copilot-urgency sf-host-copilot-urgency-${urgencyLabel}`;
  const providerLabel = copilot.generatedReplyMetadata?.providerMetadata?.providerName
    || (copilot.generatedReplyMetadata?.isFallback ? "Mock/Deterministic" : "Deterministic");
  const modelLabel = copilot.generatedReplyMetadata?.providerMetadata?.modelName ?? null;
  const confidenceLabel = copilot.generatedReplyMetadata?.isFallback ? "Fallback" : "Contextual";

  const connectionLabel = useMemo(() => {
    if (connectionStatus === "live") {
      return "Live";
    }

    if (connectionStatus === "reconnecting") {
      return "Reconnecting";
    }

    if (connectionStatus === "degraded") {
      return "Connected - updates may be delayed";
    }

    return "Offline";
  }, [connectionStatus]);

  async function copyText(value: string) {
    const next = value.trim();
    if (!next) {
      setClipboardStatus("Copy failed");
      return;
    }

    try {
      if (!navigator.clipboard?.writeText) {
        setClipboardStatus("Copy failed");
        return;
      }

      await navigator.clipboard.writeText(next);
      setClipboardStatus("Copied");
    } catch {
      setClipboardStatus("Copy failed");
    }

    if (clipboardTimerRef.current) {
      window.clearTimeout(clipboardTimerRef.current);
    }

    clipboardTimerRef.current = window.setTimeout(() => {
      setClipboardStatus(null);
      clipboardTimerRef.current = null;
    }, 2000);
  }

  function requestInsert(draft: string) {
    onUseDraft(draft);
  }

  const instructionCount = instruction.trim().length;
  const instructionOverLimit = instructionCount > COPILOT_MAX_INSTRUCTION_LENGTH;
  const generatedCount = editableGeneratedReply.trim().length;
  const generatedOverLimit = generatedCount > COPILOT_MAX_GENERATED_REPLY_LENGTH;
  const hasGeneratedContent = editableGeneratedReply.trim().length > 0;
  const summaryHasContent = Boolean(copilot.summary?.summary?.trim().length);

  return (
    <section className="sf-host-detail-section sf-host-copilot-panel" aria-label="AI Copilot">
      <div className="sf-host-detail-section-header">
        <h3>AI Copilot</h3>
        <p aria-live="polite">
          <span className={`sf-host-connection sf-host-connection-${connectionStatus}`}>{connectionLabel}</span>
        </p>
      </div>

      <p className="sf-host-muted-note" aria-live="polite">
        {providerLabel}{modelLabel ? ` • ${modelLabel}` : ""}
      </p>

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
          {copilot.isRefreshing ? "Refreshing suggestions..." : "Refresh"}
        </button>
      </div>

      <article className="sf-host-copilot-summary" aria-label="Conversation Summary">
        <header>
          <h4>Conversation Summary</h4>
          {copilot.summary ? <span className={urgencyClassName}>{urgencyLabel}</span> : null}
        </header>

        {copilot.isLoadingSummary && !copilot.summary ? (
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
            {summaryHasContent ? <p>{copilot.summary.summary}</p> : <p>No conversation summary is available.</p>}
            <dl>
              <dt>Requires Host Attention</dt>
              <dd>{requiresHostAttention ? "Yes" : "No"}</dd>
            </dl>
            <dl>
              <dt>Human Takeover</dt>
              <dd>{humanTakeoverEnabled ? "Enabled" : "AI managed"}</dd>
            </dl>
            <dl>
              <dt>Guest Intent</dt>
              <dd>{copilot.summary.guestIntent || "General stay support"}</dd>
            </dl>
            <dl>
              <dt>Source Messages</dt>
              <dd>{copilot.summary.visibleMessageCount}</dd>
            </dl>
            <dl>
              <dt>Last Generated</dt>
              <dd>{formatDate(copilot.summary.generatedAt)}</dd>
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
        ) : (
          <div className="sf-host-copilot-empty" role="status">No conversation summary is available.</div>
        )}
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
            {copilot.suggestions.map((reply, index) => (
              <CopilotSuggestionCard
                key={`${reply}-${index}`}
                reply={reply}
                index={index}
                onInsert={requestInsert}
                onCopy={copyText}
              />
            ))}
          </div>
        )}
      </article>

      <article className="sf-host-copilot-summary" aria-label="Generate reply workspace">
        <header>
          <h4>Generate Reply</h4>
          <span className="sf-host-muted-note">{copilot.tone}</span>
        </header>

        <label htmlFor="sf-host-copilot-instruction">What should the reply emphasize?</label>
        <textarea
          id="sf-host-copilot-instruction"
          value={instruction}
          onChange={(event) => setInstruction(event.target.value)}
          rows={2}
          maxLength={COPILOT_MAX_INSTRUCTION_LENGTH + 1}
          placeholder="Optional guidance for Copilot"
          disabled={disabled || copilot.isGeneratingReply}
        />
        <div className="sf-host-composer-footer">
          <span aria-live="polite">{instructionCount}/{COPILOT_MAX_INSTRUCTION_LENGTH}</span>
        </div>
        {instructionOverLimit ? <p className="sf-host-inline-error" role="alert">Instruction is too long.</p> : null}

        <div className="sf-host-copilot-actions">
          <button
            type="button"
            disabled={disabled || copilot.isGeneratingReply || instructionOverLimit}
            onClick={() => {
              void copilot.generateReply({ guidance: instruction });
            }}
          >
            {copilot.isGeneratingReply ? "Generating..." : "Generate reply"}
          </button>

          {currentDraft.trim().length > 0 ? (
            <button
              type="button"
              disabled={disabled || copilot.isGeneratingReply || instructionOverLimit}
              onClick={() => {
                void copilot.generateReply({ guidance: instruction, rewriteDraft: currentDraft });
              }}
            >
              Rewrite current draft
            </button>
          ) : null}
        </div>

        <p className="sf-host-muted-note" aria-live="polite">
          {copilot.generatedReplyState === "loading" ? "Generating reply..." : null}
        </p>
      </article>

      <article className="sf-host-copilot-summary" aria-label="Generated reply workspace">
        <header>
          <h4>Generated Reply</h4>
          <span className="sf-host-muted-note">{confidenceLabel}</span>
        </header>

        {copilot.generatedReplyState === "idle" ? (
          <div className="sf-host-copilot-empty" role="status">No reply generated yet.</div>
        ) : null}

        {copilot.generatedReplyState === "error" ? (
          <div className="sf-host-inline-error" role="alert">
            <p>Unable to generate a reply.</p>
            {copilot.generationError && !/^unable to generate a reply\.?$/i.test(copilot.generationError)
              ? <p>{copilot.generationError}</p>
              : null}
            <button type="button" onClick={() => void copilot.retryGenerateReply()} disabled={disabled || copilot.isGeneratingReply || instructionOverLimit}>
              Retry
            </button>
          </div>
        ) : null}

        {copilot.generatedReplyState === "loading" ? (
          <div className="sf-host-copilot-empty" role="status">Generating reply...</div>
        ) : null}

        {hasGeneratedContent ? (
          <>
            <dl>
              <dt>Tone</dt>
              <dd>{copilot.tone}</dd>
            </dl>
            <dl>
              <dt>Generated</dt>
              <dd>{formatDate(copilot.generatedReplyMetadata?.generatedAt ?? null)}</dd>
            </dl>
            <dl>
              <dt>Provider</dt>
              <dd>{providerLabel}{modelLabel ? ` • ${modelLabel}` : ""}</dd>
            </dl>
            <dl>
              <dt>Confidence</dt>
              <dd>{confidenceLabel}</dd>
            </dl>

            <label htmlFor="sf-host-generated-reply">Generated reply</label>
            <textarea
              id="sf-host-generated-reply"
              value={editableGeneratedReply}
              onChange={(event) => setEditableGeneratedReply(event.target.value)}
              rows={4}
              maxLength={COPILOT_MAX_GENERATED_REPLY_LENGTH + 1}
              disabled={disabled || copilot.isGeneratingReply}
            />
            <div className="sf-host-composer-footer">
              <span aria-live="polite">{generatedCount}/{COPILOT_MAX_GENERATED_REPLY_LENGTH}</span>
            </div>
            {generatedOverLimit ? <p className="sf-host-inline-error" role="alert">Generated reply is too long.</p> : null}

            <div className="sf-host-copilot-actions">
              <button type="button" onClick={() => requestInsert(editableGeneratedReply)} disabled={generatedOverLimit}>
                Insert into composer
              </button>
              <button
                type="button"
                disabled={disabled || copilot.isGeneratingReply || instructionOverLimit}
                onClick={() => {
                  void copilot.retryGenerateReply();
                }}
              >
                Regenerate
              </button>
              <button type="button" onClick={() => void copyText(editableGeneratedReply)}>
                Copy
              </button>
              <button
                type="button"
                onClick={() => {
                  setEditableGeneratedReply("");
                  copilot.clearGeneratedReply();
                }}
              >
                Clear
              </button>
            </div>
          </>
        ) : null}
      </article>

      <p className="sf-host-muted-note" aria-live="polite">{clipboardStatus}</p>

    </section>
  );
}