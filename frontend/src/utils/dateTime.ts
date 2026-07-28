import { ConversationSenderType, ConversationStatus } from "../models/enums";

const dateFormatter = new Intl.DateTimeFormat(undefined, {
  month: "short",
  day: "numeric",
  year: "numeric"
});

const dateTimeFormatter = new Intl.DateTimeFormat(undefined, {
  month: "short",
  day: "numeric",
  hour: "numeric",
  minute: "2-digit"
});

export function formatRelativeTime(isoDate?: string | null): string {
  if (!isoDate) {
    return "No activity";
  }

  const value = new Date(isoDate);
  if (Number.isNaN(value.getTime())) {
    return "No activity";
  }

  const diffMs = Date.now() - value.getTime();
  const diffMinutes = Math.round(diffMs / 60000);

  if (diffMinutes < 1) return "Just now";
  if (diffMinutes < 60) return `${diffMinutes}m ago`;

  const diffHours = Math.round(diffMinutes / 60);
  if (diffHours < 24) return `${diffHours}h ago`;

  const diffDays = Math.round(diffHours / 24);
  if (diffDays < 7) return `${diffDays}d ago`;

  return dateTimeFormatter.format(value);
}

export function formatReservationRange(checkInDate?: string | null, checkOutDate?: string | null): string {
  if (!checkInDate || !checkOutDate) {
    return "Dates unavailable";
  }

  const checkIn = new Date(checkInDate);
  const checkOut = new Date(checkOutDate);

  if (Number.isNaN(checkIn.getTime()) || Number.isNaN(checkOut.getTime())) {
    return "Dates unavailable";
  }

  return `${dateFormatter.format(checkIn)} - ${dateFormatter.format(checkOut)}`;
}

export function formatStatusLabel(status: ConversationStatus): string {
  switch (status) {
    case ConversationStatus.AwaitingGuest:
      return "Awaiting Guest";
    case ConversationStatus.AwaitingHost:
      return "Awaiting Host";
    case ConversationStatus.Escalated:
      return "Escalated";
    case ConversationStatus.HumanManaged:
      return "Human Managed";
    case ConversationStatus.Resolved:
      return "Resolved";
    case ConversationStatus.Closed:
      return "Closed";
    default:
      return "Open";
  }
}

export function formatSenderLabel(sender?: ConversationSenderType | null): string {
  switch (sender) {
    case ConversationSenderType.Guest:
      return "Guest";
    case ConversationSenderType.Host:
      return "Host";
    case ConversationSenderType.AI:
      return "AI";
    case ConversationSenderType.System:
      return "System";
    default:
      return "Unknown";
  }
}
