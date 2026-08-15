import { useMemo, useState } from "react";
import { useAuthorizedOrganizations } from "../../hooks/useAuthorizedOrganizations";
import type { UseHostAuthResult } from "../../hooks/useHostAuth";

interface HostOrganizationSelectorProps {
  auth: UseHostAuthResult;
  organizationsHref: string;
}

function toRoleLabel(role: string): string {
  return role === "Administrator" ? "Admin" : role;
}

export function HostOrganizationSelector({ auth, organizationsHref }: HostOrganizationSelectorProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [isSwitching, setIsSwitching] = useState(false);
  const organizations = useAuthorizedOrganizations({
    accessToken: auth.accessToken,
    onUnauthorized: auth.logout
  });

  const activeOrganization = useMemo(() => {
    return organizations.organizations.find((item) => item.isActiveOrganization)
      ?? organizations.organizations.find((item) => item.companyId === auth.currentUser?.companyId)
      ?? null;
  }, [auth.currentUser?.companyId, organizations.organizations]);

  async function handleSwitch(companyId: string) {
    if (companyId === auth.currentUser?.companyId) {
      setIsOpen(false);
      return;
    }

    setIsSwitching(true);
    const changed = await auth.switchOrganization(companyId);
    if (changed) {
      await organizations.refresh();
      setIsOpen(false);
    }
    setIsSwitching(false);
  }

  return (
    <div className="sf-host-organization-selector" data-testid="host-organization-selector">
      <p className="sf-host-selector-label">Organization</p>
      <button
        type="button"
        className="sf-host-organization-trigger"
        onClick={() => setIsOpen((current) => !current)}
        aria-expanded={isOpen}
        aria-haspopup="menu"
        disabled={isSwitching}
      >
        <span>{activeOrganization?.name ?? auth.currentUser?.companyId ?? "Loading organization..."}</span>
        <span aria-hidden="true">▼</span>
      </button>

      {isOpen ? (
        <div className="sf-host-organization-menu" role="menu">
          {organizations.isLoading ? <p className="sf-host-muted-note">Loading organizations...</p> : null}
          {organizations.error ? <p className="sf-host-inline-error">{organizations.error}</p> : null}

          {organizations.organizations.map((item) => (
            <button
              type="button"
              role="menuitem"
              key={item.companyId}
              className={`sf-host-organization-option ${item.isActiveOrganization ? "active" : ""}`}
              onClick={() => {
                void handleSwitch(item.companyId);
              }}
              disabled={isSwitching}
            >
              <span className="sf-host-organization-option-name">{item.name}</span>
              <span className="sf-host-organization-option-meta">{toRoleLabel(item.role)}</span>
            </button>
          ))}

          <div className="sf-host-organization-menu-footer">
            <a href={organizationsHref}>Manage organizations</a>
            <a href={`${organizationsHref}?create=1`}>+ Create organization</a>
          </div>
        </div>
      ) : null}
    </div>
  );
}