interface ChatLauncherProps {
  isOpen: boolean;
  unreadCount: number;
  onClick: () => void;
}

export function ChatLauncher({ isOpen, unreadCount, onClick }: ChatLauncherProps) {
  return (
    <button
      type="button"
      className="sf-chat-launcher"
      onClick={onClick}
      aria-label={isOpen ? "Close StayFlow chat" : "Open StayFlow chat"}
      aria-expanded={isOpen}
    >
      <span aria-hidden="true">{isOpen ? "x" : "?"}</span>
      {!isOpen && unreadCount > 0 ? <span className="sf-chat-unread">{unreadCount}</span> : null}
    </button>
  );
}
