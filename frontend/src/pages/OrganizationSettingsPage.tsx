import { useEffect, useState } from "react";
import { HostConsoleNav } from "../components/host/HostConsoleNav";
import { HostLoginPanel } from "../components/host";
import { useHostAuth } from "../hooks/useHostAuth";
import { useOrganizationSettings } from "../hooks/useOrganizationSettings";
import "../styles/host-inbox.css";
import "../styles/organization-settings.css";

const roleOptions = ["Owner", "Administrator", "Manager", "Host", "Support", "ReadOnly"];

function canEditOrganization(role: string | null | undefined): boolean {
  return role === "Owner" || role === "Administrator";
}

function canEditMembers(role: string | null | undefined): boolean {
  return role === "Owner" || role === "Administrator" || role === "Manager";
}

function canRemoveMembers(role: string | null | undefined): boolean {
  return role === "Owner" || role === "Administrator";
}

export function OrganizationSettingsPage() {
  const auth = useHostAuth();
  const settings = useOrganizationSettings({
    accessToken: auth.accessToken,
    onUnauthorized: auth.logout
  });

  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const [status, setStatus] = useState("Active");
  const [brandingLogoUrl, setBrandingLogoUrl] = useState("");
  const [brandingPrimaryColor, setBrandingPrimaryColor] = useState("");
  const [onboardingState, setOnboardingState] = useState("");

  const role = auth.currentUser?.organizationRole ?? null;
  const canUpdateOrganization = canEditOrganization(role);
  const canUpdateMembers = canEditMembers(role);
  const canRemove = canRemoveMembers(role);

  useEffect(() => {
    if (!settings.organization) {
      return;
    }

    setName(settings.organization.name ?? "");
    setSlug(settings.organization.slug ?? "");
    setStatus(settings.organization.status ?? "Active");
    setBrandingLogoUrl(settings.organization.brandingLogoUrl ?? "");
    setBrandingPrimaryColor(settings.organization.brandingPrimaryColor ?? "");
    setOnboardingState(settings.organization.onboardingState ?? "");
  }, [settings.organization]);

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
            <h1>Organization Settings</h1>
            <p className="sf-host-muted-note">Manage tenant identity, lifecycle status, and team access roles.</p>
          </div>
          <div className="sf-organization-header-actions">
            <button type="button" onClick={() => void settings.refresh()} disabled={settings.isLoading}>Refresh</button>
            <button type="button" onClick={() => auth.logout()}>Sign out</button>
          </div>
        </header>

        <HostConsoleNav
          conversationsHref="/host/conversations"
          copilotWorkspaceHref="/host/copilot"
          propertyKnowledgeHref={null}
          billingHref="/host/settings/billing"
          whatsappSettingsHref="/host/settings/whatsapp"
          organizationSettingsHref="/host/settings/organization"
          accountSettingsHref="/host/settings/account"
          current="organization"
        />

        <div className="sf-organization-access-note">
          Signed in as: <strong>{auth.currentUser?.fullName ?? "Host"}</strong> ({role ?? "Unknown role"})
        </div>

        {settings.error ? <div className="sf-host-inline-error" role="alert"><p>{settings.error}</p></div> : null}
        {settings.message ? <div className="sf-whatsapp-status" role="status">{settings.message}</div> : null}

        <section className="sf-organization-grid" aria-label="Organization profile settings">
          <article className="sf-organization-card">
            <h2>Organization Profile</h2>
            <label>
              Name
              <input value={name} onChange={(event) => setName(event.target.value)} disabled={!canUpdateOrganization || settings.isSaving} />
            </label>
            <label>
              Slug
              <input value={slug} onChange={(event) => setSlug(event.target.value)} disabled={!canUpdateOrganization || settings.isSaving} />
            </label>
            <label>
              Status
              <select value={status} onChange={(event) => setStatus(event.target.value)} disabled={!canUpdateOrganization || settings.isSaving}>
                <option value="Active">Active</option>
                <option value="Inactive">Inactive</option>
                <option value="Suspended">Suspended</option>
              </select>
            </label>
            <label>
              Branding Logo URL
              <input value={brandingLogoUrl} onChange={(event) => setBrandingLogoUrl(event.target.value)} disabled={!canUpdateOrganization || settings.isSaving} />
            </label>
            <label>
              Branding Primary Color
              <input value={brandingPrimaryColor} onChange={(event) => setBrandingPrimaryColor(event.target.value)} disabled={!canUpdateOrganization || settings.isSaving} placeholder="#0B5FFF" />
            </label>
            <label>
              Onboarding State
              <input value={onboardingState} onChange={(event) => setOnboardingState(event.target.value)} disabled={!canUpdateOrganization || settings.isSaving} />
            </label>

            <button
              type="button"
              disabled={!canUpdateOrganization || settings.isSaving}
              onClick={() => {
                void settings.updateOrganization({
                  name,
                  slug,
                  status,
                  brandingLogoUrl,
                  brandingPrimaryColor,
                  onboardingState
                });
              }}
            >
              {settings.isSaving ? "Saving..." : "Save Organization"}
            </button>
            {!canUpdateOrganization ? <p className="sf-host-muted-note">Read-only: only Owners and Administrators can edit organization settings.</p> : null}
          </article>

          <article className="sf-organization-card">
            <h2>Team Members</h2>
            {settings.isLoading ? <p>Loading members...</p> : null}
            {!settings.isLoading && settings.members.length === 0 ? <p>No active members found.</p> : null}

            <div className="sf-organization-member-list" role="list">
              {settings.members.map((member) => (
                <div key={member.userId} className="sf-organization-member-row" role="listitem">
                  <div>
                    <p className="sf-organization-member-name">{member.fullName}</p>
                    <p className="sf-organization-member-meta">{member.email}</p>
                  </div>
                  <div className="sf-organization-member-actions">
                    <select
                      value={member.role}
                      disabled={!canUpdateMembers || settings.isSaving}
                      onChange={(event) => {
                        void settings.updateMemberRole(member.userId, event.target.value);
                      }}
                    >
                      {roleOptions.map((item) => (
                        <option key={item} value={item}>{item}</option>
                      ))}
                    </select>
                    <button
                      type="button"
                      disabled={!canRemove || settings.isSaving || member.userId === auth.currentUser?.id}
                      onClick={() => {
                        void settings.removeMember(member.userId);
                      }}
                    >
                      Remove
                    </button>
                  </div>
                </div>
              ))}
            </div>

            {!canUpdateMembers ? <p className="sf-host-muted-note">Read-only: your role can view members but cannot change roles.</p> : null}
          </article>
        </section>
      </div>
    </div>
  );
}