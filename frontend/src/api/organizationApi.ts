import type {
  OrganizationMember,
  OrganizationSummary,
  UpdateOrganizationRequest
} from "../models/organization";
import type { HttpClient } from "./httpClient";

export function createOrganizationApi(http: HttpClient) {
  return {
    getCurrent() {
      return http.get<OrganizationSummary>("/organization/current");
    },
    updateCurrent(request: UpdateOrganizationRequest) {
      return http.put<OrganizationSummary>("/organization/current", request);
    },
    listMembers() {
      return http.get<OrganizationMember[]>("/organization/current/members");
    },
    updateMemberRole(memberUserId: string, role: string) {
      return http.put<OrganizationMember>(`/organization/current/members/${memberUserId}/role`, { role });
    },
    removeMember(memberUserId: string) {
      return http.delete<{ memberUserId: string }>(`/organization/current/members/${memberUserId}`);
    }
  };
}