interface HostConsoleNavProps {
  conversationsHref: string;
  propertyKnowledgeHref?: string | null;
  current: "conversations" | "knowledge";
}

export function HostConsoleNav({ conversationsHref, propertyKnowledgeHref, current }: HostConsoleNavProps) {
  return (
    <nav className="sf-host-console-nav" aria-label="Host console navigation">
      <a className={current === "conversations" ? "active" : ""} href={conversationsHref}>
        Conversations
      </a>
      {propertyKnowledgeHref ? (
        <a className={current === "knowledge" ? "active" : ""} href={propertyKnowledgeHref}>
          Property Knowledge
        </a>
      ) : (
        <span className="disabled" aria-disabled="true">Property Knowledge</span>
      )}
    </nav>
  );
}
