import type {
  AddChatMessageFeedbackRequest,
  ChatConversation,
  ChatMessageFeedbackResponse,
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
    },
    markConversationRead(conversationId: string, guestId: string) {
      return http.post<boolean>(`/chat/conversations/${conversationId}/read`, { guestId });
    },
    submitMessageFeedback(conversationId: string, messageId: string, request: AddChatMessageFeedbackRequest) {
      return http.post<ChatMessageFeedbackResponse>(`/chat/${conversationId}/messages/${messageId}/feedback`, request);
    },
    confirmPendingAction(conversationId: string, actionId: string, guestId: string) {
      return http.post<ChatMessageResponse>(`/chat/${conversationId}/actions/${actionId}/confirm`, { guestId });
    },
    cancelPendingAction(conversationId: string, actionId: string, guestId: string) {
      return http.post<ChatMessageResponse>(`/chat/${conversationId}/actions/${actionId}/cancel`, { guestId });
    }
  };
}
