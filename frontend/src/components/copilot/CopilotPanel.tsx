import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { getCopilotWarningLabel } from "../../hooks/useConversationCopilot";
import type {
  CopilotConfidence,
  CopilotContextWarning,
  CopilotSource
} from "../../models/copilot";
import { CopilotEmptyState } from "./CopilotEmptyState";
import { CopilotSuggestionCard } from "./CopilotSuggestionCard";
import type { UseConversationCopilotResult } from "../../hooks/useConversationCopilot";

interface CopilotPanelProps {
  conversationId: string;
  copilot: UseConversationCopilotResult;
  disabled?: boolean;
  onUseDraft: (draft: string) => void;
}

type CopilotSectionKey = "summary" | "sources" | "suggestions" | "generate" | "generated";

type CopilotSectionsState = Record<CopilotSectionKey, boolean>;

const defaultSectionsState: CopilotSectionsState = {
  summary: true,
  sources: false,
  suggestions: true,
  generate: true,
  generated: false
};

export function CopilotPanel({ conversationId, copilot, disabled = false, onUseDraft }: CopilotPanelProps) {
  const [sections, setSections] = useState<CopilotSectionsState>(defaultSectionsState);
  const [showAllSources, setShowAllSources] = useState(false);
  const [guidance, setGuidance] = useState("");
  const [generatedCopied, setGeneratedCopied] = useState(false);

  const summaryWarnings = useMemo(() => dedupeWarnings(copilot.summary?.warnings ?? []), [copilot.summary?.warnings]);
  const suggestionWarnings = useMemo(() => dedupeWarnings(copilot.suggestionMetadata?.warnings ?? []), [copilot.suggestionMetadata?.warnings]);

  const mergedSources = useMemo(() => {
    const summarySources = copilot.summary?.sources ?? [];
    const suggestionSources = copilot.suggestionMetadata?.sources ?? [];
    return dedupeSources([...summarySources, ...suggestionSources]);
  }, [copilot.summary?.sources, copilot.suggestionMetadata?.sources]);

  const sourcePreview = showAllSources ? mergedSources : mergedSources.slice(0, 5);
  const hasSourceOverflow = mergedSources.length > 5;

  useEffect(() => {
    setSections(defaultSectionsState);
    setShowAllSources(false);
    setGuidance("");
    setGeneratedCopied(false);
  }, [conversationId]);

  useEffect(() => {
    if (copilot.generatedReply || copilot.generatedReplyError) {
      setSections((current) => ({ ...current, generated: true }));
    }
  }, [copilot.generatedReply, copilot.generatedReplyError]);

  const summarySectionError = Boolean(copilot.summaryError);
  const suggestionSectionError = Boolean(copilot.suggestionsError);
  const generateSectionError = Boolean(copilot.generatedReplyError);
  const generatedSectionError = Boolean(copilot.generatedReplyError);

  return (
    <section className="sf-host-detail-section sf-host-copilot-panel" aria-label="Host Copilot">
      <div className="sf-host-copilot-panel-top">
        <div className="sf-host-detail-section-header sf-host-copilot-panel-header">
          <div>
            <h3>Host Copilot</h3>
            <p>Assistant workspace</p>
          </div>
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

        <div className="sf-host-copilot-controls">
          <label htmlFor="sf-host-copilot-tone">Tone</label>
          <select
            id="sf-host-copilot-tone"
            value={copilot.tone}
            onChange={(event) => copilot.setTone(event.target.value as typeof copilot.tone)}
            disabled={disabled || copilot.isLoadingSuggestions || copilot.isGeneratingReply}
          >
            <option value="professional">Professional</option>
            <option value="friendly">Friendly</option>
            <option value="luxury">Luxury</option>
            <option value="casual">Casual</option>
          </select>
        </div>
      </div>

      <div className="sf-host-copilot-scroll" aria-label="AI Copilot content" tabIndex={0}>
        <CopilotDisclosure
          title="Conversation Summary"
          open={sections.summary}
          onToggle={(open) => setSections((current) => ({ ...current, summary: open }))}
          hasError={summarySectionError}
        >
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
              <CopilotConfidenceCard confidence={copilot.summary.confidence} ariaLabel="Summary confidence" />
              <SummaryMeta summary={copilot.summary} />
            </div>
          ) : null}
        </CopilotDisclosure>

        <CopilotDisclosure
          title={`Sources (${mergedSources.length})`}
          open={sections.sources}
          onToggle={(open) => setSections((current) => ({ ...current, sources: open }))}
        >
          {mergedSources.length === 0 ? (
            <p className="sf-host-muted-note">No grounding sources were provided.</p>
          ) : (
            <section className="sf-host-copilot-sources" aria-label="Grounding sources">
              <ul className="sf-host-copilot-source-list">
                {sourcePreview.map((source, index) => (
                  <li key={`${source.title}-${source.category ?? "none"}-${index}`} className="sf-host-copilot-source-chip">
                    <span className="sf-host-copilot-source-title">{source.title}</span>
                    {source.category ? <span className="sf-host-copilot-source-meta">{source.category}</span> : null}
                    {source.lastUpdated ? (
                      <time dateTime={source.lastUpdated} className="sf-host-copilot-source-updated">
                        Updated {formatTimestamp(source.lastUpdated)}
                      </time>
                    ) : null}
                  </li>
                ))}
              </ul>
              {hasSourceOverflow ? (
                <button
                  type="button"
                  className="sf-host-copilot-link-button"
                  onClick={() => setShowAllSources((current) => !current)}
                  aria-expanded={showAllSources}
                >
                  {showAllSources ? "Show fewer sources" : "Show all sources"}
                </button>
              ) : null}
            </section>
          )}
        </CopilotDisclosure>

        <CopilotDisclosure
          title={`Suggested Replies (${copilot.suggestions.length})`}
          open={sections.suggestions}
          onToggle={(open) => setSections((current) => ({ ...current, suggestions: open }))}
          hasError={suggestionSectionError}
        >
          <CopilotConfidenceCard confidence={copilot.suggestionMetadata?.confidence} ariaLabel="Suggestions confidence" compact />
          <WarningList warnings={suggestionWarnings} />

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
        </CopilotDisclosure>

        <CopilotDisclosure
          title="Generate Reply"
          open={sections.generate}
          onToggle={(open) => setSections((current) => ({ ...current, generate: open }))}
          hasError={generateSectionError}
        >
          <div className="sf-host-copilot-generate">
            <label htmlFor="sf-host-copilot-guidance">Host instruction (optional)</label>
            <textarea
              id="sf-host-copilot-guidance"
              value={guidance}
              onChange={(event) => setGuidance(event.target.value)}
              rows={3}
              maxLength={800}
              placeholder="Add instruction for tone, emphasis, or constraints"
              disabled={disabled || copilot.isGeneratingReply}
            />
            <div className="sf-host-copilot-actions">
              <button
                type="button"
                onClick={() => {
                  void copilot.generateReply({ guidance, includeInternalNotes: false, maxContextMessages: 12 });
                }}
                disabled={disabled || copilot.isGeneratingReply}
                aria-label="Generate host reply draft"
              >
                {copilot.isGeneratingReply ? "Generating..." : "Generate Reply"}
              </button>
            </div>
            {copilot.generatedReplyError ? (
              <div className="sf-host-inline-error" role="alert">
                <p>{copilot.generatedReplyError}</p>
              </div>
            ) : null}
          </div>
        </CopilotDisclosure>

        <CopilotDisclosure
          title="Generated Reply"
          open={sections.generated}
          onToggle={(open) => setSections((current) => ({ ...current, generated: open }))}
          hasError={generatedSectionError}
        >
          {copilot.generatedReply || copilot.generatedReplyError ? (
            <div className="sf-host-copilot-generated">
              <div className="sf-host-copilot-generated-meta" aria-label="Generated reply metadata">
                {copilot.generatedReply?.providerMetadata?.providerName ? <span>{copilot.generatedReply.providerMetadata.providerName}</span> : null}
                {copilot.generatedReplyTone ? <span>Tone {copilot.generatedReplyTone}</span> : null}
                {copilot.generatedReply?.generatedAt ? <span>{formatTimestamp(copilot.generatedReply.generatedAt)}</span> : null}
                {copilot.generatedReply?.confidence?.level ? <span>{formatConfidenceLine(copilot.generatedReply.confidence)}</span> : null}
              </div>

              <textarea
                aria-label="Generated reply"
                className="sf-host-copilot-generated-textarea"
                value={copilot.generatedReplyDraft}
                onChange={(event) => copilot.setGeneratedReplyDraft(event.target.value)}
                rows={6}
                placeholder="Generated reply will appear here"
              />

              <CopilotConfidenceCard confidence={copilot.generatedReply?.confidence} ariaLabel="Generated reply confidence" compact />
              <WarningList warnings={dedupeWarnings(copilot.generatedReply?.warnings ?? [])} />

              <div className="sf-host-copilot-actions sf-host-copilot-generated-actions">
                <button
                  type="button"
                  onClick={() => onUseDraft(copilot.generatedReplyDraft)}
                  disabled={!copilot.generatedReplyDraft.trim()}
                  aria-label="Insert generated reply into host composer"
                >
                  Insert into composer
                </button>
                <button
                  type="button"
                  onClick={() => {
                    void copyToClipboard(copilot.generatedReplyDraft).then((copied) => {
                      if (!copied) {
                        return;
                      }

                      setGeneratedCopied(true);
                      window.setTimeout(() => setGeneratedCopied(false), 1500);
                    });
                  }}
                  disabled={!copilot.generatedReplyDraft.trim()}
                  aria-label="Copy generated reply"
                >
                  Copy
                </button>
                <button
                  type="button"
                  onClick={() => {
                    void copilot.generateReply({ guidance, includeInternalNotes: false, maxContextMessages: 12 });
                  }}
                  disabled={disabled || copilot.isGeneratingReply}
                  aria-label="Regenerate reply"
                >
                  {copilot.isGeneratingReply ? "Regenerating..." : "Regenerate"}
                </button>
                <button
                  type="button"
                  onClick={() => {
                    copilot.clearGeneratedReply();
                    setSections((current) => ({ ...current, generated: false }));
                  }}
                  aria-label="Clear generated reply"
                >
                  Clear
                </button>
              </div>
              <p className="sf-host-copilot-copy-feedback" aria-live="polite">{generatedCopied ? "Copied" : ""}</p>
            </div>
          ) : (
            <p className="sf-host-muted-note">No generated reply yet.</p>
          )}
        </CopilotDisclosure>
      </div>
    </section>
  );
}

