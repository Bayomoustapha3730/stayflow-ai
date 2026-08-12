import type { UseHostAuthResult } from "../../hooks/useHostAuth";
import "../../styles/organization-workspace.css";
import { HostOrganizationSelector } from "./HostOrganizationSelector";

interface HostConsoleNavProps {
  auth?: UseHostAuthResult;
  conversationsHref: string;
  copilotWorkspaceHref?: string | null;
  propertyKnowledgeHref?: string | null;
  billingHref?: string | null;
  whatsappSettingsHref?: string | null;
  organizationSettingsHref?: string | null;
  organizationsHref?: string | null;
  accountSettingsHref?: string | null;
  current: "conversations" | "copilot" | "knowledge" | "billing" | "settings" | "organization" | "account";
}

export function HostConsoleNav({ auth, conversationsHref, copilotWorkspaceHref, propertyKnowledgeHref, billingHref, whatsappSettingsHref, organizationSettingsHref, organizationsHref, accountSettingsHref, current }: HostConsoleNavProps) {
  const canRenderOrganizationSelector = auth
    && organizationsHref
    && typeof auth.switchOrganization === "function"
    && typeof auth.createOrganization === "function";

  return (
    <div className="sf-host-console-shell-nav">
      {canRenderOrganizationSelector ? <HostOrganizationSelector auth={auth} organizationsHref={organizationsHref} /> : null}

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
        {billingHref ? (
          <a className={current === "billing" ? "active" : ""} href={billingHref}>
            Billing
          </a>
        ) : (
          <span className="disabled" aria-disabled="true">Billing</span>
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
    </div>
  );
}
