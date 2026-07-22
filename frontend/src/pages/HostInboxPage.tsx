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
import "../styles/host-inbox.css";

export function HostInboxPage() {
  const auth = useHostAuth();
  const conversations = useHostConversations({
    accessToken: auth.accessToken,
    onUnauthorized: auth.logout
  });

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

  return (
    <div className="sf-host-page">
      <HostInboxHeader
        isRefreshing={conversations.isLoading}
        onRefresh={() => {
          void conversations.refresh();
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
