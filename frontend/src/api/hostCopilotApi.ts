import type {
  ConversationCopilotSuggestionsResponse,
  ConversationCopilotSummaryResponse,
  CopilotSuggestReplyRequest,
  CopilotSuggestReplyResponse,
  CopilotTone
} from "../models/copilot";
import type { HttpClient } from "./httpClient";

export function createHostCopilotApi(http: HttpClient) {
  return {
    getConversationSummary(conversationId: string) {
      return http.get<ConversationCopilotSummaryResponse>(`/copilot/conversations/${conversationId}/summary`);
    },

    getSuggestedReplies(conversationId: string, tone: CopilotTone) {
      const params = new URLSearchParams({ tone });
      return http.get<ConversationCopilotSuggestionsResponse>(
        `/copilot/conversations/${conversationId}/suggested-replies?${params.toString()}`
      );
    },

    generateReply(conversationId: string, request: CopilotSuggestReplyRequest) {
      return http.post<CopilotSuggestReplyResponse>(`/copilot/conversations/${conversationId}/generate-reply`, request);
    }
  };
}