import type {
  HostCopilotWorkspaceResponse,
  HostCopilotDraftGenerateRequest,
  HostCopilotDraftResponse,
  HostCopilotDraftValidateRequest,
  HostCopilotDraftValidationResponse,
  HostCopilotDraftSendRequest
} from "../models/hostCopilotWorkspace";
import type { HostActionDecisionRequest, HostActionListItem } from "../models/hostActions";
import type { ConversationMessage } from "../models/hostConversations";
import type { HttpClient } from "./httpClient";

export function createHostCopilotWorkspaceApi(http: HttpClient) {
  return {
    getWorkspace(propertyId?: string | null) {
      const params = new URLSearchParams();
      if (propertyId) {
        params.set("propertyId", propertyId);
      }

      const query = params.toString();
      return http.get<HostCopilotWorkspaceResponse>(`/host/copilot/workspace${query ? `?${query}` : ""}`);
    },

    generateDraft(conversationId: string, request: HostCopilotDraftGenerateRequest) {
      return http.post<HostCopilotDraftResponse>(`/host/copilot/conversations/${conversationId}/draft`, request);
    },

    validateDraft(conversationId: string, request: HostCopilotDraftValidateRequest) {
      return http.post<HostCopilotDraftValidationResponse>(`/host/copilot/conversations/${conversationId}/draft/validate`, request);
    },

    sendDraft(conversationId: string, request: HostCopilotDraftSendRequest) {
      return http.post<ConversationMessage>(`/host/copilot/conversations/${conversationId}/draft/send`, request);
    },

    approveAction(actionId: string, request: HostActionDecisionRequest) {
      return http.post<HostActionListItem>(`/host/copilot/actions/${actionId}/approve`, request);
    },

    declineAction(actionId: string, request: HostActionDecisionRequest) {
      return http.post<HostActionListItem>(`/host/copilot/actions/${actionId}/decline`, request);
    }
  };
}
