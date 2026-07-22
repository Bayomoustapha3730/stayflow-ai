export interface CopilotSuggestReplyRequest {
  guidance?: string;
  includeInternalNotes?: boolean;
  maxContextMessages?: number;
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