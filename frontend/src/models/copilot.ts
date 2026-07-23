export interface CopilotSuggestReplyRequest {
  guidance?: string;
  tone?: CopilotTone;
  includeInternalNotes?: boolean;
  maxContextMessages?: number;
}

export type CopilotTone = "professional" | "friendly" | "luxury" | "casual";

export const COPILOT_MAX_INSTRUCTION_LENGTH = 600;
export const COPILOT_MAX_GENERATED_REPLY_LENGTH = 700;

export type CopilotUrgency = "low" | "medium" | "high";

export interface ConversationCopilotSummaryResponse {
  conversationId: string;
  summary: string;
  guestIntent?: string | null;
  importantFacts?: string[] | null;
  urgency?: CopilotUrgency | null;
  latestGuestMessage?: string | null;
  visibleMessageCount: number;
  generatedAt: string;
}

export interface ConversationCopilotSuggestionsResponse {
  conversationId: string;
  tone?: CopilotTone;
  suggestedReplies: string[];
  contextMessageCount: number;
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
  generatedAt: string;
}