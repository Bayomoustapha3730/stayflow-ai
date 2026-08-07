import { useEffect, useMemo, useState } from "react";
import { ApiError, HttpClient } from "../api/httpClient";
import { getRuntimeApiUrl } from "../runtimeConfig";
import {
  createPlatformAdminApi,
  type PlatformTenantActionRequest,
  type PlatformSupportImpersonationStartRequest,
  type PlatformTenantRepairRequest
} from "../api/platformAdminApi";
import { useHostAuth } from "../hooks/useHostAuth";
import type {
  PlatformFeatureFlag,
  PlatformIncident,
  PlatformOperationalMetrics,
  PlatformOrganizationHealth,
  PlatformProviderHealth,
  PlatformReadOnlyDiagnostic,
  PlatformSupportImpersonationSession,
  PlatformSystemConfiguration,
  PlatformTenantDetail,
  PlatformTenantPagedResult,
  PlatformUsageOverview
} from "../models/platformAdmin";
import "../styles/platform-admin.css";

type AdminSection =
  | "dashboard"
  | "tenants"
  | "usage"
  | "health"
  | "feature-flags"
  | "operations"
  | "diagnostics"
  | "background-jobs"
  | "providers"
  | "support"
  | "incidents";

interface ApiBundle {
  listTenants: ReturnType<typeof createPlatformAdminApi>["listTenants"];
  getTenant: ReturnType<typeof createPlatformAdminApi>["getTenant"];
  suspendTenant: ReturnType<typeof createPlatformAdminApi>["suspendTenant"];
  reactivateTenant: ReturnType<typeof createPlatformAdminApi>["reactivateTenant"];
  archiveTenant: ReturnType<typeof createPlatformAdminApi>["archiveTenant"];
  restoreTenant: ReturnType<typeof createPlatformAdminApi>["restoreTenant"];
  getTenantHealth: ReturnType<typeof createPlatformAdminApi>["getTenantHealth"];
  getUsageOverview: ReturnType<typeof createPlatformAdminApi>["getUsageOverview"];
  getOperationalMetrics: ReturnType<typeof createPlatformAdminApi>["getOperationalMetrics"];
  listFeatureFlags: ReturnType<typeof createPlatformAdminApi>["listFeatureFlags"];
  updateFeatureFlag: ReturnType<typeof createPlatformAdminApi>["updateFeatureFlag"];
  getProviderHealth: ReturnType<typeof createPlatformAdminApi>["getProviderHealth"];
  getReadOnlyDiagnostics: ReturnType<typeof createPlatformAdminApi>["getReadOnlyDiagnostics"];
  getSystemConfiguration: ReturnType<typeof createPlatformAdminApi>["getSystemConfiguration"];
  startSupportImpersonation: ReturnType<typeof createPlatformAdminApi>["startSupportImpersonation"];
  endSupportImpersonation: ReturnType<typeof createPlatformAdminApi>["endSupportImpersonation"];
  getIncidents: ReturnType<typeof createPlatformAdminApi>["getIncidents"];
  getBackgroundJobs: ReturnType<typeof createPlatformAdminApi>["getBackgroundJobs"];
  getWebhooks: ReturnType<typeof createPlatformAdminApi>["getWebhooks"];
  getQueues: ReturnType<typeof createPlatformAdminApi>["getQueues"];
  getEmailDelivery: ReturnType<typeof createPlatformAdminApi>["getEmailDelivery"];
  repairTenant: ReturnType<typeof createPlatformAdminApi>["repairTenant"];
}

function createApi(accessToken: string | null): ApiBundle {
  const http = new HttpClient({
    baseUrl: getRuntimeApiUrl(),
    getAccessToken: () => accessToken
  });

  return createPlatformAdminApi(http);
}

function formatDate(value?: string | null): string {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
}

function isPlatformAdmin(permissions: string[]): boolean {
  return permissions.some((permission) => permission === "platform.admin");
}

