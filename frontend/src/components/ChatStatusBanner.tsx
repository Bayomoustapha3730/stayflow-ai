import { ConversationStatus } from "../models/enums";

interface ChatStatusBannerProps {
  status: ConversationStatus | null;
  requiresHostAttention: boolean;
  humanTakeoverEnabled: boolean;
  message?: string | null;
}

export function ChatStatusBanner({ status, requiresHostAttention, humanTakeoverEnabled, message }: ChatStatusBannerProps) {
  if (status === ConversationStatus.Closed) {
    return <div className="sf-chat-status sf-chat-status-closed">This conversation has ended.</div>;
  }

  if (humanTakeoverEnabled || requiresHostAttention) {
    return <div className="sf-chat-status">{message ?? "A host has been notified and will respond as soon as possible."}</div>;
  }

  return null;
}
