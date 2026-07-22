import { useMemo } from "react";
import type { CSSProperties } from "react";
import { useChat } from "../hooks/useChat";
import { defaultChatTheme, type StayFlowChatTheme } from "../models/theme";
import { ChatLauncher } from "./ChatLauncher";
import { ChatPanel } from "./ChatPanel";

export interface StayFlowChatWidgetProps {
  guestId?: string;
  reservationId?: string;
  propertyId?: string;
  channelIdentity?: string;
  apiBaseUrl?: string;
  demoEmail?: string;
  theme?: Partial<StayFlowChatTheme>;
}

export function StayFlowChatWidget({
  guestId,
  reservationId,
  propertyId,
  channelIdentity,
  apiBaseUrl = import.meta.env.VITE_STAYFLOW_API_URL ?? "http://localhost:5243",
  demoEmail,
  theme
}: StayFlowChatWidgetProps) {
  const resolvedTheme = useMemo(() => ({ ...defaultChatTheme, ...theme }), [theme]);
  const chat = useChat({ apiBaseUrl, guestId, reservationId, propertyId, channelIdentity });

  const style = {
    "--sf-chat-primary": resolvedTheme.primaryColor,
    "--sf-chat-accent": resolvedTheme.accentColor,
    "--sf-chat-bg": resolvedTheme.backgroundColor,
    "--sf-chat-text": resolvedTheme.textColor,
    "--sf-chat-guest": resolvedTheme.guestBubbleColor,
    "--sf-chat-assistant": resolvedTheme.assistantBubbleColor,
    "--sf-chat-radius": resolvedTheme.borderRadius
  } as CSSProperties;

  return (
    <div className={`sf-chat-root sf-chat-${resolvedTheme.buttonPosition}`} style={style}>
      {chat.isOpen ? <ChatPanel chat={chat} theme={resolvedTheme} demoEmail={demoEmail} /> : null}
      <ChatLauncher isOpen={chat.isOpen} unreadCount={chat.unreadCount} onClick={chat.toggle} />
    </div>
  );
}
