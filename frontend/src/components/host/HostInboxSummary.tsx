import type { ConversationSummary } from "../../models/hostConversations";

interface HostInboxSummaryProps {
  totalCount: number;
  page: number;
  items: ConversationSummary[];
}

export function HostInboxSummary({ totalCount, page, items }: HostInboxSummaryProps) {
  const requiringAttention = items.filter((item) => item.requiresHostAttention).length;
  const escalatedOrHumanManaged = items.filter((item) => item.humanTakeoverEnabled).length;

  return (
    <section className="sf-host-summary" aria-label="Conversation summary">
      <article>
        <h2>{totalCount}</h2>
        <p>Total conversations</p>
      </article>
      <article>
        <h2>{requiringAttention}</h2>
        <p>Needs host attention</p>
      </article>
      <article>
        <h2>{escalatedOrHumanManaged}</h2>
        <p>Human managed</p>
      </article>
      <article>
        <h2>{page}</h2>
        <p>Current page</p>
      </article>
    </section>
  );
}
