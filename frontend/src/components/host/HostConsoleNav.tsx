interface HostConsoleNavProps {
  conversationsHref: string;
  copilotWorkspaceHref?: string | null;
  propertyKnowledgeHref?: string | null;
  whatsappSettingsHref?: string | null;
  organizationSettingsHref?: string | null;
  accountSettingsHref?: string | null;
  current: "conversations" | "copilot" | "knowledge" | "settings" | "organization" | "account";
}

export function HostConsoleNav({ conversationsHref, copilotWorkspaceHref, propertyKnowledgeHref, whatsappSettingsHref, organizationSettingsHref, accountSettingsHref, current }: HostConsoleNavProps) {
  return (
    <nav className="sf-host-console-nav" aria-label="Host console navigation">
      <a className={current === "conversations" ? "active" : ""} href={conversationsHref}>
        Conversations
      </a>
      {copilotWorkspaceHref ? (
        <a className={current === "copilot" ? "active" : ""} href={copilotWorkspaceHref}>
          Host Copilot
        </a>
      ) : (
        <span className="disabled" aria-disabled="true">Host Copilot</span>
      )}
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
      {organizationSettingsHref ? (
        <a className={current === "organization" ? "active" : ""} href={organizationSettingsHref}>
          Organization
        </a>
      ) : (
        <span className="disabled" aria-disabled="true">Organization</span>
      )}
      {accountSettingsHref ? (
        <a className={current === "account" ? "active" : ""} href={accountSettingsHref}>
          Account
        </a>
      ) : (
        <span className="disabled" aria-disabled="true">Account</span>
      )}
    </nav>
  );
}
