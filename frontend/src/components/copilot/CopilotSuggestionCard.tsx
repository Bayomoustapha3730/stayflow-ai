import { useMemo, useState } from "react";

interface CopilotSuggestionCardProps {
  reply: string;
  onInsert: (reply: string) => void;
}

export function CopilotSuggestionCard({ reply, onInsert }: CopilotSuggestionCardProps) {
  const [copied, setCopied] = useState(false);
  const [expanded, setExpanded] = useState(false);
  const maxPreviewLength = 220;
  const canExpand = reply.length > maxPreviewLength;
  const visibleText = useMemo(() => {
    if (expanded || !canExpand) {
      return reply;
    }

    return `${reply.slice(0, maxPreviewLength).trimEnd()}...`;
  }, [canExpand, expanded, reply]);

  async function handleCopy() {
    try {
      if (navigator.clipboard?.writeText) {
        await navigator.clipboard.writeText(reply);
      }
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1500);
    } catch {
      setCopied(false);
    }
  }

  return (
    <article className="sf-host-copilot-suggestion-card">
      <p>{visibleText}</p>
      {canExpand ? (
        <button
          type="button"
          className="sf-host-copilot-text-toggle"
          onClick={() => setExpanded((current) => !current)}
          aria-expanded={expanded}
        >
          {expanded ? "Collapse" : "Expand"}
        </button>
      ) : null}

      <div className="sf-host-copilot-actions">
        <button type="button" onClick={() => onInsert(reply)} aria-label="Insert suggested reply into host composer">
          Insert
        </button>
        <button type="button" onClick={() => void handleCopy()} aria-label="Copy suggested reply">
          Copy
        </button>
      </div>
      <p className="sf-host-copilot-copy-feedback" aria-live="polite">{copied ? "Copied" : ""}</p>
    </article>
  );
}