interface HostConsoleNavProps {
  conversationsHref: string;
  propertyKnowledgeHref?: string | null;
  whatsappSettingsHref?: string | null;
  current: "conversations" | "knowledge" | "settings";
}

export function HostConsoleNav({ conversationsHref, propertyKnowledgeHref, whatsappSettingsHref, current }: HostConsoleNavProps) {
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
      {whatsappSettingsHref ? (
        <a className={current === "settings" ? "active" : ""} href={whatsappSettingsHref}>
          WhatsApp Settings
        </a>
      ) : (
        <span className="disabled" aria-disabled="true">WhatsApp Settings</span>
      )}
    </nav>
  );
}