function CopilotDisclosure({
  title,
  open,
  onToggle,
  children,
  hasError = false
}: {
  title: string;
  open: boolean;
  onToggle: (open: boolean) => void;
  children: ReactNode;
  hasError?: boolean;
}) {
  return (
    <details className="sf-host-copilot-disclosure" open={open} onToggle={(event) => onToggle((event.currentTarget as HTMLDetailsElement).open)}>
      <summary aria-expanded={open}>
        <span>{title}</span>
        {hasError ? <span className="sf-host-copilot-error-indicator">Error</span> : null}
      </summary>
      <div className="sf-host-copilot-disclosure-body">{children}</div>
    </details>
  );
}

function CopilotConfidenceCard({
  confidence,
  ariaLabel,
  compact = false
}: {
  confidence?: CopilotConfidence | null;
  ariaLabel: string;
  compact?: boolean;
}) {
  if (!confidence) {
    return null;
  }

  const score = clampScore(confidence.score);

  return (
    <section className={`sf-host-copilot-confidence-card${compact ? " compact" : ""}`} aria-label={ariaLabel}>
      <div className="sf-host-copilot-confidence-header">
        <span className={`sf-host-copilot-confidence-badge sf-host-copilot-confidence-${confidence.level.toLowerCase()}`}>
          {`${confidence.level} confidence`}
        </span>
        {typeof score === "number" ? <span>{`${score}%`}</span> : null}
      </div>
      {typeof score === "number" ? (
        <meter min={0} max={100} value={score} aria-label="Confidence score" className="sf-host-copilot-confidence-meter">
          {score}
        </meter>
      ) : null}
      {confidence.reasons.length > 0 ? (
        <details className="sf-host-copilot-reasons-toggle">
          <summary>Why this confidence</summary>
          <ul className="sf-host-copilot-reasons">
            {confidence.reasons.slice(0, 3).map((reason, index) => (
              <li key={`${reason}-${index}`}>{reason}</li>
            ))}
          </ul>
        </details>
      ) : null}
    </section>
  );
}

