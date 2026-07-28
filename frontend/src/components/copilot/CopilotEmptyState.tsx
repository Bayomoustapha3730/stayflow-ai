interface CopilotEmptyStateProps {
  isGenerating: boolean;
}

export function CopilotEmptyState({ isGenerating }: CopilotEmptyStateProps) {
  return (
    <div className="sf-host-copilot-empty" role="status" aria-live="polite">
      {isGenerating
        ? "Loading suggestions..."
        : "No suggestions available."}
    </div>
  );
}