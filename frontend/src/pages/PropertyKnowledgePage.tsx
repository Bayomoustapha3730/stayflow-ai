import { useEffect, useMemo, useState } from "react";
import { ApiError, HttpClient } from "../api/httpClient";
import { HostLoginPanel } from "../components/host";
import { HostConsoleNav } from "../components/host/HostConsoleNav";
import { PropertyKnowledgeCard } from "../components/knowledge/PropertyKnowledgeCard";
import { PropertyKnowledgeFilters } from "../components/knowledge/PropertyKnowledgeFilters";
import { PropertyKnowledgeForm, createDraftFromValue, type PropertyKnowledgeFormDraft } from "../components/knowledge/PropertyKnowledgeForm";
import { PropertyKnowledgePreview } from "../components/knowledge/PropertyKnowledgePreview";
import { useHostAuth } from "../hooks/useHostAuth";
import { usePropertyKnowledge } from "../hooks/usePropertyKnowledge";
import {
  getPropertyKnowledgeActionErrorMessage,
  getPropertyKnowledgeSaveErrorMessage
} from "../utils/propertyKnowledgeErrors";
import {
  type CreatePropertyKnowledgeRequest,
  PropertyKnowledgeCategory,
  propertyKnowledgeCategoryLabels,
  type PropertyKnowledgeDetail,
  type PropertyKnowledgeSummary,
  type UpdatePropertyKnowledgeRequest
} from "../models/propertyKnowledge";
import type { PagedResult } from "../models/chat";
import "../styles/property-knowledge.css";

interface PropertyKnowledgePageProps {
  propertyId: string | null;
}

type DialogState =
  | { mode: "create" }
  | { mode: "edit"; itemId: string }
  | { mode: "preview"; itemId: string }
  | null;

const blankDraft: PropertyKnowledgeFormDraft = {
  category: PropertyKnowledgeCategory.Other,
  title: "",
  summary: "",
  content: "",
  tagsText: "",
  priority: "0",
  isActive: true
};

interface HostPropertySummary {
  id: string;
  name: string;
}

