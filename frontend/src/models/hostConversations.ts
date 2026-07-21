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
}

export interface ConversationDetailResponse extends ConversationSummary {
  messages: ConversationMessageResponse[];
}

export interface ConversationMessageResponse {
  id: string;
  conversationId: string;
  senderType: ConversationSenderType;
  messageType: ConversationMessageType;
  content: string;
  isInternal: boolean;
  sentAt: string;
}

export interface ConversationHistoryResponse {
  conversationId: string;
  messages: PagedResult<ConversationMessageResponse>;
}

export interface ConversationGuestSummary {
  id: string;
  guestId: string;
  fullName: string;
  preferredLanguage: string;
  firstName: string;
  lastName: string;
  email?: string | null;
}

export interface ConversationPropertySummary {
  id: string;
  propertyId: string;
  name: string;
  city: string;
}

export interface ConversationReservationSummary {
  id: string;
  reservationId: string;
  confirmationNumber?: string | null;
  checkInDate: string;
  checkOutDate: string;
  status: number;
}

export interface ConversationAssignedUserSummary {
  id: string;
  userId: string;
  fullName: string;
}
