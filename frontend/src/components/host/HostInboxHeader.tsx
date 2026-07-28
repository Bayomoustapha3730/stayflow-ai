interface HostInboxHeaderProps {
  isRefreshing: boolean;
  realtimeState: "offline" | "connecting" | "online" | "reconnecting";
  totalUnreadCount: number;
  notificationsEnabled: boolean;
  notificationsSupported: boolean;
  onRefresh: () => void;
  onEnableNotifications: () => void;
  onSignOut: () => void;
}

export function HostInboxHeader({
  isRefreshing,
  realtimeState,
  totalUnreadCount,
  notificationsEnabled,
  notificationsSupported,
  onRefresh,
  onEnableNotifications,
  onSignOut
}: HostInboxHeaderProps) {
  const realtimeLabel =
    realtimeState === "online"
      ? "Live"
      : realtimeState === "reconnecting"
        ? "Reconnecting"
        : realtimeState === "connecting"
          ? "Connecting"
          : "Offline";

  return (
    <header className="sf-host-header">
      <div>
        <div className="sf-host-kicker">StayFlow Host Console</div>
        <h1>Conversation Inbox</h1>
        <p className="sf-host-header-meta" aria-live="polite">
          <span className={`sf-host-connection sf-host-connection-${realtimeState}`}>{realtimeLabel}</span>
          <span>{totalUnreadCount} unread total</span>
        </p>
      </div>

      <div className="sf-host-header-actions">
        {notificationsSupported ? (
          <button type="button" onClick={onEnableNotifications} aria-label="Enable notifications">
            {notificationsEnabled ? "Notifications enabled" : "Enable notifications"}
          </button>
        ) : (
          <button type="button" disabled aria-label="Notifications unsupported">
            Notifications unavailable
          </button>
        )}
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