export function PropertyKnowledgePage({ propertyId }: PropertyKnowledgePageProps) {
  const auth = useHostAuth();
  const [resolvedPropertyId, setResolvedPropertyId] = useState<string | null>(null);
  const [isResolvingProperty, setIsResolvingProperty] = useState(false);
  const [propertyResolutionError, setPropertyResolutionError] = useState<string | null>(null);

  useEffect(() => {
    if (propertyId) {
      setResolvedPropertyId(null);
      setPropertyResolutionError(null);
      setIsResolvingProperty(false);
      return;
    }

    if (!auth.isAuthenticated || !auth.accessToken) {
      setResolvedPropertyId(null);
      setPropertyResolutionError(null);
      setIsResolvingProperty(false);
      return;
    }

    const controller = new AbortController();
    let isActive = true;

    async function resolveDefaultProperty() {
      setIsResolvingProperty(true);
      setPropertyResolutionError(null);

      try {
        const http = new HttpClient({
          baseUrl: import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243",
          getAccessToken: () => auth.accessToken
        });

        const page = await http.get<PagedResult<HostPropertySummary>>(
          "/properties?pageNumber=1&pageSize=1",
          { signal: controller.signal }
        );

        if (!isActive) {
          return;
        }

        setResolvedPropertyId(page.items[0]?.id ?? null);
      } catch (error) {
        if (!isActive) {
          return;
        }

        if (error instanceof ApiError && error.status === 401) {
          auth.logout();
          return;
        }

        setResolvedPropertyId(null);
        setPropertyResolutionError("Property unavailable");
      } finally {
        if (isActive) {
          setIsResolvingProperty(false);
        }
      }
    }

    void resolveDefaultProperty();

    return () => {
      isActive = false;
      controller.abort();
    };
  }, [auth.accessToken, auth.isAuthenticated, auth.logout, propertyId]);

  const effectivePropertyId = propertyId ?? resolvedPropertyId;

  const knowledge = usePropertyKnowledge({
    propertyId: effectivePropertyId,
    accessToken: auth.accessToken,
    onUnauthorized: auth.logout
  });
  const [dialog, setDialog] = useState<DialogState>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  const selectedSummary = useMemo(
    () => knowledge.response?.items.find((item) => item.id === knowledge.selectedKnowledgeId) ?? null,
    [knowledge.response?.items, knowledge.selectedKnowledgeId]
  );

  const selectedPropertyName = knowledge.propertyName || selectedSummary?.propertyName || null;
  const propertyKnowledgeHref = effectivePropertyId ? `/host/properties/${effectivePropertyId}/knowledge` : null;

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

  const items = knowledge.response?.items ?? [];
  const totalCount = knowledge.response?.totalCount ?? 0;
  const totalPages = knowledge.response?.totalPages ?? 1;

  async function handleCreate(submission: CreatePropertyKnowledgeRequest) {
    setStatusMessage(null);
    setActionError(null);
    try {
      const created = await knowledge.createKnowledge(submission);
      setDialog({ mode: "preview", itemId: created.id });
      setStatusMessage("Knowledge item created successfully.");
      return created;
    } catch (error) {
      setFormError(getPropertyKnowledgeSaveErrorMessage(error));
    }
  }

  async function handleUpdate(itemId: string, submission: UpdatePropertyKnowledgeRequest) {
    setStatusMessage(null);
    setActionError(null);
    try {
      const updated = await knowledge.updateKnowledge(itemId, submission);
      setDialog({ mode: "preview", itemId: updated.id });
      setStatusMessage("Knowledge item updated successfully.");
      return updated;
    } catch (error) {
      setFormError(getPropertyKnowledgeSaveErrorMessage(error));
    }
  }

  async function runAction(operation: () => Promise<unknown>, action: "approve" | "activate" | "delete") {
    try {
      setStatusMessage(null);
      setActionError(null);
      await operation();
    } catch (error) {
      setActionError(getPropertyKnowledgeActionErrorMessage(action, error));
    }
  }

  async function openEdit(itemId: string) {
    setFormError(null);
    setDialog({ mode: "edit", itemId });
    await knowledge.selectKnowledge(itemId);
  }

  async function openPreview(itemId: string) {
    setDialog({ mode: "preview", itemId });
    await knowledge.selectKnowledge(itemId);
  }

  const activeItem = knowledge.selectedKnowledge;

  return (
    <div className="sf-host-page sf-knowledge-page">
      <div className="sf-host-page-top">
        <header className="sf-knowledge-header">
          <div>
            <p className="sf-host-kicker">StayFlow Host Console</p>
            <h1>Property Knowledge</h1>
            <p className="sf-knowledge-property-name">{selectedPropertyName || (effectivePropertyId ? "Loading property name..." : isResolvingProperty ? "Resolving property..." : "Property unavailable")}</p>
          </div>

          <div className="sf-knowledge-header-actions">
            <button type="button" onClick={() => setDialog({ mode: "create" })} disabled={!effectivePropertyId}>
              Create Knowledge
            </button>
            <button type="button" onClick={() => knowledge.refresh()} disabled={knowledge.isLoading || knowledge.isRefreshing}>
              {knowledge.isRefreshing ? "Refreshing..." : "Refresh"}
            </button>
          </div>
        </header>

        <HostConsoleNav
          conversationsHref="/host/conversations"
          copilotWorkspaceHref="/host/copilot"
          propertyKnowledgeHref={propertyKnowledgeHref}
          whatsappSettingsHref="/host/settings/whatsapp"
          organizationSettingsHref="/host/settings/organization"
          current="knowledge"
        />

        {!effectivePropertyId ? (
          <p className="sf-knowledge-help">
            {propertyResolutionError ?? "Select a conversation with a property first."}
          </p>
        ) : null}

        {knowledge.error ? (
          <div className="sf-host-inline-error" role="alert">
            <p>{knowledge.error}</p>
            <button type="button" onClick={() => knowledge.retry()}>Retry</button>
          </div>
        ) : null}

        {statusMessage ? (
          <div className="sf-knowledge-status" role="status" aria-live="polite">{statusMessage}</div>
        ) : null}

        {actionError ? (
          <div className="sf-knowledge-error" role="alert">
            {actionError}
          </div>
        ) : null}

        <PropertyKnowledgeFilters
          search={knowledge.search}
          category={knowledge.category}
          isApproved={knowledge.approvalFilter === "all" ? undefined : knowledge.approvalFilter === "approved"}
          isActive={knowledge.activeFilter === "all" ? undefined : knowledge.activeFilter === "active"}
          pageSize={knowledge.pageSize}
          onSearchChange={knowledge.setSearch}
          onCategoryChange={knowledge.setCategory}
          onApprovalChange={(value) => knowledge.setApprovalFilter(value === undefined ? "all" : value ? "approved" : "unapproved")}
          onActiveChange={(value) => knowledge.setActiveFilter(value === undefined ? "all" : value ? "active" : "inactive")}
          onPageSizeChange={knowledge.setPageSize}
        />

        <section className="sf-knowledge-summary-row" aria-label="Property knowledge results summary">
          <div>
            <h2>{totalCount}</h2>
            <p>Total items</p>
          </div>
          <div>
            <h2>{knowledge.page}</h2>
            <p>Current page</p>
          </div>
          <div>
            <h2>{items.filter((item) => item.canBeUsedByAI).length}</h2>
            <p>AI eligible on page</p>
          </div>
          <div>
            <h2>{selectedSummary?.categoryLabel || "Preview"}</h2>
            <p>Selected state</p>
          </div>
        </section>
      </div>

      <div className="sf-knowledge-grid">
        <section className="sf-knowledge-list-column" aria-label="Property knowledge list">
          {knowledge.isLoading ? <div className="sf-knowledge-state">Loading property knowledge...</div> : null}
          {!knowledge.isLoading && items.length === 0 ? (
            <div className="sf-knowledge-state" role="status">
              <h3>No property knowledge yet</h3>
              <p>Create approved knowledge for Wi-Fi, check-in, parking, house rules, and more.</p>
            </div>
          ) : null}

          <div className="sf-knowledge-list">
            {items.map((item) => (
              <PropertyKnowledgeCard
                key={item.id}
                item={item}
                isSelected={knowledge.selectedKnowledgeId === item.id}
                onView={() => void openPreview(item.id)}
                onEdit={() => void openEdit(item.id)}
                onToggleApproval={() => {
                  void runAction(() => (item.isApproved ? knowledge.unapproveKnowledge(item.id) : knowledge.approveKnowledge(item.id)), "approve");
                }}
                onToggleActive={() => {
                  void runAction(() => (item.isActive ? knowledge.deactivateKnowledge(item.id) : knowledge.activateKnowledge(item.id)), "activate");
                }}
                onDelete={() => {
                  if (!window.confirm(`Delete "${item.title}"? This cannot be undone.`)) {
                    return;
                  }

                  void runAction(() => knowledge.deleteKnowledge(item.id), "delete");
                }}
              />
            ))}
          </div>

          <footer className="sf-knowledge-pagination" aria-label="Property knowledge pagination">
            <button type="button" onClick={() => knowledge.setPage(knowledge.page - 1)} disabled={knowledge.page <= 1 || knowledge.isLoading || knowledge.isRefreshing}>
              Previous
            </button>
            <span>Page {knowledge.page} of {totalPages}</span>
            <button type="button" onClick={() => knowledge.setPage(knowledge.page + 1)} disabled={knowledge.page >= totalPages || knowledge.isLoading || knowledge.isRefreshing}>
              Next
            </button>
          </footer>
        </section>

        <aside className="sf-knowledge-preview-column" aria-label="Knowledge preview">
          {knowledge.selectedKnowledgeError ? (
            <div className="sf-knowledge-state" role="alert">
              <p>{knowledge.selectedKnowledgeError}</p>
            </div>
          ) : knowledge.selectedKnowledge ? (
            <PropertyKnowledgePreview item={knowledge.selectedKnowledge} />
          ) : (
            <div className="sf-knowledge-state" role="status">
              <h3>Select a knowledge item</h3>
              <p>Choose View to inspect the AI-visible preview and metadata.</p>
            </div>
          )}
        </aside>
      </div>

      {dialog?.mode === "create" ? (
        <div className="sf-knowledge-modal" role="dialog" aria-modal="true" aria-label="Create property knowledge">
          <div className="sf-knowledge-modal-surface">
            <PropertyKnowledgeForm
              heading="Create knowledge item"
              submitLabel="Create"
              initialValue={blankDraft}
              isSaving={knowledge.isCreating}
              error={formError}
              onSubmit={async (submission) => {
                setFormError(null);
                await handleCreate({
                  category: submission.category,
                  title: submission.title,
                  summary: submission.summary || undefined,
                  content: submission.content,
                  tags: submission.tags,
                  priority: submission.priority,
                  isActive: submission.isActive
                });
              }}
              onCancel={() => setDialog(null)}
            />
          </div>
        </div>
      ) : null}

      {dialog?.mode === "edit" && knowledge.selectedKnowledgeError ? (
        <div className="sf-knowledge-modal" role="dialog" aria-modal="true" aria-label="Knowledge item unavailable">
          <div className="sf-knowledge-modal-surface">
            <div className="sf-knowledge-state" role="alert">
              <p>{knowledge.selectedKnowledgeError}</p>
              <button type="button" onClick={() => setDialog(null)}>Close</button>
            </div>
          </div>
        </div>
      ) : dialog?.mode === "edit" && knowledge.selectedKnowledge ? (
        <div className="sf-knowledge-modal" role="dialog" aria-modal="true" aria-label="Edit property knowledge">
          <div className="sf-knowledge-modal-surface">
            <PropertyKnowledgeForm
              heading="Edit knowledge item"
              submitLabel="Save changes"
              initialValue={createDraftFromValue(knowledge.selectedKnowledge)}
              isSaving={knowledge.isUpdating}
              error={formError}
              showApprovalNotice={knowledge.selectedKnowledge.isApproved}
              onSubmit={async (submission) => {
                setFormError(null);
                await handleUpdate(knowledge.selectedKnowledge!.id, {
                  category: submission.category,
                  title: submission.title,
                  summary: submission.summary || undefined,
                  content: submission.content,
                  tags: submission.tags,
                  priority: submission.priority,
                  isActive: submission.isActive
                });
              }}
              onCancel={() => setDialog(null)}
            />
          </div>
        </div>
      ) : dialog?.mode === "edit" ? (
        <div className="sf-knowledge-modal" role="dialog" aria-modal="true" aria-label="Loading knowledge item">
          <div className="sf-knowledge-modal-surface">
            <div className="sf-knowledge-state">Loading knowledge item...</div>
          </div>
        </div>
      ) : null}

      {dialog?.mode === "preview" && knowledge.selectedKnowledgeError ? (
        <div className="sf-knowledge-modal" role="dialog" aria-modal="true" aria-label="Knowledge item unavailable">
          <div className="sf-knowledge-modal-surface">
            <div className="sf-knowledge-state" role="alert">
              <p>{knowledge.selectedKnowledgeError}</p>
              <button type="button" onClick={() => setDialog(null)}>Close</button>
            </div>
          </div>
        </div>
      ) : dialog?.mode === "preview" && knowledge.selectedKnowledge ? (
        <div className="sf-knowledge-modal" role="dialog" aria-modal="true" aria-label="AI-visible preview">
          <div className="sf-knowledge-modal-surface">
            <PropertyKnowledgePreview item={knowledge.selectedKnowledge} />
            <div className="sf-knowledge-preview-actions">
              <button type="button" onClick={() => setDialog(null)}>Close</button>
              <button type="button" onClick={() => void openEdit(knowledge.selectedKnowledge!.id)}>Edit</button>
            </div>
          </div>
        </div>
      ) : dialog?.mode === "preview" ? (
        <div className="sf-knowledge-modal" role="dialog" aria-modal="true" aria-label="Loading preview">
          <div className="sf-knowledge-modal-surface">
            <div className="sf-knowledge-state">Loading preview...</div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
