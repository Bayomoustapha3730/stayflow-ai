import type { ConversationMessageType, ConversationSenderType, ConversationStatus, GuestChannel } from "./enums";
import type { PagedResult } from "./chat";

export interface ConversationListQuery {
  status?: ConversationStatus;
  propertyId?: string;
  requiresHostAttention?: boolean;
  search?: string;
  page: number;
  pageSize: number;
}

export interface ConversationListResponse {
  items: ConversationSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  totalUnreadCount: number;
}

export interface ConversationSummary {
  id: string;
  conversationId: string;
  guestId: string;
  reservationId?: string | null;
  propertyId?: string | null;
  status: ConversationStatus;
  channel: GuestChannel;
  channelIdentity?: string | null;
  subject?: string | null;
  guest?: ConversationGuestSummary | null;
  property?: ConversationPropertySummary | null;
  reservation?: ConversationReservationSummary | null;
  assignedUser?: ConversationAssignedUserSummary | null;
  humanTakeoverEnabled: boolean;
  requiresHostAttention: boolean;
  escalationReason?: string | null;
  startedAt: string;
  lastActivityAt: string;
  closedAt?: string | null;
  latestVisibleMessagePreview?: string | null;
  latestVisibleMessageSenderType?: ConversationSenderType | null;
  latestVisibleMessageTimestamp?: string | null;
  totalVisibleMessageCount: number;
  unreadMessageCount: number;
  lastReadAt?: string | null;
}

export interface ConversationDetail extends ConversationSummary {
  messages: ConversationMessage[];
}

export type ConversationDetailResponse = ConversationDetail;

export interface ConversationMessage {
  id: string;
  conversationId: string;
  senderType: ConversationSenderType;
  messageType: ConversationMessageType;
  content: string;
  isInternal: boolean;
  sentAt: string;
  authorDisplayName?: string | null;
  deliveryStatus?: "sending" | "failed";
  optimisticId?: string;
}

export type ConversationMessageResponse = ConversationMessage;

export interface ConversationHistoryQuery {
  includeInternal?: boolean;
  pageNumber?: number;
  pageSize?: number;
}

export interface ConversationHistoryResponse {
  conversationId: string;
  messages: PagedResult<ConversationMessage>;
}

export interface AddHostMessageRequest {
  content: string;
  sentAt?: string;
}

export interface AddInternalNoteRequest {
  content: string;
}

export interface ConversationGuestSummary {
  id: string;
  fullName: string;
  preferredLanguage: string;
  firstName: string;
  lastName: string;
  email?: string | null;
}

export interface ConversationPropertySummary {
  id: string;
  name: string;
  city: string;
}

export interface ConversationReservationSummary {
  id: string;
  confirmationNumber?: string | null;
  checkInDate: string;
  checkOutDate: string;
  status: number;
}

export interface ConversationAssignedUserSummary {
  id: string;
  fullName: string;
}
