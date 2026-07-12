import type { StayFlowChatTheme } from "../models/theme";
import { ConversationStatus, statusLabel } from "../models/enums";

interface ChatHeaderProps {
  theme: StayFlowChatTheme;
  status: ConversationStatus | null;
  onClose: () => void;
  onEnd: () => void;
  canEnd: boolean;
}

export function ChatHeader({ theme, status, onClose, onEnd, canEnd }: ChatHeaderProps) {
  return (
    <header className="sf-chat-header">
      <div className="sf-chat-brand">
        {theme.logoUrl ? <img src={theme.logoUrl} alt="" className="sf-chat-logo" /> : <span className="sf-chat-logo-mark">S</span>}
        <div>
          <h2>{theme.assistantName}</h2>
          <p>{statusLabel(status ?? ConversationStatus.Open)}</p>
        </div>
      </div>
      <div className="sf-chat-header-actions">
        <button type="button" onClick={onEnd} disabled={!canEnd} title="End conversation" aria-label="End conversation">
          End
        </button>
        <button type="button" onClick={onClose} aria-label="Close chat">
          x
        </button>
      </div>
    </header>
  );
}
