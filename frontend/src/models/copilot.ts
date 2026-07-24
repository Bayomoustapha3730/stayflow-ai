export interface CopilotSuggestReplyRequest {
  guidance?: string;
  includeInternalNotes?: boolean;
  maxContextMessages?: number;
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
  confidence?: CopilotConfidence | null;
  sources?: CopilotSource[] | null;
  warnings?: CopilotContextWarning[] | null;
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
  rationale?: string | null;
  contextMessageCount: number;
  isFallback: boolean;
  providerMetadata?: CopilotProviderMetadata | null;
  confidence?: CopilotConfidence | null;
  sources?: CopilotSource[] | null;
  warnings?: CopilotContextWarning[] | null;
  contextTruncated?: boolean;
  generatedAt: string;
}