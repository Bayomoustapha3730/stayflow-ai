interface HostConversationDetailErrorProps {
  error: string;
  onRetry: () => void;
}

export function HostConversationDetailError({ error, onRetry }: HostConversationDetailErrorProps) {
  return (
    <section className="sf-host-detail-state" role="alert">
      <h3>Unable to load conversation</h3>
      <p>{error}</p>
      <button type="button" onClick={onRetry}>
        Retry
      </button>
    </section>
  );
}
