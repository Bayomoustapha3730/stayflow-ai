import {
  HostConversationDetail,
  HostConversationFilters,
  HostConversationList,
  HostInboxHeader,
  HostInboxSummary,
  HostLoginPanel
} from "../components/host";
import { useHostAuth } from "../hooks/useHostAuth";
import { useHostConversations } from "../hooks/useHostConversations";
import { useEffect, useMemo, useRef, useState } from "react";
import { ConversationSenderType } from "../models/enums";
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
  const previousMessageTimestampsRef = useRef<Record<string, string | null>>({});

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
  const selectedConversationId = conversations.selectedConversationId;
  const connectionStatus: "live" | "reconnecting" | "degraded" | "offline" =
    conversations.realtimeState === "online"
      ? "live"
      : conversations.realtimeState === "reconnecting"
        ? "reconnecting"
        : conversations.isHttpAvailable
          ? "degraded"
          : "offline";

  return (
    <div className="sf-host-page">
      <HostInboxHeader
        isRefreshing={conversations.isLoading}
        connectionStatus={connectionStatus}
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

      {conversations.sessionExpired ? (
        <div className="sf-host-session-expired" role="alert">
          Your host session expired. Please sign in again.
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

      <div className="sf-host-main-grid">
        <section className="sf-host-list-column">
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

        <HostConversationDetail
          conversationId={selectedConversationId}
          accessToken={auth.accessToken}
          onUnauthorized={auth.logout}
          onConversationChanged={() => {
            void conversations.refresh();
          }}
        />
      </div>
    </div>
  );
}
