import type {
  AddInternalNoteRequest,
  AddHostMessageRequest,
  ConversationDetailResponse,
  ConversationHistoryQuery,
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

    getMessages(conversationId: string, query?: ConversationHistoryQuery) {
      const params = new URLSearchParams();

      if (query?.includeInternal !== undefined) {
        params.set("includeInternal", String(query.includeInternal));
      }

      if (query?.pageNumber !== undefined) {
        params.set("pageNumber", String(query.pageNumber));
      }

      if (query?.pageSize !== undefined) {
        params.set("pageSize", String(query.pageSize));
      }

      const queryString = params.toString();
      const path = queryString
        ? `/conversations/${conversationId}/messages?${queryString}`
        : `/conversations/${conversationId}/messages`;

      return http.get<ConversationHistoryResponse>(path);
    },

    addHostMessage(conversationId: string, content: string) {
      const payload: AddHostMessageRequest = { content };
      return http.post<ConversationMessageResponse>(`/conversations/${conversationId}/messages/host`, payload);
    },

    addInternalNote(conversationId: string, content: string) {
      const payload: AddInternalNoteRequest = { content };
      return http.post<ConversationMessageResponse>(`/conversations/${conversationId}/notes`, payload);
    },

    enableHumanTakeover(conversationId: string) {
      return http.post<ConversationDetailResponse>(`/conversations/${conversationId}/human-takeover`);
    },

    returnToAI(conversationId: string) {
      return http.post<ConversationDetailResponse>(`/conversations/${conversationId}/return-to-ai`);
    },

    assignToMe(conversationId: string) {
      return http.post<ConversationDetailResponse>(`/conversations/${conversationId}/assign-me`);
    },

    unassign(conversationId: string) {
      return http.post<ConversationDetailResponse>(`/conversations/${conversationId}/unassign`);
    },

    markRead(conversationId: string) {
      return http.post<boolean>(`/conversations/${conversationId}/read`);
    },

    resolveConversation(conversationId: string) {
      return http.post<ConversationDetailResponse>(`/conversations/${conversationId}/resolve`);
    },

    closeConversation(conversationId: string) {
      return http.post<ConversationDetailResponse>(`/conversations/${conversationId}/close`);
    }
  };
}
