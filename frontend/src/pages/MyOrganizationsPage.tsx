import { useEffect, useMemo, useState } from "react";
import { HostConsoleNav, HostLoginPanel } from "../components/host";
import { useHostAuth } from "../hooks/useHostAuth";
import { useAuthorizedOrganizations } from "../hooks/useAuthorizedOrganizations";
import "../styles/host-inbox.css";
import "../styles/organization-settings.css";
import "../styles/organization-workspace.css";

function canOpenSettings(role: string): boolean {
  return role === "Owner" || role === "Administrator";
}

function canManageTeam(role: string): boolean {
  return role === "Owner" || role === "Administrator" || role === "Manager";
}

export function MyOrganizationsPage() {
  const auth = useHostAuth();
  const organizations = useAuthorizedOrganizations({
    accessToken: auth.accessToken,
    onUnauthorized: auth.logout
  });
  const [name, setName] = useState("");
  const [supportContactEmail, setSupportContactEmail] = useState("");
  const [countryCode, setCountryCode] = useState("KE");
  const [timeZone, setTimeZone] = useState("Africa/Nairobi");
  const [message, setMessage] = useState<string | null>(null);
  const [isCreating, setIsCreating] = useState(false);

  useEffect(() => {
    if (!auth.currentUser?.email) {
      return;
    }

    setSupportContactEmail((current) => current || auth.currentUser?.email || "");
  }, [auth.currentUser?.email]);

  const activeOrganization = useMemo(
    () => organizations.organizations.find((item) => item.companyId === auth.currentUser?.companyId)
      ?? organizations.organizations.find((item) => item.isActiveOrganization)
      ?? null,
    [auth.currentUser?.companyId, organizations.organizations]
  );

  async function switchOrganization(companyId: string) {
    if (!await auth.switchOrganization(companyId)) {
      return;
    }

    await organizations.refresh();
  }

  if (!auth.isAuthenticated) {
    return (
      <div className="sf-host-login-shell">
        <HostLoginPanel
          isSigningIn={auth.isSigningIn}
          error={auth.error}
          onLogin={auth.login}
          onClearError={auth.clearError}
        />
      </div>
    );
  }

  return (
    <div className="sf-host-page sf-organization-page">
      <div className="sf-host-page-top">
        <header className="sf-organization-header">
          <div>
            <p className="sf-host-kicker">StayFlow Host Console</p>
            <h1>My Organizations</h1>
            <p className="sf-host-muted-note">Switch workspaces, review memberships, and create another organization.</p>
          </div>
          <div className="sf-organization-header-actions">
            <button type="button" onClick={() => void organizations.refresh()} disabled={organizations.isLoading}>Refresh</button>
            <button type="button" onClick={() => auth.logout()}>Sign out</button>
          </div>
        </header>

        <HostConsoleNav
          auth={auth}
          conversationsHref="/host/conversations"
          copilotWorkspaceHref="/host/copilot"
          propertyKnowledgeHref={null}
          billingHref="/host/settings/billing"
          whatsappSettingsHref="/host/settings/whatsapp"
          organizationSettingsHref="/host/settings/organization"
          organizationsHref="/host/organizations"
          accountSettingsHref="/host/settings/account"
          current="organization"
        />

        <div className="sf-organization-access-note">
          Active organization: <strong>{activeOrganization?.name ?? "Unknown"}</strong>
        </div>

        {auth.error ? <div className="sf-host-inline-error" role="alert"><p>{auth.error}</p></div> : null}
        {organizations.error ? <div className="sf-host-inline-error" role="alert"><p>{organizations.error}</p></div> : null}
        {message ? <div className="sf-whatsapp-status" role="status">{message}</div> : null}

        <section className="sf-organization-grid" aria-label="Organization workspace management">
          <article className="sf-organization-card">
            <h2>Organizations</h2>
            {organizations.isLoading ? <p>Loading organizations...</p> : null}
            {!organizations.isLoading && organizations.organizations.length === 0 ? <p>No accessible organizations found.</p> : null}

            <div className="sf-organization-table" role="table" aria-label="Authorized organizations">
              <div className="sf-organization-table-row sf-organization-table-header" role="row">
                <span role="columnheader">Organization</span>
                <span role="columnheader">Role</span>
                <span role="columnheader">Properties</span>
                <span role="columnheader">Plan</span>
                <span role="columnheader">Status</span>
                <span role="columnheader">Actions</span>
              </div>

              {organizations.organizations.map((item) => (
                <div className="sf-organization-table-row" role="row" key={item.companyId}>
                  <span role="cell">{item.name}{item.isActiveOrganization ? " (Current)" : ""}</span>
                  <span role="cell">{item.role}</span>
                  <span role="cell">{item.propertyCount}</span>
                  <span role="cell">{item.planName ?? "Free"}</span>
                  <span role="cell">{item.organizationStatus}</span>
                  <span role="cell" className="sf-organization-table-actions">
                    <button
                      type="button"
                      onClick={() => {
                        if (item.isActiveOrganization) {
                          window.history.pushState({}, "", "/host/conversations");
                          window.dispatchEvent(new PopStateEvent("popstate"));
                          return;
                        }

                        void switchOrganization(item.companyId);
                      }}
                    >
                      {item.isActiveOrganization ? "Open" : "Switch"}
                    </button>
                    {canOpenSettings(item.role) ? <a href="/host/settings/organization">Settings</a> : null}
                    {canManageTeam(item.role) ? <a href="/host/settings/organization">Manage Team</a> : null}
                  </span>
                </div>
              ))}
            </div>
          </article>

          <article className="sf-organization-card">
            <h2>Create Organization</h2>
            <label>
              Organization Name
              <input value={name} onChange={(event) => setName(event.target.value)} disabled={isCreating} />
            </label>
            <label>
              Support Contact Email
              <input value={supportContactEmail} onChange={(event) => setSupportContactEmail(event.target.value)} disabled={isCreating} />
            </label>
            <label>
              Country Code
              <input value={countryCode} onChange={(event) => setCountryCode(event.target.value.toUpperCase())} disabled={isCreating} maxLength={2} />
            </label>
            <label>
              Time Zone
              <input value={timeZone} onChange={(event) => setTimeZone(event.target.value)} disabled={isCreating} />
            </label>

            <button
              type="button"
              disabled={isCreating}
              onClick={async () => {
                setMessage(null);
                setIsCreating(true);
                const created = await auth.createOrganization({
                  name,
                  supportContactEmail,
                  countryCode,
                  timeZone
                });
                setIsCreating(false);

                if (!created) {
                  return;
                }

                setMessage("Organization created and activated.");
                await organizations.refresh();
              }}
            >
              {isCreating ? "Creating..." : "Create Organization"}
            </button>
          </article>
        </section>
      </div>
    </div>
  );
}