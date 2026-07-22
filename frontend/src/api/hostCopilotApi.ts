import type { CopilotSuggestReplyRequest, CopilotSuggestReplyResponse } from "../models/copilot";
import type { HttpClient } from "./httpClient";

export function createHostCopilotApi(http: HttpClient) {
  return {
    suggestReply(conversationId: string, request: CopilotSuggestReplyRequest) {
      return http.post<CopilotSuggestReplyResponse>(`/copilot/conversations/${conversationId}/suggest-reply`, request);
    }
  };
}