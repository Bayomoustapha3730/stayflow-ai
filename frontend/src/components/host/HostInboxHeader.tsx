interface HostInboxHeaderProps {
  isRefreshing: boolean;
  onRefresh: () => void;
  onSignOut: () => void;
}

export function HostInboxHeader({ isRefreshing, onRefresh, onSignOut }: HostInboxHeaderProps) {
  return (
    <header className="sf-host-header">
      <div>
        <div className="sf-host-kicker">StayFlow Host Console</div>
        <h1>Conversation Inbox</h1>
      </div>

      <div className="sf-host-header-actions">
        <button type="button" onClick={onRefresh} disabled={isRefreshing}>
          {isRefreshing ? "Refreshing..." : "Refresh"}
        </button>
        <button type="button" onClick={onSignOut}>
          Sign out
        </button>
      </div>
    </header>
  );
}
