import {
  HostConversationDetail,
  HostConversationFilters,
  HostConversationList,
  HostInboxHeader,
  HostInboxSummary,
  HostLoginPanel
} from "../components/host";
import { createOnboardingApi } from "../api/onboardingApi";
import { CopilotPanel } from "../components/copilot";
import { useHostAuth } from "../hooks/useHostAuth";
import { useConversationCopilot } from "../hooks/useConversationCopilot";
import { useHostConversations } from "../hooks/useHostConversations";
import { useEffect, useMemo, useRef, useState } from "react";
import { ConversationSenderType } from "../models/enums";
import { HostConsoleNav } from "../components/host/HostConsoleNav";
import { HttpClient } from "../api/httpClient";
import { getRuntimeApiUrl } from "../runtimeConfig";
import { normalizePropertyId, resolvePropertyKnowledgePropertyId } from "../utils/propertyRouting";
import "../styles/host-inbox.css";

const notificationsPreferenceKey = "stayflow.host.notifications.enabled";

function truncatePreview(value: string | null | undefined): string {
  if (!value) {
    return "New guest message";
  }

  const trimmed = value.trim();
  if (trimmed.length <= 110) {
    return trimmed;
  }

  return `${trimmed.slice(0, 107)}...`;
}

export function HostInboxPage() {
  const auth = useHostAuth();
  const conversations = useHostConversations({
    accessToken: auth.accessToken,
    onUnauthorized: auth.logout
  });
  const [notificationPreferenceEnabled, setNotificationPreferenceEnabled] = useState(
    () => localStorage.getItem(notificationsPreferenceKey) === "true"
  );
  const [showOnboardingPrompt, setShowOnboardingPrompt] = useState(false);
  const [copilotDraft, setCopilotDraft] = useState<string | null>(null);
  const [copilotDraftVersion, setCopilotDraftVersion] = useState(0);
  const previousMessageTimestampsRef = useRef<Record<string, string | null>>({});
  const selectedConversationId = conversations.selectedConversationId;
  const selectedConversation = conversations.response?.items.find((item) => item.conversationId === selectedConversationId) ?? null;
  const selectedConversationPropertyId = normalizePropertyId(selectedConversation?.propertyId ?? null);
  const configuredDemoPropertyId = normalizePropertyId(import.meta.env.VITE_DEMO_PROPERTY_ID);
  const resolvedKnowledgePropertyId = resolvePropertyKnowledgePropertyId(
    selectedConversationPropertyId,
    configuredDemoPropertyId,
    import.meta.env.DEV
  );
  const copilot = useConversationCopilot({
    conversationId: selectedConversationId,
    accessToken: auth.accessToken,
    onUnauthorized: auth.logout
  });

  const notificationsSupported = useMemo(() => typeof Notification !== "undefined", []);
  const notificationsEnabled = notificationsSupported
    && notificationPreferenceEnabled
    && Notification.permission === "granted";

  useEffect(() => {
    const current = conversations.response?.items ?? [];
    if (!notificationsEnabled) {
      previousMessageTimestampsRef.current = Object.fromEntries(
        current.map((item) => [item.conversationId, item.latestVisibleMessageTimestamp ?? null])
      );
      return;
    }

    for (const item of current) {
      const previousTimestamp = previousMessageTimestampsRef.current[item.conversationId] ?? null;
      const latestTimestamp = item.latestVisibleMessageTimestamp ?? null;
      const hasNewMessage = Boolean(latestTimestamp && latestTimestamp !== previousTimestamp);
      const isGuestMessage = item.latestVisibleMessageSenderType === ConversationSenderType.Guest;
      const isSelectedAndVisible =
        item.conversationId === conversations.selectedConversationId
        && document.visibilityState === "visible";

      if (
        hasNewMessage
        && isGuestMessage
        && !isSelectedAndVisible
      ) {
        const notification = new Notification(
          `${item.guest?.fullName?.trim() || "Guest"} - ${item.property?.name?.trim() || "Property"}`,
          {
            body: truncatePreview(item.latestVisibleMessagePreview),
            tag: `conversation-${item.conversationId}`
          }
        );

        notification.onclick = () => {
          window.focus();
          conversations.selectConversation(item.conversationId);
          notification.close();
        };
      }
    }

    previousMessageTimestampsRef.current = Object.fromEntries(
      current.map((item) => [item.conversationId, item.latestVisibleMessageTimestamp ?? null])
    );
  }, [conversations.response?.items, conversations.selectedConversationId, conversations.selectConversation, notificationsEnabled]);

  useEffect(() => {
    if (!auth.accessToken) {
      setShowOnboardingPrompt(false);
      return;
    }

    const http = new HttpClient({
      baseUrl: getRuntimeApiUrl(),
      getAccessToken: () => auth.accessToken
    });
    const onboardingApi = createOnboardingApi(http);

    void onboardingApi.getStatus()
      .catch(() => onboardingApi.start())
      .then((status) => {
        setShowOnboardingPrompt(!status.isCompleted);
      })
      .catch(() => {
        setShowOnboardingPrompt(false);
      });
  }, [auth.accessToken]);

  async function enableNotifications() {
    if (!notificationsSupported) {
      return;
    }

    localStorage.setItem(notificationsPreferenceKey, "true");
    setNotificationPreferenceEnabled(true);

    if (Notification.permission === "default") {
      try {
        await Notification.requestPermission();
      } catch {
        // Browser-level permission errors are intentionally ignored.
      }
    }
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

  const response = conversations.response;
  const items = response?.items ?? [];

  return (
    <div className="sf-host-page">
      <div className="sf-host-page-top">
        <HostInboxHeader
          isRefreshing={conversations.isLoading}
          realtimeState={conversations.realtimeState}
          totalUnreadCount={conversations.totalUnreadCount}
          notificationsEnabled={notificationsEnabled}
          notificationsSupported={notificationsSupported}
          onRefresh={() => {
            void conversations.refresh();
          }}
          onEnableNotifications={() => {
            void enableNotifications();
          }}
          onSignOut={() => {
            auth.logout();
          }}
        />

        <HostConsoleNav
          auth={auth}
          conversationsHref="/host/conversations"
          copilotWorkspaceHref="/host/copilot"
          propertyKnowledgeHref={resolvedKnowledgePropertyId ? `/host/properties/${resolvedKnowledgePropertyId}/knowledge` : null}
          billingHref="/host/settings/billing"
          whatsappSettingsHref="/host/settings/whatsapp"
          organizationSettingsHref="/host/settings/organization"
          organizationsHref="/host/organizations"
          accountSettingsHref="/host/settings/account"
          current="conversations"
        />

        {!resolvedKnowledgePropertyId ? (
          <p className="sf-host-muted-note">Select a conversation with a property first.</p>
        ) : null}

        {conversations.sessionExpired ? (
          <div className="sf-host-session-expired" role="alert">
            Your host session expired. Please sign in again.
          </div>
        ) : null}

        {showOnboardingPrompt ? (
          <div className="sf-whatsapp-status" role="status">
            Onboarding is still in progress. <a href="/onboarding">Resume setup</a>
          </div>
        ) : null}

        <HostInboxSummary totalCount={response?.totalCount ?? 0} page={response?.page ?? 1} items={items} />

        <HostConversationFilters
          search={conversations.search}
          status={conversations.status}
          requiresHostAttention={conversations.requiresHostAttention}
          pageSize={conversations.pageSize}
          onSearchChange={conversations.setSearch}
          onStatusChange={conversations.setStatus}
          onRequiresHostAttentionChange={conversations.setRequiresHostAttention}
          onPageSizeChange={conversations.setPageSize}
        />
      </div>

      <div className="sf-host-main-grid">
        <section className="sf-host-list-column" aria-label="Conversation inbox">
          <HostConversationList
            isLoading={conversations.isLoading}
            error={conversations.error}
            items={items}
            selectedConversationId={selectedConversationId}
            onRetry={() => {
              void conversations.refresh();
            }}
            onSelect={conversations.selectConversation}
          />

          <footer className="sf-host-pagination" aria-label="Conversation pagination">
            <button
              type="button"
              onClick={() => conversations.setPage(conversations.page - 1)}
              disabled={conversations.page <= 1 || conversations.isLoading}
            >
              Previous
            </button>

            <span>
              Page {response?.page ?? 1} of {response?.totalPages ?? 1}
            </span>

            <button
              type="button"
              onClick={() => conversations.setPage((response?.page ?? 1) + 1)}
              disabled={!response || response.page >= response.totalPages || conversations.isLoading}
            >
              Next
            </button>
          </footer>
        </section>

        <section className="sf-host-conversation-column" aria-label="Conversation workspace">
          <HostConversationDetail
            conversationId={selectedConversationId}
            accessToken={auth.accessToken}
            onUnauthorized={auth.logout}
            externalDraft={copilotDraft}
            externalDraftVersion={copilotDraftVersion}
            onConversationChanged={() => {
              void conversations.refresh();
            }}
          />
        </section>

        <section className="sf-host-copilot-column" aria-label="AI Copilot">
          {selectedConversationId ? (
            <CopilotPanel
              conversationId={selectedConversationId}
              copilot={copilot}
              onUseDraft={(draft) => {
                setCopilotDraft(draft);
                setCopilotDraftVersion((current) => current + 1);
              }}
              selectedPropertyId={selectedConversation?.propertyId ?? null}
              onViewKnowledge={(propertyId) => {
                const targetPropertyId = resolvePropertyKnowledgePropertyId(
                  propertyId ?? selectedConversation?.propertyId,
                  configuredDemoPropertyId,
                  import.meta.env.DEV
                );
                if (targetPropertyId) {
                  window.location.href = `/host/properties/${targetPropertyId}/knowledge`;
                }
              }}
            />
          ) : (
            <aside className="sf-host-selection-panel" aria-live="polite">
              <h3>Copilot</h3>
              <p>Select a conversation to view suggestions and grounded context.</p>
            </aside>
          )}
        </section>
      </div>
    </div>
  );
}
