export function HostConversationDetailSkeleton() {
  return (
    <section className="sf-host-detail-skeleton" aria-label="Loading conversation detail" aria-busy="true">
      <div className="sf-host-skeleton-block sf-host-skeleton-header" />
      <div className="sf-host-skeleton-block sf-host-skeleton-meta" />
      <div className="sf-host-skeleton-block sf-host-skeleton-timeline" />
      <div className="sf-host-skeleton-block sf-host-skeleton-composer" />
    </section>
  );
}
