import type {
  ConversationDetailResponse,
  ConversationHistoryResponse,
  ConversationListQuery,
  ConversationListResponse,
  ConversationMessageResponse
} from "../models/hostConversations";
import type { HttpClient } from "./httpClient";

export function createHostConversationsApi(http: HttpClient) {
  return {
    listConversations(query: ConversationListQuery) {
      const params = new URLSearchParams();

      if (query.status !== undefined) {
        params.set("status", String(query.status));
      }

      if (query.propertyId) {
        params.set("propertyId", query.propertyId);
      }

      if (query.requiresHostAttention !== undefined) {
        params.set("requiresHostAttention", String(query.requiresHostAttention));
      }

      if (query.search?.trim()) {
        params.set("search", query.search.trim());
      }

      params.set("page", String(query.page));
      params.set("pageSize", String(query.pageSize));

      return http.get<ConversationListResponse>(`/conversations?${params.toString()}`);
    },

    getConversation(conversationId: string) {
      return http.get<ConversationDetailResponse>(`/conversations/${conversationId}`);
    },

    getMessages(conversationId: string, page = 1, pageSize = 25) {
      const params = new URLSearchParams({
        pageNumber: String(page),
        pageSize: String(pageSize)
      });

      return http.get<ConversationHistoryResponse>(`/conversations/${conversationId}/messages?${params.toString()}`);
    },

    addHostMessage(conversationId: string, content: string) {
      return http.post<ConversationMessageResponse>(`/conversations/${conversationId}/messages/host`, { content });
    },

    addInternalNote(conversationId: string, content: string) {
      return http.post<ConversationMessageResponse>(`/conversations/${conversationId}/notes`, { content });
    },

    enableHumanTakeover(conversationId: string) {
      return http.post<ConversationDetailResponse>(`/conversations/${conversationId}/human-takeover`);
    },

    returnToAI(conversationId: string) {
      return http.post<ConversationDetailResponse>(`/conversations/${conversationId}/return-to-ai`);
    },

    resolveConversation(conversationId: string) {
      return http.post<ConversationDetailResponse>(`/conversations/${conversationId}/resolve`);
    },

    closeConversation(conversationId: string) {
      return http.post<ConversationDetailResponse>(`/conversations/${conversationId}/close`);
    }
  };
}