export function PlatformAdminPage() {
  const auth = useHostAuth();
  const api = useMemo(() => createApi(auth.accessToken), [auth.accessToken]);

  const [section, setSection] = useState<AdminSection>("dashboard");
  const [tenants, setTenants] = useState<PlatformTenantPagedResult | null>(null);
  const [selectedTenantId, setSelectedTenantId] = useState<string | null>(null);
  const [selectedTenant, setSelectedTenant] = useState<PlatformTenantDetail | null>(null);
  const [tenantHealth, setTenantHealth] = useState<PlatformOrganizationHealth | null>(null);
  const [usage, setUsage] = useState<PlatformUsageOverview | null>(null);
  const [operations, setOperations] = useState<PlatformOperationalMetrics | null>(null);
  const [featureFlags, setFeatureFlags] = useState<PlatformFeatureFlag[]>([]);
  const [providers, setProviders] = useState<PlatformProviderHealth[]>([]);
  const [diagnostics, setDiagnostics] = useState<PlatformReadOnlyDiagnostic[]>([]);
  const [systemConfig, setSystemConfig] = useState<PlatformSystemConfiguration | null>(null);
  const [incidents, setIncidents] = useState<PlatformIncident[]>([]);
  const [impersonationSession, setImpersonationSession] = useState<PlatformSupportImpersonationSession | null>(null);
  const [supportReason, setSupportReason] = useState("");
  const [supportCode, setSupportCode] = useState("");
  const [supportTargetCompanyId, setSupportTargetCompanyId] = useState("");
  const [supportTargetUserId, setSupportTargetUserId] = useState("");
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [jobSummary, setJobSummary] = useState<string>("-");

  const canView = isPlatformAdmin(auth.currentUser?.permissions ?? []);

  async function loadTenants() {
    const data = await api.listTenants({ search, status: statusFilter || undefined, page: 1, pageSize: 50 });
    setTenants(data);

    const firstTenant = data.items[0]?.companyId ?? null;
    const targetTenantId = selectedTenantId ?? firstTenant;
    if (!targetTenantId) {
      setSelectedTenantId(null);
      setSelectedTenant(null);
      setTenantHealth(null);
      return;
    }

    setSelectedTenantId(targetTenantId);
    const [tenant, health] = await Promise.all([
      api.getTenant(targetTenantId),
      api.getTenantHealth(targetTenantId)
    ]);

    setSelectedTenant(tenant);
    setTenantHealth(health);
  }

  async function loadOverview() {
    const [usageData, operationData, providerData, diagnosticData, configData, incidentData, jobs, webhooks, queues, email] = await Promise.all([
      api.getUsageOverview(),
      api.getOperationalMetrics(),
      api.getProviderHealth(),
      api.getReadOnlyDiagnostics(),
      api.getSystemConfiguration(),
      api.getIncidents(),
      api.getBackgroundJobs(),
      api.getWebhooks(),
      api.getQueues(),
      api.getEmailDelivery()
    ]);

    setUsage(usageData);
    setOperations(operationData);
    setProviders(providerData);
    setDiagnostics(diagnosticData);
    setSystemConfig(configData);
    setIncidents(incidentData);

    const webhookEvents = webhooks[0]?.totalEventsLast24Hours ?? 0;
    const queueDepth = queues[0]?.depthEstimate ?? -1;
    setJobSummary(
      `jobs=${jobs.length}, webhookEvents24h=${webhookEvents}, queueDepth=${queueDepth}, emailReset24h=${email.passwordResetIssuedLast24Hours}`
    );
  }

  async function loadFeatureFlags() {
    const flags = await api.listFeatureFlags();
    setFeatureFlags(flags);
  }

  async function refresh() {
    setIsLoading(true);
    setError(null);

    try {
      await Promise.all([loadTenants(), loadOverview(), loadFeatureFlags()]);
    } catch (failure) {
      const message = failure instanceof Error ? failure.message : "Failed loading platform administration data.";
      setError(message);
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    if (!auth.isAuthenticated || !canView) {
      return;
    }

    void refresh();
  }, [auth.isAuthenticated, canView]);

  async function applyTenantAction(
    action: "suspend" | "reactivate" | "archive" | "restore" | "repair",
    reason: string
  ) {
    if (!selectedTenantId) {
      return;
    }

    setError(null);
    setIsLoading(true);

    try {
      const body: PlatformTenantActionRequest = { reason };
      if (action === "suspend") {
        await api.suspendTenant(selectedTenantId, body);
      } else if (action === "reactivate") {
        await api.reactivateTenant(selectedTenantId, body);
      } else if (action === "archive") {
        await api.archiveTenant(selectedTenantId, body);
      } else if (action === "restore") {
        await api.restoreTenant(selectedTenantId, body);
      } else {
        const repairRequest: PlatformTenantRepairRequest = {
          reason,
          normalizeStatusAndActivation: true,
          recomputeSubscriptionSnapshot: true
        };
        await api.repairTenant(selectedTenantId, repairRequest);
      }

      await loadTenants();
    } catch (failure) {
      const message = failure instanceof Error ? failure.message : "Tenant action failed.";
      setError(message);
    } finally {
      setIsLoading(false);
    }
  }

  async function toggleFeatureFlag(flag: PlatformFeatureFlag) {
    setError(null);

    try {
      await api.updateFeatureFlag(flag.planId, flag.key, {
        isEnabled: !flag.isEnabled,
        quotaLimit: flag.quotaLimit ?? null,
        isUnlimited: flag.isUnlimited,
        unit: flag.unit ?? null,
        notes: flag.notes ?? null
      });
      await loadFeatureFlags();
    } catch (failure) {
      const message = failure instanceof Error ? failure.message : "Feature flag update failed.";
      setError(message);
    }
  }

  async function startImpersonation() {
    setError(null);

    try {
      const request: PlatformSupportImpersonationStartRequest = {
        targetCompanyId: supportTargetCompanyId.trim(),
        targetUserId: supportTargetUserId.trim(),
        reason: supportReason.trim(),
        explicitAuthorizationCode: supportCode.trim()
      };
      const session = await api.startSupportImpersonation(request);
      setImpersonationSession(session);
      setSection("support");
    } catch (failure) {
      const message = failure instanceof ApiError ? failure.message : "Unable to start support impersonation.";
      setError(message);
    }
  }

  async function endImpersonation() {
    if (!impersonationSession) {
      return;
    }

    setError(null);

    try {
      await api.endSupportImpersonation(impersonationSession.sessionId, {
        reason: "Support session completed"
      });
      setImpersonationSession(null);
    } catch (failure) {
      const message = failure instanceof Error ? failure.message : "Unable to end support impersonation.";
      setError(message);
    }
  }

  if (!auth.isAuthenticated) {
    return (
      <main className="sf-platform-admin-page" aria-live="polite">
        <h1>Platform Admin Dashboard</h1>
        <p>Sign in to continue.</p>
      </main>
    );
  }

  if (!canView) {
    return (
      <main className="sf-platform-admin-page" aria-live="polite">
        <h1>Platform Admin Dashboard</h1>
        <p>You do not have platform administrator permissions.</p>
      </main>
    );
  }

  return (
    <main className="sf-platform-admin-page" aria-live="polite">
      <header className="sf-platform-admin-header">
        <div>
          <p className="sf-platform-admin-kicker">StayFlow SaaS Operations</p>
          <h1>Platform Admin Dashboard</h1>
          <p className="sf-platform-admin-subtitle">Operator tooling for tenant lifecycle, diagnostics, health, and incidents.</p>
        </div>

        <div className="sf-platform-admin-actions">
          <button type="button" onClick={() => { void refresh(); }} disabled={isLoading}>
            {isLoading ? "Refreshing..." : "Refresh"}
          </button>
          <button type="button" onClick={() => auth.logout()}>Sign out</button>
        </div>
      </header>

      <nav className="sf-platform-admin-tabs" aria-label="Platform admin sections">
        {([
          "dashboard",
          "tenants",
          "usage",
          "health",
          "feature-flags",
          "operations",
          "diagnostics",
          "background-jobs",
          "providers",
          "support",
          "incidents"
        ] as AdminSection[]).map((item) => (
          <button
            key={item}
            type="button"
            className={section === item ? "active" : ""}
            onClick={() => setSection(item)}
          >
            {item}
          </button>
        ))}
      </nav>

      {error ? <div className="sf-platform-admin-error" role="alert">{error}</div> : null}

      {section === "dashboard" ? (
        <section className="sf-platform-admin-cards" aria-label="Admin dashboard overview">
          <article>
            <h2>Tenants</h2>
            <p>{tenants?.totalCount ?? 0}</p>
          </article>
          <article>
            <h2>API Requests 30d</h2>
            <p>{usage?.apiRequestsLast30Days ?? 0}</p>
          </article>
          <article>
            <h2>AI Requests 30d</h2>
            <p>{operations?.aiRequestsLast30Days ?? 0}</p>
          </article>
          <article>
            <h2>Incidents 24h</h2>
            <p>{incidents.length}</p>
          </article>
        </section>
      ) : null}

      {section === "tenants" ? (
        <section className="sf-platform-admin-grid">
          <article>
            <h2>Tenant Management</h2>
            <div className="sf-platform-admin-controls">
              <input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search tenants"
                aria-label="Search tenants"
              />
              <select value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)} aria-label="Status filter">
                <option value="">All statuses</option>
                <option value="Active">Active</option>
                <option value="Suspended">Suspended</option>
                <option value="Archived">Archived</option>
              </select>
              <button type="button" onClick={() => { void refresh(); }}>Apply</button>
            </div>

            <ul className="sf-platform-admin-list" aria-label="Tenant list">
              {tenants?.items.map((tenant) => (
                <li key={tenant.companyId}>
                  <button
                    type="button"
                    className={selectedTenantId === tenant.companyId ? "selected" : ""}
                    onClick={async () => {
                      setSelectedTenantId(tenant.companyId);
                      setSelectedTenant(await api.getTenant(tenant.companyId));
                      setTenantHealth(await api.getTenantHealth(tenant.companyId));
                    }}
                  >
                    <span>{tenant.name}</span>
                    <small>{tenant.status}</small>
                  </button>
                </li>
              ))}
            </ul>
          </article>

          <article>
            <h2>Selected Tenant</h2>
            {!selectedTenant ? (
              <p>Select a tenant.</p>
            ) : (
              <div className="sf-platform-admin-detail">
                <p><strong>Name:</strong> {selectedTenant.name}</p>
                <p><strong>Status:</strong> {selectedTenant.status}</p>
                <p><strong>Plan:</strong> {selectedTenant.currentPlanName ?? "-"}</p>
                <p><strong>Users:</strong> {selectedTenant.userCount}</p>
                <p><strong>Properties:</strong> {selectedTenant.propertyCount}</p>
                <p><strong>Conversations:</strong> {selectedTenant.conversationCount}</p>
                <p><strong>Created:</strong> {formatDate(selectedTenant.createdAt)}</p>

                <div className="sf-platform-admin-actions-inline">
                  <button type="button" onClick={() => { void applyTenantAction("suspend", "Operational suspension"); }}>Suspend</button>
                  <button type="button" onClick={() => { void applyTenantAction("reactivate", "Re-enable service"); }}>Reactivate</button>
                  <button type="button" onClick={() => { void applyTenantAction("archive", "Tenant archived"); }}>Archive</button>
                  <button type="button" onClick={() => { void applyTenantAction("restore", "Tenant restored"); }}>Restore</button>
                  <button type="button" onClick={() => { void applyTenantAction("repair", "Manual tenant repair"); }}>Repair</button>
                </div>
              </div>
            )}
          </article>
        </section>
      ) : null}

      {section === "usage" ? (
        <section className="sf-platform-admin-cards" aria-label="Usage analytics">
          <article><h2>API</h2><p>{usage?.apiRequestsLast30Days ?? 0}</p></article>
          <article><h2>AI Requests</h2><p>{usage?.aiRequestsLast30Days ?? 0}</p></article>
          <article><h2>AI Tokens</h2><p>{usage?.aiTokensLast30Days ?? 0}</p></article>
          <article><h2>WhatsApp</h2><p>{usage?.whatsAppMessagesLast30Days ?? 0}</p></article>
        </section>
      ) : null}

      {section === "health" ? (
        <section className="sf-platform-admin-single" aria-label="Organization health">
          <h2>Organization Health</h2>
          {!tenantHealth ? (
            <p>Select a tenant in the tenant management section.</p>
          ) : (
            <>
              <p><strong>Organization:</strong> {tenantHealth.organizationName}</p>
              <p><strong>Status:</strong> {tenantHealth.status}</p>
              <p><strong>Open conversations:</strong> {tenantHealth.openConversations}</p>
              <p><strong>Overdue actions:</strong> {tenantHealth.overdueActionCount}</p>
              <p><strong>Blocking issues:</strong> {tenantHealth.hasBlockingIssues ? "Yes" : "No"}</p>
              <ul>
                {tenantHealth.healthSignals.map((signal) => (
                  <li key={signal}>{signal}</li>
                ))}
              </ul>
            </>
          )}
        </section>
      ) : null}

      {section === "feature-flags" ? (
        <section className="sf-platform-admin-single" aria-label="Feature flags">
          <h2>Feature Flag Management</h2>
          <table>
            <thead>
              <tr>
                <th>Plan</th>
                <th>Flag</th>
                <th>Enabled</th>
                <th>Quota</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              {featureFlags.map((flag) => (
                <tr key={`${flag.planId}:${flag.key}`}>
                  <td>{flag.planName}</td>
                  <td>{flag.key}</td>
                  <td>{flag.isEnabled ? "Yes" : "No"}</td>
                  <td>{flag.isUnlimited ? "Unlimited" : (flag.quotaLimit ?? "-")}</td>
                  <td>
                    <button type="button" onClick={() => { void toggleFeatureFlag(flag); }}>
                      Toggle
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      ) : null}

      {section === "operations" ? (
        <section className="sf-platform-admin-cards" aria-label="Operations dashboard">
          <article><h2>SignalR Events</h2><p>{operations?.signalREventsLast30Days ?? 0}</p></article>
          <article><h2>Billing Webhooks</h2><p>{operations?.billingWebhookEventsLast30Days ?? 0}</p></article>
          <article><h2>Email Events</h2><p>{operations?.emailEventsLast30Days ?? 0}</p></article>
          <article><h2>Health Issues 24h</h2><p>{operations?.healthCheckIssuesLast24Hours ?? 0}</p></article>
        </section>
      ) : null}

      {section === "diagnostics" ? (
        <section className="sf-platform-admin-single" aria-label="Diagnostics dashboard">
          <h2>Read-only Diagnostics</h2>
          <ul className="sf-platform-admin-list-plain">
            {diagnostics.map((item) => (
              <li key={`${item.area}:${item.key}`}>{item.area}.{item.key}: {item.value}</li>
            ))}
          </ul>
          <h3>System configuration</h3>
          {!systemConfig ? (
            <p>Unavailable.</p>
          ) : (
            <ul className="sf-platform-admin-list-plain">
              <li>Environment: {systemConfig.environmentName}</li>
              <li>AI Provider: {systemConfig.aiProvider}</li>
              <li>OpenAI Configured: {systemConfig.openAiConfigured ? "Yes" : "No"}</li>
              <li>Auth Rate Limit: {systemConfig.authRateLimitPerMinute}/min</li>
              <li>Host API Rate Limit: {systemConfig.hostApiRateLimitPerMinute}/min</li>
              <li>AI Generation Rate Limit: {systemConfig.aiGenerationRateLimitPerMinute}/min</li>
              <li>Billing Webhooks Enabled: {systemConfig.billingWebhookEnabled ? "Yes" : "No"}</li>
              <li>WhatsApp Webhooks Enabled: {systemConfig.whatsAppWebhookEnabled ? "Yes" : "No"}</li>
            </ul>
          )}
        </section>
      ) : null}

      {section === "background-jobs" ? (
        <section className="sf-platform-admin-single" aria-label="Background job dashboard">
          <h2>Background Job Dashboard</h2>
          <p>{jobSummary}</p>
        </section>
      ) : null}

      {section === "providers" ? (
        <section className="sf-platform-admin-single" aria-label="Provider health dashboard">
          <h2>Provider Health</h2>
          <ul className="sf-platform-admin-list-plain">
            {providers.map((provider) => (
              <li key={provider.provider}>
                <strong>{provider.provider}</strong>: {provider.status} ({provider.message})
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {section === "support" ? (
        <section className="sf-platform-admin-single" aria-label="Support console">
          <h2>Support Console</h2>
          <p>Support impersonation requires explicit authorization code and is fully audited.</p>

          <div className="sf-platform-admin-form-grid">
            <label>
              Target Company ID
              <input value={supportTargetCompanyId} onChange={(event) => setSupportTargetCompanyId(event.target.value)} />
            </label>
            <label>
              Target User ID
              <input value={supportTargetUserId} onChange={(event) => setSupportTargetUserId(event.target.value)} />
            </label>
            <label>
              Authorization Code
              <input value={supportCode} onChange={(event) => setSupportCode(event.target.value)} />
            </label>
            <label>
              Reason
              <input value={supportReason} onChange={(event) => setSupportReason(event.target.value)} />
            </label>
          </div>

          <div className="sf-platform-admin-actions-inline">
            <button type="button" onClick={() => { void startImpersonation(); }}>Start impersonation</button>
            <button type="button" onClick={() => { void endImpersonation(); }} disabled={!impersonationSession}>End impersonation</button>
          </div>

          {impersonationSession ? (
            <p>
              Session {impersonationSession.sessionId} active until {formatDate(impersonationSession.expiresAtUtc)}.
            </p>
          ) : null}
        </section>
      ) : null}

      {section === "incidents" ? (
        <section className="sf-platform-admin-single" aria-label="Incident dashboard">
          <h2>Incident Dashboard</h2>
          {incidents.length === 0 ? <p>No active incidents from current heuristics.</p> : null}
          <ul className="sf-platform-admin-list-plain">
            {incidents.map((incident) => (
              <li key={incident.incidentCode}>
                <strong>{incident.severity}</strong> - {incident.incidentCode}: {incident.summary} ({formatDate(incident.detectedAtUtc)})
              </li>
            ))}
          </ul>
        </section>
      ) : null}
    </main>
  );
}
