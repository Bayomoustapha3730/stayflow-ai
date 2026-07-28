import { useMemo } from "react";
import { HostLoginPanel } from "../components/host";
import { HostConsoleNav } from "../components/host/HostConsoleNav";
import { useHostAuth } from "../hooks/useHostAuth";
import { useWhatsAppSettings } from "../hooks/useWhatsAppSettings";
import "../styles/host-inbox.css";
import "../styles/property-knowledge.css";
import "../styles/whatsapp-settings.css";

function formatDateTime(value?: string | null): string {
  if (!value) {
    return "Not available";
  }

  const date = new Date(value);
  if (Number.isNaN(date.valueOf())) {
    return "Not available";
  }

  return date.toLocaleString();
}

export function WhatsAppSettingsPage() {
  const auth = useHostAuth();
  const settings = useWhatsAppSettings({
    accessToken: auth.accessToken,
    onUnauthorized: auth.logout
  });

  const templates = settings.templatesResponse?.items ?? [];

  const filterOptions = useMemo(() => {
    const statuses = new Set<string>();
    const languages = new Set<string>();
    const categories = new Set<string>();

    for (const template of templates) {
      if (template.status) {
        statuses.add(template.status);
      }

      if (template.languageCode) {
        languages.add(template.languageCode);
      }

      if (template.category) {
        categories.add(template.category);
      }
    }

    return {
      statuses: Array.from(statuses).sort((left, right) => left.localeCompare(right)),
      languages: Array.from(languages).sort((left, right) => left.localeCompare(right)),
      categories: Array.from(categories).sort((left, right) => left.localeCompare(right))
    };
  }, [templates]);

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
    <div className="sf-host-page sf-whatsapp-settings-page">
      <div className="sf-host-page-top">
        <header className="sf-whatsapp-header">
          <div>
            <p className="sf-host-kicker">StayFlow Host Console</p>
            <h1>WhatsApp Settings</h1>
            <p className="sf-whatsapp-subtitle">Manage integration health and approved template visibility.</p>
          </div>

          <div className="sf-whatsapp-header-actions">
            <button
              type="button"
              onClick={() => {
                void settings.checkHealth();
              }}
              disabled={!settings.selectedIntegration || settings.isCheckingHealth || settings.isLoadingIntegrations}
            >
              {settings.isCheckingHealth ? "Checking..." : "Check Health"}
            </button>

            <button
              type="button"
              onClick={() => {
                void settings.syncTemplates();
              }}
              disabled={!settings.selectedIntegration || settings.isSyncingTemplates || settings.isLoadingIntegrations}
            >
              {settings.isSyncingTemplates ? "Syncing..." : "Sync Templates"}
            </button>

            <button
              type="button"
              onClick={() => {
                void settings.refresh();
              }}
              disabled={settings.isLoadingIntegrations || settings.isLoadingTemplates}
            >
              Refresh
            </button>
          </div>
        </header>

        <HostConsoleNav
          conversationsHref="/host/conversations"
          propertyKnowledgeHref={null}
          whatsappSettingsHref="/host/settings/whatsapp"
          current="settings"
        />

        {settings.error ? (
          <div className="sf-host-inline-error" role="alert">
            <p>{settings.error}</p>
          </div>
        ) : null}

        {settings.actionMessage ? (
          <div className="sf-whatsapp-status" role="status" aria-live="polite">
            {settings.actionMessage}
          </div>
        ) : null}

        <section className="sf-whatsapp-integration-grid" aria-label="WhatsApp integration status">
          <article className="sf-whatsapp-card">
            <h2>Integration</h2>
            {settings.isLoadingIntegrations ? <p>Loading integration...</p> : null}
            {!settings.isLoadingIntegrations && !settings.selectedIntegration ? (
              <p>No integration is configured for this company.</p>
            ) : null}
            {settings.integrations.length > 1 ? (
              <label className="sf-whatsapp-inline-control">
                Integration
                <select
                  value={settings.selectedIntegrationId ?? ""}
                  onChange={(event) => settings.setSelectedIntegrationId(event.target.value)}
                >
                  {settings.integrations.map((integration) => (
                    <option key={integration.id} value={integration.id}>{integration.displayName}</option>
                  ))}
                </select>
              </label>
            ) : null}
            {settings.selectedIntegration ? (
              <dl className="sf-whatsapp-definition-grid">
                <div>
                  <dt>Display Name</dt>
                  <dd>{settings.selectedIntegration.displayName}</dd>
                </div>
                <div>
                  <dt>Business Number</dt>
                  <dd>{settings.selectedIntegration.businessPhoneNumberMasked || "Not available"}</dd>
                </div>
                <div>
                  <dt>Mode</dt>
                  <dd>{settings.selectedIntegration.mode}</dd>
                </div>
                <div>
                  <dt>Active</dt>
                  <dd>{settings.selectedIntegration.isActive ? "Yes" : "No"}</dd>
                </div>
                <div>
                  <dt>Production Enabled</dt>
                  <dd>{settings.selectedIntegration.isProductionEnabled ? "Yes" : "No"}</dd>
                </div>
                <div>
                  <dt>Health Status</dt>
                  <dd>{settings.health?.status ?? settings.selectedIntegration.healthStatus}</dd>
                </div>
                <div>
                  <dt>Last Health Check</dt>
                  <dd>{formatDateTime(settings.health?.checkedAt ?? settings.selectedIntegration.lastHealthCheckAt)}</dd>
                </div>
                <div>
                  <dt>Last Successful Health Check</dt>
                  <dd>{formatDateTime(settings.selectedIntegration.lastSuccessfulHealthCheckAt)}</dd>
                </div>
                <div>
                  <dt>Last Template Sync</dt>
                  <dd>{formatDateTime(settings.selectedIntegration.lastTemplateSyncAt)}</dd>
                </div>
                <div>
                  <dt>Safe Error Summary</dt>
                  <dd>{settings.selectedIntegration.lastErrorSummary || "None"}</dd>
                </div>
              </dl>
            ) : null}
          </article>

          <article className="sf-whatsapp-card">
            <h2>Sync Result</h2>
            {settings.syncResult ? (
              <dl className="sf-whatsapp-definition-grid">
                <div>
                  <dt>Added</dt>
                  <dd>{settings.syncResult.added}</dd>
                </div>
                <div>
                  <dt>Updated</dt>
                  <dd>{settings.syncResult.updated}</dd>
                </div>
                <div>
                  <dt>Unchanged</dt>
                  <dd>{settings.syncResult.unchanged}</dd>
                </div>
                <div>
                  <dt>Disabled</dt>
                  <dd>{settings.syncResult.disabled}</dd>
                </div>
                <div>
                  <dt>Failed</dt>
                  <dd>{settings.syncResult.failed}</dd>
                </div>
                <div>
                  <dt>Synced At</dt>
                  <dd>{formatDateTime(settings.syncResult.syncedAt)}</dd>
                </div>
              </dl>
            ) : (
              <p>Run template sync to view update counts.</p>
            )}
          </article>
        </section>

        <section className="sf-whatsapp-filters" aria-label="Template filters">
          <label>
            Search
            <input
              type="search"
              value={settings.search}
              onChange={(event) => settings.setSearch(event.target.value)}
              placeholder="Search templates"
            />
          </label>

          <label>
            Status
            <select value={settings.statusFilter} onChange={(event) => settings.setStatusFilter(event.target.value)}>
              <option value="">All statuses</option>
              {filterOptions.statuses.map((status) => (
                <option key={status} value={status}>{status}</option>
              ))}
            </select>
          </label>

          <label>
            Language
            <select value={settings.languageFilter} onChange={(event) => settings.setLanguageFilter(event.target.value)}>
              <option value="">All languages</option>
              {filterOptions.languages.map((language) => (
                <option key={language} value={language}>{language}</option>
              ))}
            </select>
          </label>

          <label>
            Category
            <select value={settings.categoryFilter} onChange={(event) => settings.setCategoryFilter(event.target.value)}>
              <option value="">All categories</option>
              {filterOptions.categories.map((category) => (
                <option key={category} value={category}>{category}</option>
              ))}
            </select>
          </label>

          <label>
            Page Size
            <select value={settings.pageSize} onChange={(event) => settings.setPageSize(Number(event.target.value))}>
              {[10, 20, 50].map((size) => (
                <option key={size} value={size}>{size}</option>
              ))}
            </select>
          </label>

          <label className="sf-host-checkbox-row">
            <input
              type="checkbox"
              checked={settings.approvedOnly}
              onChange={(event) => settings.setApprovedOnly(event.target.checked)}
            />
            Approved only
          </label>
        </section>
      </div>

      <div className="sf-whatsapp-grid">
        <section className="sf-whatsapp-list" aria-label="Template list">
          {settings.templatesError ? (
            <div className="sf-whatsapp-empty" role="alert">
              <p>{settings.templatesError}</p>
            </div>
          ) : null}

          {settings.isLoadingTemplates ? <div className="sf-whatsapp-empty">Loading templates...</div> : null}

          {!settings.isLoadingTemplates && templates.length === 0 ? (
            <div className="sf-whatsapp-empty" role="status">
              <h3>No templates found</h3>
              <p>Adjust filters or sync templates to refresh provider data.</p>
            </div>
          ) : null}

          <div className="sf-whatsapp-template-list" role="list">
            {templates.map((template) => (
              <button
                key={template.id}
                type="button"
                role="listitem"
                className={`sf-whatsapp-template-item${settings.selectedTemplate?.id === template.id ? " is-selected" : ""}`}
                onClick={() => {
                  void settings.selectTemplate(template.id);
                }}
              >
                <div className="sf-whatsapp-template-title-row">
                  <h3>{template.name}</h3>
                  <span className={`sf-whatsapp-template-badge ${template.isApproved ? "approved" : "pending"}`}>
                    {template.status}
                  </span>
                </div>
                <p>{template.languageCode} · {template.category}</p>
                <p>{template.variableCount} variable{template.variableCount === 1 ? "" : "s"}</p>
              </button>
            ))}
          </div>

          <footer className="sf-host-pagination" aria-label="Template pagination">
            <button
              type="button"
              onClick={() => settings.setPage(settings.page - 1)}
              disabled={settings.page <= 1 || settings.isLoadingTemplates}
            >
              Previous
            </button>
            <span>
              Page {settings.templatesResponse?.page ?? 1} of {settings.templatesResponse?.totalPages ?? 1}
            </span>
            <button
              type="button"
              onClick={() => settings.setPage((settings.templatesResponse?.page ?? 1) + 1)}
              disabled={
                !settings.templatesResponse
                || settings.templatesResponse.page >= settings.templatesResponse.totalPages
                || settings.isLoadingTemplates
              }
            >
              Next
            </button>
          </footer>
        </section>

        <aside className="sf-whatsapp-preview" aria-label="Template preview">
          <h2>Preview</h2>

          {settings.isLoadingTemplateDetail ? <p>Loading preview...</p> : null}

          {!settings.selectedTemplate && !settings.isLoadingTemplateDetail ? (
            <p>Select a template to view details and content preview.</p>
          ) : null}

          {settings.selectedTemplate ? (
            <div className="sf-whatsapp-preview-card">
              <h3>{settings.selectedTemplate.name}</h3>
              <dl className="sf-whatsapp-definition-grid">
                <div>
                  <dt>Language</dt>
                  <dd>{settings.selectedTemplate.languageCode}</dd>
                </div>
                <div>
                  <dt>Category</dt>
                  <dd>{settings.selectedTemplate.category}</dd>
                </div>
                <div>
                  <dt>Status</dt>
                  <dd>{settings.selectedTemplate.status}</dd>
                </div>
                <div>
                  <dt>Approved</dt>
                  <dd>{settings.selectedTemplate.isApproved ? "Yes" : "No"}</dd>
                </div>
                <div>
                  <dt>Active</dt>
                  <dd>{settings.selectedTemplate.isActive ? "Yes" : "No"}</dd>
                </div>
                <div>
                  <dt>Variable Count</dt>
                  <dd>{settings.selectedTemplate.variableCount}</dd>
                </div>
                <div>
                  <dt>Last Synced</dt>
                  <dd>{formatDateTime(settings.selectedTemplate.lastSyncedAt)}</dd>
                </div>
              </dl>

              {settings.selectedTemplate.headerType ? (
                <section>
                  <h4>Header</h4>
                  <p>{settings.selectedTemplate.headerType}</p>
                </section>
              ) : null}

              <section>
                <h4>Body</h4>
                <p>{settings.selectedTemplate.bodyText}</p>
              </section>

              {settings.selectedTemplate.footerText ? (
                <section>
                  <h4>Footer</h4>
                  <p>{settings.selectedTemplate.footerText}</p>
                </section>
              ) : null}

              {settings.selectedTemplate.variables.length > 0 ? (
                <section>
                  <h4>Variables</h4>
                  <ul>
                    {settings.selectedTemplate.variables.map((variable) => (
                      <li key={variable.position}>#{variable.position} {variable.placeholder}</li>
                    ))}
                  </ul>
                </section>
              ) : null}
            </div>
          ) : null}
        </aside>
      </div>
    </div>
  );
}
