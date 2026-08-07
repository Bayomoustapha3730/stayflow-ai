import type { InvitationDecisionRequest } from "../models/auth";
import type { HttpClient } from "./httpClient";

export function createInvitationApi(http: HttpClient) {
  return {
    accept(request: InvitationDecisionRequest) {
      return http.post<Record<string, never>>("/api/organization/invitations/accept", request);
    },
    reject(request: InvitationDecisionRequest) {
      return http.post<Record<string, never>>("/api/organization/invitations/reject", request);
    }
  };
}