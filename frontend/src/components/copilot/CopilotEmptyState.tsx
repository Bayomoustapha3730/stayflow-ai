interface CopilotEmptyStateProps {
  isGenerating: boolean;
}

export function CopilotEmptyState({ isGenerating }: CopilotEmptyStateProps) {
  return (
    <div className="sf-host-copilot-empty" role="status" aria-live="polite">
      {isGenerating
        ? "Generating a draft based on the latest conversation context..."
        : "Generate a suggested host reply from recent guest messages."}
    </div>
  );
}