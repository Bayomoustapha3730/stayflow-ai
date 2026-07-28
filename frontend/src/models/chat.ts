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
  feedback?: ChatMessageFeedbackSummary | null;
}

export enum ConversationMessageFeedbackValue {
  Helpful = 0,
  NotHelpful = 1
}

export interface ChatMessageFeedbackSummary {
  feedbackValue: ConversationMessageFeedbackValue;
  submittedAt: string;
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
  pendingAction?: PendingActionCard | null;
  createdAt: string;
}

export enum PendingConciergeActionStatus {
  AwaitingGuestConfirmation = 0,
  AwaitingHostApproval = 1,
  ReadyToExecute = 2,
  Executing = 3,
  Completed = 4,
  Failed = 5,
  Cancelled = 6,
  Expired = 7
}

export enum ConciergeActionConfirmationRequirement {
  None = 0,
  ExplicitGuestConfirmation = 1,
  HostApproval = 2,
  Both = 3
}

export enum ConciergeActionType {
  None = 0,
  RequestEarlyCheckIn = 1,
  RequestLateCheckout = 2,
  CreateMaintenanceTicket = 3,
  RequestHousekeeping = 4,
  RequestExtraItem = 5,
  RequestParking = 6,
  NotifyHost = 7
}

export interface PendingActionCard {
  actionId: string;
  actionType: ConciergeActionType;
  status: PendingConciergeActionStatus;
  confirmationRequirement: ConciergeActionConfirmationRequirement;
  prompt: string;
  requiresHostApproval: boolean;
  expiresAt: string;
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

export interface AddChatMessageFeedbackRequest {
  guestId: string;
  feedbackValue: ConversationMessageFeedbackValue;
  comment?: string;
}

export interface ChatMessageFeedbackResponse {
  id: string;
  conversationId: string;
  conversationMessageId: string;
  guestId: string;
  feedbackValue: ConversationMessageFeedbackValue;
  comment?: string | null;
  submittedAt: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
}
