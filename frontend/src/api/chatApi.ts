import type {
  ChatConversation,
  ChatHistoryResponse,
  ChatMessageResponse,
  ChatStatusResponse,
  SendChatMessageRequest
} from "../models/chat";
import type { HttpClient } from "./httpClient";

export function createChatApi(http: HttpClient) {
  return {
    sendChatMessage(request: SendChatMessageRequest) {
      return http.post<ChatMessageResponse>("/chat/message", request);
    },
    getChatConversation(conversationId: string) {
      return http.get<ChatConversation>(`/chat/${conversationId}`);
    },
    getChatHistory(conversationId: string, pageNumber = 1, pageSize = 20) {
      return http.get<ChatHistoryResponse>(`/chat/${conversationId}/history?pageNumber=${pageNumber}&pageSize=${pageSize}`);
    },
    escalateChatConversation(conversationId: string, guestId: string, reason?: string) {
      return http.post<ChatStatusResponse>(`/chat/${conversationId}/escalate`, { guestId, reason });
    },
    endChatConversation(conversationId: string, guestId: string) {
      return http.post<ChatStatusResponse>(`/chat/${conversationId}/end`, { guestId });
    }
  };
}
