import {
  ConversationMessageType,
  ConversationSenderType,
  ConversationStatus,
  GuestChannel
} from "./enums";

export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data?: T;
  errors: string[];
  correlationId: string;
}

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ChatMessage {
  id: string;
  conversationId: string;
  senderType: ConversationSenderType;
  content: string;
  messageType: ConversationMessageType;
  sentAt: string;
  localStatus?: "sending" | "sent" | "failed";
}

export interface ChatReservationSummary {
  confirmationNumber?: string | null;
  checkInDate?: string | null;
  checkOutDate?: string | null;
  propertyDisplayName?: string | null;
}

export interface ChatConversation {
  conversationId: string;
  status: ConversationStatus;
  channel: GuestChannel;
  subject?: string | null;
  humanTakeoverEnabled: boolean;
  requiresHostAttention: boolean;
  startedAt: string;
  lastActivityAt: string;
  closedAt?: string | null;
  reservation?: ChatReservationSummary | null;
  recentMessages: ChatMessage[];
}

export interface SendChatMessageRequest {
  conversationId?: string;
  guestId: string;
  reservationId?: string;
  propertyId?: string;
  message: string;
  channel: GuestChannel;
  channelIdentity?: string;
  externalMessageId?: string;
  explicitReservationReference?: string;
  explicitPropertyName?: string;
  currentTimestamp?: string;
}

export interface ChatProviderMetadata {
  providerName?: string | null;
  modelName?: string | null;
  requestId?: string | null;
}

export interface ChatMessageResponse {
  conversationId: string;
  conversationStatus: ConversationStatus;
  guestMessage: ChatMessage;
  assistantMessage?: ChatMessage | null;
  humanTakeoverEnabled: boolean;
  requiresHostAttention: boolean;
  escalationReason?: string | null;
  providerMetadata?: ChatProviderMetadata | null;
  createdAt: string;
}

export interface ChatHistoryResponse {
  conversationId: string;
  messages: PagedResult<ChatMessage>;
}

export interface ChatStatusResponse {
  conversationId: string;
  status: ConversationStatus;
  humanTakeoverEnabled: boolean;
  requiresHostAttention: boolean;
  guestSafeMessage: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
}
