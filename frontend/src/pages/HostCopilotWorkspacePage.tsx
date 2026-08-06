import { useEffect, useMemo, useState } from "react";
import { HostConsoleNav, HostLoginPanel } from "../components/host";
import { useHostAuth } from "../hooks/useHostAuth";
import { useHostCopilotWorkspace } from "../hooks/useHostCopilotWorkspace";
import "../styles/host-inbox.css";

function formatTime(value?: string | null): string {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "-";
  }

  return date.toLocaleString();
}

export function HostCopilotWorkspacePage() {
  const auth = useHostAuth();
  const workspace = useHostCopilotWorkspace({
    accessToken: auth.accessToken,
    onUnauthorized: auth.logout
  });
  const [tone, setTone] = useState("professional");
  const [instruction, setInstruction] = useState("");
  const [draftText, setDraftText] = useState("");

  const selectedItem = useMemo(() => {
    return workspace.workspace?.items.find((item) => item.workItemId === workspace.selectedWorkItemId) ?? null;
  }, [workspace.selectedWorkItemId, workspace.workspace?.items]);

  useEffect(() => {
    if (!workspace.draftResult) {
      return;
    }

    setDraftText(workspace.draftResult.draft);
  }, [workspace.draftResult]);

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
    <div className="sf-host-page">
      <div className="sf-host-page-top">
        <header className="sf-knowledge-header">
          <div>
            <p className="sf-host-kicker">StayFlow Host Console</p>
            <h1>Host Copilot Workspace</h1>
            <p className="sf-host-muted-note">
              Tenant-scoped work-item aggregation with deterministic priority, safety, SLA monitoring, and operational recommendations.
            </p>
          </div>

          <div className="sf-knowledge-header-actions">
            <button
              type="button"
              onClick={() => {
                void workspace.refresh();
              }}
              disabled={workspace.isLoading}
            >
              {workspace.isLoading ? "Refreshing..." : "Refresh"}
            </button>
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
          current="copilot"
        />

        <section className="sf-knowledge-summary-row" aria-label="Host copilot summary">
          <div>
            <h2>{workspace.workspace?.totalOpenItems ?? 0}</h2>
            <p>Open work items</p>
          </div>
          <div>
            <h2>{workspace.workspace?.totalBreachedSlaItems ?? 0}</h2>
            <p>Breached SLA</p>
          </div>
          <div>
            <h2>{workspace.realtimeState}</h2>
            <p>Realtime state</p>
          </div>
          <div>
            <h2>{formatTime(workspace.workspace?.generatedAt)}</h2>
            <p>Last refresh</p>
          </div>
        </section>

        {workspace.error ? (
          <div className="sf-host-inline-error" role="alert">
            <p>{workspace.error}</p>
          </div>
        ) : null}
      </div>

      <div className="sf-host-main-grid">
        <section className="sf-host-list-column" aria-label="Host copilot work items">
          <ul className="sf-host-conversation-list">
            {(workspace.workspace?.items ?? []).map((item) => (
              <li key={item.workItemId} className="sf-host-conversation-list-item">
                <button
                  type="button"
                  className={`sf-host-conversation-card ${workspace.selectedWorkItemId === item.workItemId ? "selected" : ""}`}
                  onClick={() => workspace.selectWorkItem(item.workItemId)}
                >
                  <p className="sf-host-conversation-title">{item.guestName}</p>
                  <p className="sf-host-conversation-meta">{item.propertyName}</p>
                  <p className="sf-host-conversation-preview">{item.summary.lastGuestMessagePreview}</p>
                  <div className="sf-host-conversation-status-row">
                    <span className="sf-host-badge">{item.priority}</span>
                    <span className="sf-host-badge">{item.safetyClassification}</span>
                    <span className="sf-host-badge">{item.sla.alertLevel}</span>
                  </div>
                </button>
              </li>
            ))}
          </ul>
        </section>

        <section className="sf-host-detail-column" aria-label="Selected host copilot work item">
          {!selectedItem ? (
            <div className="sf-host-conversation-selection-placeholder">
              <p>Select a work item to inspect summary, timeline, recommendations, and drafts.</p>
            </div>
          ) : (
            <div className="sf-host-detail-card-stack">
              <article className="sf-host-detail-section">
                <div className="sf-host-detail-section-header">
                  <div>
                    <h3>{selectedItem.summary.headline}</h3>
                    <p>{selectedItem.priorityReason}</p>
                  </div>
                </div>
                <p><strong>Intent:</strong> {selectedItem.summary.lastGuestIntent}</p>
                <p><strong>SLA:</strong> {selectedItem.sla.alertMessage}</p>
                <p><strong>Due:</strong> {formatTime(selectedItem.sla.responseDueAt)}</p>
              </article>

              <article className="sf-host-detail-section">
                <div className="sf-host-detail-section-header">
                  <div>
                    <h3>Operational timeline</h3>
                    <p>Recent events and audit trail</p>
                  </div>
                </div>
                <ul className="sf-host-message-list">
                  {selectedItem.timeline.map((event, index) => (
                    <li key={`${event.timestamp}-${event.eventType}-${index}`} className="sf-host-message-item">
                      <p><strong>{event.eventType}:</strong> {event.title}</p>
                      <p>{event.detail}</p>
                      <time dateTime={event.timestamp}>{formatTime(event.timestamp)}</time>
                    </li>
                  ))}
                </ul>
              </article>

              <article className="sf-host-detail-section">
                <div className="sf-host-detail-section-header">
                  <div>
                    <h3>Explainable recommendations</h3>
                    <p>Deterministic recommendation rules</p>
                  </div>
                </div>
                <ul className="sf-host-message-list">
                  {selectedItem.recommendations.map((recommendation) => (
                    <li key={recommendation.code} className="sf-host-message-item">
                      <p><strong>{recommendation.title}</strong> ({recommendation.confidence}%)</p>
                      <p>{recommendation.reason}</p>
                      <p>{recommendation.suggestedAction}</p>
                    </li>
                  ))}
                </ul>
              </article>

              <article className="sf-host-detail-section">
                <div className="sf-host-detail-section-header">
                  <div>
                    <h3>Contextual draft reply</h3>
                    <p>Generate, validate, then send with host control</p>
                  </div>
                </div>

                <div className="sf-host-copilot-controls">
                  <label htmlFor="host-copilot-tone">Tone</label>
                  <select id="host-copilot-tone" value={tone} onChange={(event) => setTone(event.target.value)}>
                    <option value="professional">Professional</option>
                    <option value="friendly">Friendly</option>
                    <option value="luxury">Luxury</option>
                    <option value="casual">Casual</option>
                  </select>
                </div>

                <label htmlFor="host-copilot-instruction">Instruction</label>
                <textarea
                  id="host-copilot-instruction"
                  rows={2}
                  value={instruction}
                  onChange={(event) => setInstruction(event.target.value)}
                  placeholder="Optional host guidance"
                />

                <div className="sf-host-conversation-actions-row">
                  <button
                    type="button"
                    onClick={() => {
                      void workspace.generateDraft(selectedItem.conversationId, tone, instruction);
                    }}
                  >
                    Generate Draft
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      void workspace.validateDraft(selectedItem.conversationId, draftText);
                    }}
                  >
                    Validate Draft
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      void workspace.sendDraft(selectedItem.conversationId, draftText);
                    }}
                    disabled={workspace.isSendingDraft}
                  >
                    {workspace.isSendingDraft ? "Sending..." : "Send Draft"}
                  </button>
                </div>

                <textarea
                  rows={5}
                  value={draftText}
                  onChange={(event) => setDraftText(event.target.value)}
                  placeholder="Draft text"
                />

                {workspace.draftResult?.conversationId === selectedItem.conversationId ? (
                  <p className="sf-host-muted-note">
                    Mode: {workspace.draftResult.generationMode} | {workspace.draftResult.rationale}
                  </p>
                ) : null}

                {workspace.validationResult ? (
                  <div>
                    <p><strong>Validation:</strong> {workspace.validationResult.isValid ? "Valid" : "Invalid"}</p>
                    {workspace.validationResult.errors.map((error) => (
                      <p key={error} className="sf-host-inline-error">{error}</p>
                    ))}
                    {workspace.validationResult.warnings.map((warning) => (
                      <p key={warning} className="sf-host-muted-note">{warning}</p>
                    ))}
                  </div>
                ) : null}
              </article>

              <article className="sf-host-detail-section">
                <div className="sf-host-detail-section-header">
                  <div>
                    <h3>Pending host actions</h3>
                    <p>Approve or decline operational actions</p>
                  </div>
                </div>

                {selectedItem.pendingActions.length === 0 ? (
                  <p className="sf-host-muted-note">No pending host approvals.</p>
                ) : (
                  <ul className="sf-host-message-list">
                    {selectedItem.pendingActions.map((action) => (
                      <li key={action.actionId} className="sf-host-message-item">
                        <p><strong>{action.actionType}</strong></p>
                        <p>Status: {action.status}</p>
                        <p>Created: {formatTime(action.createdAt)}</p>
                        <p>Expires: {formatTime(action.expiresAt)}</p>
                        <div className="sf-host-conversation-actions-row">
                          <button
                            type="button"
                            onClick={() => {
                              void workspace.approveAction(action.actionId, "Approved from Host Copilot workspace");
                            }}
                          >
                            Approve
                          </button>
                          <button
                            type="button"
                            onClick={() => {
                              void workspace.declineAction(action.actionId, "Declined from Host Copilot workspace");
                            }}
                          >
                            Decline
                          </button>
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
              </article>
            </div>
          )}
        </section>
      </div>
    </div>
  );
}