function SummaryMeta({ summary }: { summary: NonNullable<UseConversationCopilotResult["summary"]> }) {
  const items: Array<{ label: string; value: string }> = [];

  if (summary.guestIntent) {
    items.push({ label: "Guest intent", value: summary.guestIntent });
  }

  if (typeof summary.visibleMessageCount === "number") {
    items.push({ label: "Source messages", value: String(summary.visibleMessageCount) });
  }

  if (summary.generatedAt) {
    items.push({ label: "Last generated", value: formatTimestamp(summary.generatedAt) });
  }

  if (items.length === 0) {
    return null;
  }

  return (
    <dl className="sf-host-copilot-meta-grid">
      {items.map((item) => (
        <div key={item.label}>
          <dt>{item.label}</dt>
          <dd>{item.value}</dd>
        </div>
      ))}
    </dl>
  );
}

function WarningList({ warnings }: { warnings: CopilotContextWarning[] }) {
  if (warnings.length === 0) {
    return null;
  }

  return (
    <section className="sf-host-copilot-warning-box" aria-label="Context warnings" role="status">
      <h5>Warnings</h5>
      <ul className="sf-host-copilot-warnings">
        {warnings.map((warning) => (
          <li key={warning}>{`Warning: ${getCopilotWarningLabel(warning)}`}</li>
        ))}
      </ul>
    </section>
  );
}

