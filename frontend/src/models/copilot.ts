export interface CopilotSuggestReplyRequest {
  guidance?: string;
  tone?: CopilotTone;
  hostDraft?: string;
  includeInternalNotes?: boolean;
  maxContextMessages?: number;
}

export type GuestIntent =
  | "WiFi"
  | "CheckIn"
  | "Checkout"
  | "Parking"
  | "HouseRules"
  | "Amenities"
  | "Laundry"
  | "Thermostat"
  | "Trash"
  | "Emergency"
  | "Accessibility"
  | "Maintenance"
  | "Noise"
  | "Refund"
  | "Cancellation"
  | "ReservationChange"
  | "LateArrival"
  | "EarlyCheckIn"
  | "GeneralQuestion"
  | "Unknown";

export interface CopilotOrchestrationWarning {
  code: string;
  message: string;
  severity: "info" | "warning" | "error" | string;
}

export type CopilotConfidenceLevel = "High" | "Medium" | "Low";

export type CopilotContextWarning =
  | "MissingProperty"
  | "MissingReservation"
  | "NoApprovedKnowledge"
  | "NoVisibleMessages"
  | "ContextTruncated"
  | "AmbiguousGuestRequest"
  | "ConflictingKnowledge";

export interface CopilotConfidence {
  score: number;
  level: CopilotConfidenceLevel;
  reasons: string[];
  missingContext: CopilotContextWarning[];
}

export interface CopilotSource {
  sourceType: "Conversation" | "Reservation" | "Property" | "PropertyKnowledge";
  title: string;
  category?: string | null;
  relevanceReason?: string | null;
  lastUpdated?: string | null;
}

export type CopilotTone = "professional" | "friendly" | "luxury" | "casual";

export type CopilotUrgency = "low" | "medium" | "high";

export interface ConversationCopilotSummaryResponse {
  conversationId: string;
  summary: string;
  guestIntent?: string | null;
  importantFacts?: string[] | null;
  urgency?: CopilotUrgency | null;
  latestGuestMessage?: string | null;
  visibleMessageCount: number;
  confidence?: CopilotConfidence | null;
  sources?: CopilotSource[] | null;
  warnings?: CopilotContextWarning[] | null;
  contextTruncated?: boolean;
  generatedAt: string;
}

export interface ConversationCopilotSuggestionsResponse {
  conversationId: string;
  suggestedReplies: string[];
  contextMessageCount: number;
  detectedIntent?: GuestIntent | null;
  confidence?: CopilotConfidence | null;
  sources?: CopilotSource[] | null;
  warnings?: CopilotContextWarning[] | null;
  orchestrationWarnings?: CopilotOrchestrationWarning[] | null;
  provider?: string | null;
  isMock?: boolean;
  fallbackUsed?: boolean;
  contextTruncated?: boolean;
  generatedAt: string;
}

export interface CopilotProviderMetadata {
  providerName?: string | null;
  modelName?: string | null;
  requestId?: string | null;
}

export interface CopilotSuggestReplyResponse {
  conversationId: string;
  suggestedReply: string;
  tone?: CopilotTone | null;
  detectedIntent?: GuestIntent | null;
  rationale?: string | null;
  contextMessageCount: number;
  isFallback: boolean;
  fallbackUsed?: boolean;
  requiresHumanReview?: boolean;
  provider?: string | null;
  isMock?: boolean;
  providerMetadata?: CopilotProviderMetadata | null;
  confidence?: CopilotConfidence | null;
  sources?: CopilotSource[] | null;
  warnings?: CopilotContextWarning[] | null;
  orchestrationWarnings?: CopilotOrchestrationWarning[] | null;
  contextTruncated?: boolean;
  generatedAt: string;
}