function dedupeWarnings(warnings: CopilotContextWarning[]): CopilotContextWarning[] {
  const seen = new Set<CopilotContextWarning>();
  const output: CopilotContextWarning[] = [];

  for (const warning of warnings) {
    if (seen.has(warning)) {
      continue;
    }

    seen.add(warning);
    output.push(warning);
  }

  return output;
}

function dedupeSources(sources: CopilotSource[]): CopilotSource[] {
  const seen = new Set<string>();
  const output: CopilotSource[] = [];

  for (const source of sources) {
    const key = [source.sourceType, source.title.trim().toLowerCase(), source.category?.trim().toLowerCase() ?? ""].join("|");
    if (seen.has(key)) {
      continue;
    }

    seen.add(key);
    output.push(source);
  }

  return output;
}

function clampScore(score?: number): number | null {
  if (typeof score !== "number" || Number.isNaN(score)) {
    return null;
  }

  return Math.max(0, Math.min(100, Math.round(score)));
}

function formatConfidenceLine(confidence?: CopilotConfidence | null): string {
  if (!confidence) {
    return "";
  }

  const score = clampScore(confidence.score);
  if (score === null) {
    return `${confidence.level} confidence`;
  }

  return `${confidence.level} confidence ${score}%`;
}

function formatTimestamp(value?: string | null): string {
  if (!value) {
    return "";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "";
  }

  return new Intl.DateTimeFormat(undefined, {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit"
  }).format(date);
}

async function copyToClipboard(text: string): Promise<boolean> {
  try {
    if (!navigator.clipboard?.writeText) {
      return false;
    }

    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    return false;
  }
}
